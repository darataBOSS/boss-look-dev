using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Boss.LookDev.Editor.Ops
{
    /// <summary>
    /// (B) The STYLY color path (spec §3.5, inventory C7/G4): pushes the look's
    /// color intent into the environment (skybox tint/exposure → ambient +
    /// reflections) and ambient intensity. This survives Gamma and a dropped post
    /// stack because it lives in standard scene/lighting data, not a camera effect
    /// — the only reliable way to tint a STYLY-mobile look. Ported from legacy
    /// LookBakeOps. Idempotent: derived from the look's own values, never
    /// multiplied onto current state.
    /// </summary>
    public static class LookBakeOps
    {
        public static void ApplyToLighting(LookDefinition look)
        {
            if (look == null) return;
            var color = look.color;
            var lighting = look.lighting;

            Color tint = MoodColor(color.temperature, color.tint, color.colorFilter);

            var mat = RenderSettings.skybox;
            if (mat != null)
            {
                if (mat.HasProperty("_Tint"))
                {
                    Color t = tint * 0.5f; t.a = 1f; // skybox treats 0.5 grey as neutral
                    mat.SetColor("_Tint", ClampColor01(t));
                }
                if (mat.HasProperty("_Exposure"))
                {
                    float exp = lighting.skyboxExposure * Mathf.Pow(2f, color.exposure * 0.5f);
                    mat.SetFloat("_Exposure", Mathf.Clamp(exp, 0f, 8f));
                }
                EditorUtility.SetDirty(mat);
            }

            RenderSettings.ambientIntensity = Mathf.Clamp(
                lighting.environmentIntensity * Mathf.Pow(2f, color.exposure * 0.35f), 0f, 8f);
            RenderSettings.reflectionIntensity = Mathf.Clamp01(lighting.environmentIntensity);
            DynamicGI.UpdateEnvironment();
        }

        public static void ResetLightingTint(LookDefinition look)
        {
            if (look == null) return;
            var mat = RenderSettings.skybox;
            if (mat != null)
            {
                if (mat.HasProperty("_Tint")) mat.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 1f));
                if (mat.HasProperty("_Exposure")) mat.SetFloat("_Exposure", look.lighting.skyboxExposure);
                EditorUtility.SetDirty(mat);
            }
            RenderSettings.ambientIntensity = look.lighting.environmentIntensity;
            DynamicGI.UpdateEnvironment();
        }

        /// <summary>White-balance-style intent → RGB multiplier centered on white.</summary>
        private static Color MoodColor(float temperature, float tint, Color colorFilter)
        {
            float t = Mathf.Clamp(temperature / 100f, -1f, 1f);
            Color warm = new Color(1f + 0.15f * t, 1f, 1f - 0.15f * t, 1f);
            float g = Mathf.Clamp(tint / 100f, -1f, 1f);
            Color tn = new Color(1f + 0.10f * g, 1f - 0.10f * g, 1f + 0.10f * g, 1f);
            return new Color(warm.r * tn.r * colorFilter.r, warm.g * tn.g * colorFilter.g, warm.b * tn.b * colorFilter.b, 1f);
        }

        private static Color ClampColor01(Color c) =>
            new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), Mathf.Clamp01(c.a));
    }
}
