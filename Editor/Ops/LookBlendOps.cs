using UnityEditor;
using UnityEngine;

namespace Boss.LookDev.Editor.Ops
{
    /// <summary>
    /// (B/D) Look-feel blending and snapshots (spec §4.2, §4.4). Blends only the
    /// purely-visual "feel" params (color grade, fog, environment exposure, rig
    /// mood) between two looks — never structural bake data. Snapshots let the
    /// author save a good state and get back to it (before/after, history).
    /// </summary>
    public static class LookBlendOps
    {
        /// <summary>Lerp the feel of A→B into target at t (0..1). Structural fields
        /// (probes, atlas, rig type) are taken from target as-is.</summary>
        public static void BlendInto(LookDefinition target, LookDefinition a, LookDefinition b, float t)
        {
            if (target == null || a == null || b == null) return;
            t = Mathf.Clamp01(t);
            Undo.RecordObject(target, "Blend Looks");

            // Color
            var tc = target.color; var ac = a.color; var bc = b.color;
            tc.enabled = t < 0.5f ? ac.enabled : bc.enabled;
            tc.exposure = Mathf.Lerp(ac.exposure, bc.exposure, t);
            tc.contrast = Mathf.Lerp(ac.contrast, bc.contrast, t);
            tc.saturation = Mathf.Lerp(ac.saturation, bc.saturation, t);
            tc.colorFilter = Color.Lerp(ac.colorFilter, bc.colorFilter, t);
            tc.temperature = Mathf.Lerp(ac.temperature, bc.temperature, t);
            tc.tint = Mathf.Lerp(ac.tint, bc.tint, t);
            tc.bloom = t < 0.5f ? ac.bloom : bc.bloom;
            tc.bloomIntensity = Mathf.Lerp(ac.bloomIntensity, bc.bloomIntensity, t);
            tc.vignette = t < 0.5f ? ac.vignette : bc.vignette;
            tc.vignetteIntensity = Mathf.Lerp(ac.vignetteIntensity, bc.vignetteIntensity, t);

            // Atmosphere
            var ta = target.atmosphere; var aa = a.atmosphere; var ba = b.atmosphere;
            ta.enabled = t < 0.5f ? aa.enabled : ba.enabled;
            ta.fogColor = Color.Lerp(aa.fogColor, ba.fogColor, t);
            ta.fogDensity = Mathf.Lerp(aa.fogDensity, ba.fogDensity, t);

            // Lighting feel (not structure)
            target.lighting.environmentIntensity = Mathf.Lerp(a.lighting.environmentIntensity, b.lighting.environmentIntensity, t);
            target.lighting.skyboxExposure = Mathf.Lerp(a.lighting.skyboxExposure, b.lighting.skyboxExposure, t);

            // Rig mood
            target.lighting.rig.keyFillRatio = Mathf.Lerp(a.lighting.rig.keyFillRatio, b.lighting.rig.keyFillRatio, t);
            target.lighting.rig.skyFillRatio = Mathf.Lerp(a.lighting.rig.skyFillRatio, b.lighting.rig.skyFillRatio, t);

            EditorUtility.SetDirty(target);
        }

        // ---- Snapshot / history (before/after) ----

        /// <summary>Serializes the look's full state to JSON for restore.</summary>
        public static string Capture(LookDefinition look) =>
            look == null ? null : EditorJsonUtility.ToJson(look);

        public static void Restore(LookDefinition look, string json)
        {
            if (look == null || string.IsNullOrEmpty(json)) return;
            Undo.RecordObject(look, "Restore Look Snapshot");
            EditorJsonUtility.FromJsonOverwrite(json, look);
            EditorUtility.SetDirty(look);
        }
    }
}
