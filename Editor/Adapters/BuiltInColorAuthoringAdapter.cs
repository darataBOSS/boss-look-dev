// Built-in RP color adapter (editor authoring), Post Processing Stack v2.
// Lives in the Editor asmdef (guarded) like the URP one. Compiles only when PPv2
// is installed, so URP-only / bare projects still build (spec §7).
//
// Note: this PPv2 path is for SelfApp on Built-in. STYLY does NOT use PPv2
// (spec §3.5) — for STYLY, color is delegated to the STYLY side plus the
// lighting bake layer (LookBakeOps); see the window / quickstart STYLY branch.
#if BOSS_LOOK_DEV_HAS_PPV2
using Boss.LookDev.Editor.Ops;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace Boss.LookDev.Editor.Adapters
{
    /// <summary>(C) Built-in/PPv2 color adapter. Creates a Profile + global
    /// PostProcessVolume + a PostProcessLayer on the main camera and configures
    /// Bloom + ColorGrading + Vignette from the look's ColorSection. STYLY target
    /// uses LDR grading (Gamma-safe); SelfApp uses HDR + ACES. Ported from legacy
    /// PostProcessOpsBuiltin incl. the sub-asset persistence fix (inventory C8).</summary>
    public sealed class BuiltInColorAuthoringAdapter : IColorAdapter
    {
        public LookPipeline Pipeline => LookPipeline.BuiltIn;
        private const string PostLayerName = "PostProcessing";
        private static string VolumeName(LookDefinition look) => $"{look.lookName} Post Volume";

        public void Setup(ILookContextScope scope)
        {
            var look = scope.Look;
            if (look == null) return;

            string folder = SceneBindingOps.AssetFolder(look);
            string profilePath = $"{folder}/{look.lookName}_PostProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<PostProcessProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            ApplyEffects(profile, look.color, scope.Target);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            string volumeName = VolumeName(look);
            var volume = SceneBindingOps.FindComponentInScene<PostProcessVolume>(volumeName);
            if (volume == null)
            {
                var go = new GameObject(volumeName);
                Undo.RegisterCreatedObjectUndo(go, "Create Post Process Volume");
                volume = go.AddComponent<PostProcessVolume>();
            }
            volume.isGlobal = true;
            volume.weight = 1f;
            volume.priority = 0f;
            volume.sharedProfile = profile;
            SceneBindingOps.Parent(volume.gameObject, look);
            EditorUtility.SetDirty(volume);

            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[BOSS Look Dev] MainCamera が見つかりません。Post Process Layer 追加をスキップ。");
            }
            else
            {
                var layer = cam.GetComponent<PostProcessLayer>() ?? Undo.AddComponent<PostProcessLayer>(cam.gameObject);
                int idx = LayerMask.NameToLayer(PostLayerName);
                if (idx < 0)
                {
                    layer.volumeLayer = 1; // Default
                }
                else
                {
                    layer.volumeLayer = 1 << idx;
                    volume.gameObject.layer = idx;
                }
                var resources = LoadDefaultPostResources();
                if (resources != null) layer.Init(resources);
                else Debug.LogWarning("[BOSS Look Dev] PostProcessResources が見つかりません。Layer を手動初期化してください。");
                EditorUtility.SetDirty(layer);
            }
        }

        public void Apply(ILookContextScope scope)
        {
            var look = scope.Look;
            if (look == null) return;
            string folder = SceneBindingOps.AssetFolder(look);
            var profile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>($"{folder}/{look.lookName}_PostProfile.asset");
            if (profile == null) { Setup(scope); return; }
            ApplyEffects(profile, look.color, scope.Target);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        public void Remove(ILookContextScope scope)
        {
            var look = scope.Look;
            if (look == null) return;
            var volume = SceneBindingOps.FindComponentInScene<PostProcessVolume>(VolumeName(look));
            if (volume != null) Undo.DestroyObjectImmediate(volume.gameObject);
            var cam = Camera.main;
            if (cam != null)
            {
                var layer = cam.GetComponent<PostProcessLayer>();
                if (layer != null) Undo.DestroyObjectImmediate(layer);
            }
        }

        private static PostProcessResources LoadDefaultPostResources()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:PostProcessResources"))
            {
                var res = AssetDatabase.LoadAssetAtPath<PostProcessResources>(AssetDatabase.GUIDToAssetPath(guid));
                if (res != null) return res;
            }
            return null;
        }

        private static void ApplyEffects(PostProcessProfile profile, ColorSection color, LookTarget target)
        {
            CleanProfile(profile);
            bool ldr = target == LookTarget.STYLY; // Gamma-safe LDR grading for STYLY

            Ensure<Bloom>(profile, color.enabled && color.bloom, e =>
            {
                e.intensity.overrideState = true; e.intensity.value = color.bloomIntensity;
                // LDR (STYLY) needs threshold near LDR white or bloom never triggers.
                e.threshold.overrideState = true;
                e.threshold.value = ldr ? Mathf.Clamp(color.bloomThreshold, 0f, 0.95f) : color.bloomThreshold;
                e.diffusion.overrideState = true; e.diffusion.value = Mathf.Lerp(1f, 10f, color.bloomScatter);
                e.color.overrideState = true; e.color.value = color.bloomTint;
            });

            var tonemapper = color.tonemap == TonemapMode.None ? Tonemapper.None
                : color.tonemap == TonemapMode.Neutral ? Tonemapper.Neutral : Tonemapper.ACES;
            Ensure<ColorGrading>(profile, color.enabled, e =>
            {
                if (ldr)
                {
                    e.gradingMode.overrideState = true; e.gradingMode.value = GradingMode.LowDefinitionRange;
                    e.brightness.overrideState = true; e.brightness.value = Mathf.Clamp(color.exposure * 20f, -100f, 100f);
                    // LUT is available in PPv2 only in the LDR path.
                    if (color.lut != null)
                    {
                        e.ldrLut.overrideState = true; e.ldrLut.value = color.lut as Texture;
                        e.ldrLutContribution.overrideState = true; e.ldrLutContribution.value = color.lutContribution;
                    }
                }
                else
                {
                    e.gradingMode.overrideState = true; e.gradingMode.value = GradingMode.HighDefinitionRange;
                    e.tonemapper.overrideState = true; e.tonemapper.value = tonemapper;
                    e.postExposure.overrideState = true; e.postExposure.value = color.exposure;
                }
                e.contrast.overrideState = true; e.contrast.value = color.contrast;
                e.saturation.overrideState = true; e.saturation.value = color.saturation;
                e.colorFilter.overrideState = true; e.colorFilter.value = color.colorFilter;
                e.temperature.overrideState = true; e.temperature.value = color.temperature;
                e.tint.overrideState = true; e.tint.value = color.tint;
                e.hueShift.overrideState = true; e.hueShift.value = color.hueShift;
            });

            Ensure<Vignette>(profile, color.enabled && color.vignette, e =>
            {
                e.intensity.overrideState = true; e.intensity.value = color.vignetteIntensity;
                e.smoothness.overrideState = true; e.smoothness.value = color.vignetteSmoothness;
                e.roundness.overrideState = true; e.roundness.value = color.vignetteRoundness;
                e.color.overrideState = true; e.color.value = color.vignetteColor;
            });
        }

        private static void Ensure<T>(PostProcessProfile profile, bool enabled, System.Action<T> configure)
            where T : PostProcessEffectSettings
        {
            T effect = profile.HasSettings<T>() ? profile.GetSetting<T>() : profile.AddSettings<T>();
            // Persistence fix (inventory C8): AddSettings only touches the in-memory
            // list; without AddObjectToAsset the saved profile ends up empty. Guard
            // with IsPersistent (+ try/catch) to avoid the double-add throw after reimport.
            if (AssetDatabase.Contains(profile) && !EditorUtility.IsPersistent(effect))
            {
                effect.hideFlags = HideFlags.HideInHierarchy;
                try { AssetDatabase.AddObjectToAsset(effect, profile); }
                catch (System.Exception e) { Debug.LogWarning($"[BOSS Look Dev] エフェクト {typeof(T).Name} 保存失敗: {e.Message}"); }
            }
            effect.enabled.overrideState = true;
            effect.enabled.value = enabled;
            configure?.Invoke(effect);
            EditorUtility.SetDirty(effect);
        }

        private static void CleanProfile(PostProcessProfile profile)
        {
            if (profile.settings == null) return;
            var seen = new System.Collections.Generic.HashSet<System.Type>();
            for (int i = profile.settings.Count - 1; i >= 0; i--)
            {
                var s = profile.settings[i];
                if (s == null) { profile.settings.RemoveAt(i); continue; }
                if (!seen.Add(s.GetType())) profile.settings.RemoveAt(i);
            }
            EditorUtility.SetDirty(profile);
        }
    }
}
#endif
