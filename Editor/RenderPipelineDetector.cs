using UnityEngine.Rendering;
#if BOSS_LOOK_DEV_HAS_URP
using UnityEngine.Rendering.Universal;
#endif

namespace Boss.LookDev.Editor
{
    /// <summary>Detects the host project's active render pipeline so the
    /// AdapterRegistry can pick the matching color/material backend (spec §7).
    /// Built-in is reported when no SRP asset is assigned.</summary>
    public static class RenderPipelineDetector
    {
        public static LookPipeline Active
        {
            get
            {
                var asset = GraphicsSettings.defaultRenderPipeline;
                if (asset == null) return LookPipeline.BuiltIn;
#if BOSS_LOOK_DEV_HAS_URP
                if (asset is UniversalRenderPipelineAsset) return LookPipeline.URP;
#endif
                // String fallback so a URP without the versionDefine still resolves.
                var typeName = asset.GetType().FullName ?? string.Empty;
                if (typeName.Contains("Universal")) return LookPipeline.URP;
                // HDRP / unknown SRP: not supported (spec §9). Treat as Built-in
                // for detection; the validator warns separately.
                return LookPipeline.BuiltIn;
            }
        }

        public static bool IsHdrpOrUnknown
        {
            get
            {
                var asset = GraphicsSettings.defaultRenderPipeline;
                if (asset == null) return false;
                var typeName = asset.GetType().FullName ?? string.Empty;
                return typeName.Contains("HDRender") ||
                       (!typeName.Contains("Universal") && asset != null);
            }
        }

        public static string DisplayName
        {
            get
            {
                var asset = GraphicsSettings.defaultRenderPipeline;
                if (asset == null) return "Built-in RP";
                var typeName = asset.GetType().FullName ?? string.Empty;
                if (typeName.Contains("Universal")) return "Universal RP (URP)";
                if (typeName.Contains("HDRender")) return "High Definition RP (HDRP — not supported)";
                return "Custom SRP (not supported)";
            }
        }
    }
}
