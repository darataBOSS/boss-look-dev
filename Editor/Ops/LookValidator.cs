using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Boss.LookDev.Editor.Ops
{
    public enum LookIssueSeverity { Info, Warning, Error }

    public class LookIssue
    {
        public LookIssueSeverity severity;
        public string message;
        public List<UnityEngine.Object> targets = new List<UnityEngine.Object>();
        public string fixLabel;
        public Action fix;
    }

    /// <summary>
    /// (E) Validator skeleton (spec §4.3): catches "ships broken on device"
    /// settings before handoff. v1 increment covers pipeline/target mismatches,
    /// color space, camera HDR, color temperature, probe presence, white albedo,
    /// and missing UV2. Target-specific STYLY / Pico checks and the handoff doc
    /// land in later increments.
    /// </summary>
    public static class LookValidator
    {
        private static readonly string[] AlbedoProps = { "_BaseColor", "_Color" };

        public static List<LookIssue> Run(LookDefinition look)
        {
            var issues = new List<LookIssue>();
            if (look == null) return issues;

            var active = RenderPipelineDetector.Active;

            if (look.IsPipelineMismatch(active))
                issues.Add(new LookIssue
                {
                    severity = LookIssueSeverity.Warning,
                    message = $"対象パイプライン ({look.targetPipeline}) が現在 ({active}) と不一致。プリセットはパイプライン間で移植できません (警告のみ)。",
                });

            if (RenderPipelineDetector.IsHdrpOrUnknown)
                issues.Add(new LookIssue
                {
                    severity = LookIssueSeverity.Error,
                    message = "HDRP / 未対応 SRP が有効です。本ツールは Built-in / URP のみ対応です。",
                });

            CheckStyly(look, active, issues);
            CheckColorSpace(look, issues);
            CheckCameraHdr(look, issues);
            CheckColorTemperature(issues);
            CheckProbes(look, issues);
            CheckWhiteAlbedo(issues);
            CheckMissingUv2(issues);

            if (issues.Count == 0)
                issues.Add(new LookIssue { severity = LookIssueSeverity.Info, message = "問題は見つかりませんでした ✨" });
            return issues;
        }

        private static void CheckStyly(LookDefinition look, LookPipeline active, List<LookIssue> issues)
        {
            if (look.target != LookTarget.STYLY) return;
            if (active != LookPipeline.BuiltIn)
                issues.Add(new LookIssue
                {
                    severity = LookIssueSeverity.Warning,
                    message = "Target=STYLY は Built-in 専用です。STYLY 向けは Built-in プロジェクトで制作してください (URP/HDRP 非対応)。",
                });
            issues.Add(new LookIssue
            {
                severity = LookIssueSeverity.Info,
                message = "STYLY ではカスタム C#・最新 PPv2 は使えません。color はライティング焼き込み + STYLY 側方式 (PPv1 / PlayMaker) に委譲し、レイヤーは Default を使用してください。",
            });
        }

        private static void CheckColorSpace(LookDefinition look, List<LookIssue> issues)
        {
            bool styly = look.target == LookTarget.STYLY;
            if (styly)
            {
                if (PlayerSettings.colorSpace == ColorSpace.Linear)
                    issues.Add(new LookIssue
                    {
                        severity = LookIssueSeverity.Warning,
                        message = "Target=STYLY ですが Color Space が Linear です。STYLY モバイルは Gamma 前提です。",
                    });
                return;
            }
            if (PlayerSettings.colorSpace == ColorSpace.Linear) return;
            issues.Add(new LookIssue
            {
                severity = LookIssueSeverity.Warning,
                message = "Color Space が Gamma です。Tonemapping / Color Grading が実質効かず、ライティング品質も落ちます (SelfApp は Linear 推奨)。",
                fixLabel = "Linear に変更 (全テクスチャ再インポート)",
                fix = () =>
                {
                    if (EditorUtility.DisplayDialog("BOSS Look Dev",
                        "Color Space を Linear に変更します。テクスチャ再インポートが走ります。よろしいですか?", "変更", "キャンセル"))
                        PlayerSettings.colorSpace = ColorSpace.Linear;
                },
            });
        }

        private static void CheckCameraHdr(LookDefinition look, List<LookIssue> issues)
        {
            if (!look.color.enabled || !look.color.bloom || look.target == LookTarget.STYLY) return;
            var cam = Camera.main;
            if (cam == null || cam.allowHDR) return;
            issues.Add(new LookIssue
            {
                severity = LookIssueSeverity.Warning,
                message = "Bloom が有効なのに Main Camera の HDR が OFF です。Bloom がほぼ効きません。",
                targets = { cam },
                fixLabel = "HDR を ON",
                fix = () => { Undo.RecordObject(cam, "Enable HDR"); cam.allowHDR = true; EditorUtility.SetDirty(cam); },
            });
        }

        private static void CheckColorTemperature(List<LookIssue> issues)
        {
            if (LightRigAuthoring.ColorTemperatureSupported) return;
            issues.Add(new LookIssue
            {
                severity = LookIssueSeverity.Warning,
                message = "Graphics Settings の色温度が無効です。リグの Kelvin 指定が効きません。",
                fixLabel = "色温度を有効化",
                fix = LightRigAuthoring.EnableColorTemperature,
            });
        }

        private static void CheckProbes(LookDefinition look, List<LookIssue> issues)
        {
            if (SceneBindingOps.FindComponentInScene<LightProbeGroup>(SceneBindingOps.ProbeGroupName(look)) == null)
                issues.Add(new LookIssue { severity = LookIssueSeverity.Info, message = "ライトプローブ未生成。動くオブジェクトがベイク照明を受け取れません。" });
            if (SceneBindingOps.FindComponentInScene<ReflectionProbe>(SceneBindingOps.ReflectionProbeName(look)) == null)
                issues.Add(new LookIssue { severity = LookIssueSeverity.Info, message = "リフレクションプローブ未生成。映り込みがスカイボックス頼みになります。" });
        }

        private static void CheckWhiteAlbedo(List<LookIssue> issues)
        {
            var offenders = new List<UnityEngine.Object>();
            var seen = new HashSet<Material>();
            foreach (var mr in SceneBindingOps.FindAll<MeshRenderer>())
            {
                foreach (var m in mr.sharedMaterials)
                {
                    if (m == null || !seen.Add(m)) continue;
                    var path = AssetDatabase.GetAssetPath(m);
                    if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets")) continue;
                    foreach (var prop in AlbedoProps)
                    {
                        if (!m.HasProperty(prop)) continue;
                        var c = m.GetColor(prop);
                        if (c.r > 0.95f && c.g > 0.95f && c.b > 0.95f) offenders.Add(m);
                        break;
                    }
                }
            }
            if (offenders.Count == 0) return;
            issues.Add(new LookIssue
            {
                severity = LookIssueSeverity.Warning,
                message = $"アルベドがほぼ純白のマテリアルが {offenders.Count} 個。GI で白飛びの原因になります (現実の白壁は反射率 0.8 程度)。",
                targets = offenders,
                fixLabel = "アルベドを 0.85 に下げる",
                fix = () =>
                {
                    foreach (var o in offenders)
                    {
                        var m = (Material)o;
                        Undo.RecordObject(m, "Clamp Albedo");
                        foreach (var prop in AlbedoProps)
                        {
                            if (!m.HasProperty(prop)) continue;
                            var c = m.GetColor(prop);
                            m.SetColor(prop, new Color(0.85f, 0.85f, 0.85f, c.a));
                            break;
                        }
                        EditorUtility.SetDirty(m);
                    }
                },
            });
        }

        private static void CheckMissingUv2(List<LookIssue> issues)
        {
            var offenders = new List<UnityEngine.Object>();
            var importers = new HashSet<ModelImporter>();
            foreach (var mf in SceneBindingOps.FindAll<MeshFilter>())
            {
                if (mf == null || mf.sharedMesh == null) continue;
                var flags = GameObjectUtility.GetStaticEditorFlags(mf.gameObject);
                if ((flags & StaticEditorFlags.ContributeGI) == 0) continue;
                if (mf.sharedMesh.uv2 != null && mf.sharedMesh.uv2.Length > 0) continue;
                offenders.Add(mf.gameObject);
                if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(mf.sharedMesh)) is ModelImporter mi)
                    importers.Add(mi);
            }
            if (offenders.Count == 0) return;
            var issue = new LookIssue
            {
                severity = LookIssueSeverity.Error,
                message = $"ContributeGI なのに UV2 が無いメッシュが {offenders.Count} 個。ベイクが壊れます。",
                targets = offenders,
            };
            if (importers.Count > 0)
            {
                issue.fixLabel = $"Generate Lightmap UVs を有効化 ({importers.Count} モデル)";
                issue.fix = () => { foreach (var mi in importers) { mi.generateSecondaryUV = true; mi.SaveAndReimport(); } };
            }
            issues.Add(issue);
        }
    }
}
