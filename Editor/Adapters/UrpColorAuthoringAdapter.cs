// URP color adapter (editor authoring). Lives in the Editor asmdef (guarded) to
// avoid a circular dependency with SceneBindingOps; the runtime apply adapter
// will live in Boss.LookDev.URP later. Compiles only when URP is present.
#if BOSS_LOOK_DEV_HAS_URP && BOSS_LOOK_DEV_HAS_SRPCORE
using Boss.LookDev.Editor.Ops;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Boss.LookDev.Editor.Adapters
{
    /// <summary>(C) URP/Volume color adapter. Creates a global Volume + profile and
    /// configures Tonemapping + ColorAdjustments + WhiteBalance + Bloom + Vignette
    /// from the look's ColorSection. Ported from legacy PostProcessOpsURP, incl. the
    /// sub-asset persistence fix (AddObjectToAsset guarded by IsPersistent).</summary>
    public sealed class UrpColorAuthoringAdapter : IColorAdapter
    {
        public LookPipeline Pipeline => LookPipeline.URP;

        public void Setup(ILookContextScope scope)
        {
            var look = scope.Look;
            if (look == null) return;

            string folder = SceneBindingOps.AssetFolder(look);
            string profilePath = $"{folder}/{look.lookName}_VolumeProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            ApplyEffects(profile, look.color);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            string volumeName = SceneBindingOps.VolumeName(look);
            var volume = SceneBindingOps.FindComponentInScene<Volume>(volumeName);
            if (volume == null)
            {
                var go = new GameObject(volumeName);
                Undo.RegisterCreatedObjectUndo(go, "Create URP Volume");
                volume = go.AddComponent<Volume>();
            }
            volume.isGlobal = true;
            volume.weight = 1f;
            volume.priority = 0f;
            volume.sharedProfile = profile;
            SceneBindingOps.Parent(volume.gameObject, look);
            EditorUtility.SetDirty(volume);

            var cam = Camera.main;
            if (cam != null)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null && !data.renderPostProcessing)
                {
                    Undo.RecordObject(data, "Enable URP Post Processing");
                    data.renderPostProcessing = true;
                    EditorUtility.SetDirty(data);
                }
            }
            else
            {
                Debug.LogWarning("[BOSS Look Dev] MainCamera が見つかりません。Camera の Post Processing を手動で ON にしてください。");
            }
        }

        public void Apply(ILookContextScope scope)
        {
            var look = scope.Look;
            if (look == null) return;
            string folder = SceneBindingOps.AssetFolder(look);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>($"{folder}/{look.lookName}_VolumeProfile.asset");
            if (profile == null) { Setup(scope); return; }
            ApplyEffects(profile, look.color);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        public void Remove(ILookContextScope scope)
        {
            var look = scope.Look;
            if (look == null) return;
            var volume = SceneBindingOps.FindComponentInScene<Volume>(SceneBindingOps.VolumeName(look));
            if (volume != null) Undo.DestroyObjectImmediate(volume.gameObject);
        }

        private static void ApplyEffects(VolumeProfile profile, ColorSection color)
        {
            CleanProfile(profile);

            Ensure<Bloom>(profile, color.enabled && color.bloom, b =>
            {
                b.intensity.overrideState = true; b.intensity.value = color.bloomIntensity;
                b.threshold.overrideState = true; b.threshold.value = color.bloomThreshold;
                b.scatter.overrideState = true; b.scatter.value = color.bloomScatter;
                b.tint.overrideState = true; b.tint.value = color.bloomTint;
            });
            var mode = color.tonemap == TonemapMode.None ? TonemappingMode.None
                : color.tonemap == TonemapMode.Neutral ? TonemappingMode.Neutral : TonemappingMode.ACES;
            Ensure<Tonemapping>(profile, color.enabled, t =>
            {
                t.mode.overrideState = true; t.mode.value = mode;
            });
            Ensure<ColorAdjustments>(profile, color.enabled, c =>
            {
                c.postExposure.overrideState = true; c.postExposure.value = color.exposure;
                c.contrast.overrideState = true; c.contrast.value = color.contrast;
                c.saturation.overrideState = true; c.saturation.value = color.saturation;
                c.colorFilter.overrideState = true; c.colorFilter.value = color.colorFilter;
                c.hueShift.overrideState = true; c.hueShift.value = color.hueShift;
            });
            Ensure<WhiteBalance>(profile, color.enabled, w =>
            {
                w.temperature.overrideState = true; w.temperature.value = color.temperature;
                w.tint.overrideState = true; w.tint.value = color.tint;
            });
            Ensure<Vignette>(profile, color.enabled && color.vignette, v =>
            {
                v.intensity.overrideState = true; v.intensity.value = color.vignetteIntensity;
                v.smoothness.overrideState = true; v.smoothness.value = color.vignetteSmoothness;
                v.color.overrideState = true; v.color.value = color.vignetteColor;
                // Note: URP's Vignette has no `roundness` parameter (PPv2 only).
            });
            Ensure<ColorLookup>(profile, color.enabled && color.lut != null, cl =>
            {
                cl.texture.overrideState = true; cl.texture.value = color.lut;
                cl.contribution.overrideState = true; cl.contribution.value = color.lutContribution;
            });
        }

        private static void Ensure<T>(VolumeProfile profile, bool enabled, System.Action<T> configure)
            where T : VolumeComponent
        {
            if (!profile.TryGet<T>(out var component))
                component = profile.Add<T>(overrides: true);

            // Persistence fix (legacy inventory C8): Add only touches the in-memory
            // list; without AddObjectToAsset the saved profile has no effects.
            // Guard with IsPersistent (+ try/catch) to avoid the double-add throw
            // after reimport.
            if (AssetDatabase.Contains(profile) && !EditorUtility.IsPersistent(component))
            {
                component.hideFlags = HideFlags.HideInHierarchy;
                try { AssetDatabase.AddObjectToAsset(component, profile); }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[BOSS Look Dev] エフェクト {typeof(T).Name} の保存に失敗: {e.Message}");
                }
            }

            component.active = enabled;
            configure?.Invoke(component);
            EditorUtility.SetDirty(component);
        }

        private static void CleanProfile(VolumeProfile profile)
        {
            if (profile.components == null) return;
            var seen = new System.Collections.Generic.HashSet<System.Type>();
            for (int i = profile.components.Count - 1; i >= 0; i--)
            {
                var c = profile.components[i];
                if (c == null) { profile.components.RemoveAt(i); continue; }
                if (!seen.Add(c.GetType())) profile.components.RemoveAt(i);
            }
            EditorUtility.SetDirty(profile);
        }
    }
}
#endif
