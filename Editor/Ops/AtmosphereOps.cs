using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Boss.LookDev.Editor.Ops
{
    /// <summary>
    /// (B) Fog / distance-haze. Distance fog is the "遠くが霞む" gradient (near clear
    /// → far haze color). Auto-matching the fog color to the HDRI/environment makes
    /// the haze blend with the water so the far backdrop fades seamlessly.
    /// </summary>
    public static class AtmosphereOps
    {
        public static void Apply(LookDefinition look)
        {
            var a = look.atmosphere;
            RenderSettings.fog = a.enabled;
            RenderSettings.fogMode = a.fogMode;
            RenderSettings.fogColor = a.fogColor;
            RenderSettings.fogDensity = a.fogDensity;
            RenderSettings.fogStartDistance = a.fogStartDistance;
            RenderSettings.fogEndDistance = a.fogEndDistance;
        }

        /// <summary>Samples the baked ambient probe (derived from the HDRI/skybox) at
        /// the horizon so the haze color matches the environment.</summary>
        public static Color SuggestFogColorFromEnvironment()
        {
            var probe = RenderSettings.ambientProbe;
            Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            var results = new Color[dirs.Length];
            probe.Evaluate(dirs, results);
            Color sum = Color.black;
            foreach (var c in results) sum += c;
            Color avg = sum / dirs.Length;
            avg.a = 1f;
            float max = Mathf.Max(avg.r, avg.g, avg.b); // ambient is HDR; clamp to displayable
            if (max > 1f) avg = new Color(avg.r / max, avg.g / max, avg.b / max, 1f);
            return avg;
        }

        /// <summary>Sets the look's fog color from the environment and keeps the
        /// skybox haze tint in sync, then applies fog to the scene.</summary>
        public static void AutoFogColorFromEnvironment(LookDefinition look)
        {
            look.atmosphere.fogColor = SuggestFogColorFromEnvironment();
            EditorUtility.SetDirty(look);
            Apply(look);
            LightingBakeOps.ApplySkyboxHaze(look); // haze tint follows the fog color
            DynamicGI.UpdateEnvironment();
        }
    }
}
