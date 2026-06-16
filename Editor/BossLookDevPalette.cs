using UnityEngine;

namespace Boss.LookDev.Editor
{
    /// <summary>
    /// Central color palette (spec §11): the UI must be colorful and let you
    /// identify, at a glance, layers / contexts / pipelines / inheritance verdicts
    /// / preset states. Same colorful spirit as darataBOSS's boss-PieMenu — not
    /// Unity's monotone grey. All editor UI pulls colors from here so the look
    /// stays consistent.
    /// </summary>
    public static class BossLookDevPalette
    {
        // Layers (lighting / color / atmosphere / overrides)
        public static readonly Color Lighting = new Color(1f, 0.78f, 0.35f);   // warm amber
        public static readonly Color Color_ = new Color(0.55f, 0.85f, 1f);     // sky blue
        public static readonly Color Atmosphere = new Color(0.65f, 0.9f, 0.75f); // misty green
        public static readonly Color Overrides = new Color(0.82f, 0.65f, 1f);  // violet

        // Contexts (AR / VR / MR)
        public static readonly Color VR = new Color(0.5f, 0.7f, 1f);
        public static readonly Color AR = new Color(0.55f, 0.9f, 0.6f);
        public static readonly Color MR = new Color(1f, 0.7f, 0.45f);

        // Pipelines (Built-in / URP)
        public static readonly Color BuiltIn = new Color(0.8f, 0.8f, 0.85f);
        public static readonly Color URP = new Color(0.55f, 0.85f, 0.95f);

        // Inheritance verdicts (legacy inventory: 継承=green / 落とす=red / 保留=yellow)
        public static readonly Color Inherit = new Color(0.55f, 0.9f, 0.55f);
        public static readonly Color Drop = new Color(1f, 0.5f, 0.5f);
        public static readonly Color Hold = new Color(1f, 0.88f, 0.4f);

        // Action roles (buttons)
        public static readonly Color Generate = new Color(0.6f, 0.9f, 0.6f);
        public static readonly Color Auto = new Color(0.5f, 0.78f, 1f);
        public static readonly Color Bake = new Color(1f, 0.72f, 0.35f);
        public static readonly Color Finalize = new Color(0.82f, 0.65f, 1f);
        public static readonly Color Danger = new Color(1f, 0.55f, 0.55f);

        public static Color ForLayer(LookLayer layer)
        {
            switch (layer)
            {
                case LookLayer.Lighting: return Lighting;
                case LookLayer.Color: return Color_;
                case LookLayer.Atmosphere: return Atmosphere;
                case LookLayer.Overrides: return Overrides;
                default: return Color.white;
            }
        }

        public static Color ForContext(LookContext context)
        {
            switch (context)
            {
                case LookContext.VR: return VR;
                case LookContext.AR: return AR;
                case LookContext.MR: return MR;
                default: return Color.white;
            }
        }

        public static Color ForPipeline(LookPipeline pipeline) =>
            pipeline == LookPipeline.URP ? URP : BuiltIn;
    }
}
