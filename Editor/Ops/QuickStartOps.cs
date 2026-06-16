using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Boss.LookDev.Editor.Ops
{
    /// <summary>
    /// (B) "Good defaults / quickstart" — the speed-and-quality main act (spec
    /// §4.1). One click turns a look from nothing into something that already
    /// reads as real: skybox/IBL → lighting settings → scene-derived probe area →
    /// static flags → probes → reflection → light rig → color. Everything it does
    /// equals the individual steps, so the result stays fully adjustable.
    /// </summary>
    public static class QuickStartOps
    {
        public static Vector3 ResolveSubject()
        {
            if (Selection.activeTransform != null) return Selection.activeTransform.position;
            if (TryComputeSceneBounds(out var b)) return b.center;
            return Vector3.zero;
        }

        public static bool TryComputeSceneBounds(out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (var r in SceneBindingOps.FindAll<MeshRenderer>())
            {
                if (r == null) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any;
        }

        public static string Run(LookDefinition look)
        {
            if (look == null) return "Look がありません。";
            bool hasHdri = look.lighting.hdri != null;
            bool hasSceneSky = RenderSettings.skybox != null;
            if (!hasHdri && !hasSceneSky)
                return "HDRI を設定するか、シーンにスカイボックスを用意してください。";

            var report = new StringBuilder();
            Undo.RegisterFullObjectHierarchyUndo(look, "Quick Start");

            if (hasHdri)
            {
                LightingBakeOps.SetupSkybox(look);
                report.AppendLine("✓ スカイボックス生成・環境光を Skybox に設定");
            }
            else
            {
                report.AppendLine("○ HDRI 未設定 — 既存のシーンスカイボックスを使用");
            }

            LightingBakeOps.CreateOrUpdateLightingSettings(look);
            report.AppendLine("✓ Lighting Settings 生成 (Auto Generate OFF)");

            if (TryComputeSceneBounds(out var sceneBounds))
            {
                sceneBounds.Expand(0.5f);
                look.lighting.probeArea = sceneBounds;
                report.AppendLine("✓ プローブ範囲をシーン全体から自動設定");
            }
            else
            {
                report.AppendLine("⚠ MeshRenderer が無いためプローブ範囲は既定値");
            }

            int flagged = LightingBakeOps.ApplyStaticFlagsToScene(out var missingUv2);
            report.AppendLine($"✓ ContributeGI 付与 ({flagged} 個)");
            if (missingUv2.Count > 0)
                report.AppendLine($"⚠ UV2 が空のメッシュ {missingUv2.Count} 個 (Generate Lightmap UVs を確認)");

            LightingBakeOps.CreateOrUpdateProbeGroup(look);
            report.AppendLine("✓ ライトプローブグリッド生成");

            LightingBakeOps.CreateOrUpdateReflectionProbe(look);
            report.AppendLine("✓ リフレクションプローブ生成 (Baked)");

            if (!LightRigAuthoring.ColorTemperatureSupported)
            {
                LightRigAuthoring.EnableColorTemperature();
                report.AppendLine("✓ 色温度 (Kelvin) を有効化");
            }
            LightRigAuthoring.CreateOrUpdateRig(look, ResolveSubject());
            report.AppendLine("✓ ライトリグ生成");

            // Color: STYLY bakes into lighting (no PPv2); SelfApp uses the post adapter.
            if (look.color.enabled)
            {
                if (look.target == LookTarget.STYLY)
                {
                    LookBakeOps.ApplyToLighting(look);
                    report.AppendLine("✓ カラーをライティング/環境光へ焼き込み (STYLY)");
                }
                else
                {
                    var adapter = AdapterRegistry.GetActiveColorAdapter();
                    if (adapter != null)
                    {
                        adapter.Setup(new EditorLookScope(look));
                        report.AppendLine($"✓ カラー (ポスト) セットアップ [{RenderPipelineDetector.DisplayName}]");
                    }
                    else report.AppendLine($"⚠ カラーアダプタ未検出 ({RenderPipelineDetector.DisplayName}) — ポストはスキップ");
                }
            }

            EditorUtility.SetDirty(look);
            report.AppendLine("\n→ 仕上げに『🔥 ベイク』を実行してください。");
            return report.ToString();
        }

        // ---- Bake presets (spec §4.1) ----

        public static void ApplyBakePreset(LookDefinition look, bool highQuality)
        {
            var l = look.lighting;
            if (highQuality)
            {
                l.lightmapResolution = 40f; l.atlasSize = 2048;
                l.directSamples = 64; l.indirectSamples = 512; l.bounces = 3;
                l.ambientOcclusion = true; l.reflectionResolution = 256;
            }
            else // mobile
            {
                l.lightmapResolution = 15f; l.atlasSize = 1024;
                l.directSamples = 32; l.indirectSamples = 256; l.bounces = 2;
                l.ambientOcclusion = true; l.reflectionResolution = 128;
            }
            EditorUtility.SetDirty(look);
        }
    }
}
