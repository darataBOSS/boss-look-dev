using System;
using UnityEngine;

namespace Boss.LookDev
{
    // The look is built lighting-first (spec §3.1): lighting is the foundation,
    // the other sections are optional layers stacked on top. Each optional section
    // carries its own `enabled` flag so a look can omit it.

    /// <summary>Light rig parameters (spec §4.1). Subject is resolved at apply
    /// time (selection / scene center) rather than stored, since asset→scene refs
    /// don't survive reload.</summary>
    [Serializable]
    public class LightRigConfig
    {
        public RigType rigType = RigType.ThreePoint;

        [Header("Three-point")]
        public RigLightKind rigLightKind = RigLightKind.Spot;
        public float keyIntensity = 2f;
        [Range(1f, 8f)] public float keyFillRatio = 2f;
        public float keyKelvin = 5600f;
        public float fillKelvin = 6500f;
        public float backKelvin = 5000f;
        [Range(1f, 179f)] public float spotAngle = 60f;
        public Vector2 areaSize = new Vector2(2f, 2f);

        [Header("Sun (outdoor)")]
        [Range(0f, 90f)] public float sunElevation = 50f;
        [Range(0f, 360f)] public float sunAzimuth = 30f;
        public float sunIntensity = 1.2f;
        public float sunKelvin = 5500f;
        public bool skyFill = true;
        [Range(2f, 10f)] public float skyFillRatio = 4f;
        public float skyFillKelvin = 9000f;

        [Header("Ceiling grid (large interior — uses probe area as ceiling)")]
        public GridLightKind gridLightKind = GridLightKind.Spot;
        [Range(1, 8)] public int gridRows = 2;
        [Range(1, 8)] public int gridColumns = 3;
        public float gridIntensity = 1.5f;
        public float gridKelvin = 4500f;
        [Range(1f, 179f)] public float gridSpotAngle = 100f;
    }

    /// <summary>Foundation layer (spec §3.1, §4.0): full bake + HDRI/IBL + probes
    /// + light rig. Pipeline-agnostic — realized by the common core, no adapter.</summary>
    [Serializable]
    public class LightingSection
    {
        [Header("Bake (Progressive Lightmapper)")]
        public BakeBackend bakeBackend = BakeBackend.ProgressiveGPU;
        public float lightmapResolution = 20f;
        public int atlasSize = 1024;
        public int directSamples = 32;
        public int indirectSamples = 256;
        [Range(0, 4)] public int bounces = 2;
        public bool ambientOcclusion = true;
        public bool compressLightmaps = true;

        [Header("Environment / HDRI / IBL (VR foundation)")]
        public Texture hdri;
        [Range(0f, 8f)] public float environmentIntensity = 1f;
        [Range(0f, 360f)] public float skyboxRotation = 0f;
        [Range(0f, 8f)] public float skyboxExposure = 1f;

        [Header("Light probes")]
        public Bounds probeArea = new Bounds(Vector3.zero, new Vector3(10f, 4f, 10f));
        public float probeSpacing = 2f;
        [Range(1, 6)] public int verticalLayers = 2;

        [Header("Reflection probe")]
        public ReflectionBakeMode reflectionMode = ReflectionBakeMode.UseHDRI;
        public int reflectionResolution = 128;
        public float reflectionPadding = 0.5f;

        [Header("Light rig")]
        public LightRigConfig rig = new LightRigConfig();
    }

    /// <summary>Color layer (spec §4.0). Pipeline-specific: realized by an
    /// IColorAdapter (PPv2 / URP Volume). For STYLY, post is delegated to the
    /// STYLY side — only <see cref="bakeIntoLighting"/> is emitted there
    /// (legacy LookBakeOps, inventory C7/G4).</summary>
    [Serializable]
    public class ColorSection
    {
        public bool enabled = true;

        [Header("Grade")]
        [Range(-3f, 3f)] public float exposure = 0f;
        [Range(-100f, 100f)] public float contrast = 0f;
        [Range(-100f, 100f)] public float saturation = 0f;
        public Color colorFilter = Color.white;
        [Range(-100f, 100f)] public float temperature = 0f;
        [Range(-100f, 100f)] public float tint = 0f;

        [Header("Effects")]
        public bool bloom = true;
        [Range(0f, 2f)] public float bloomIntensity = 0.4f;
        public bool vignette = true;
        [Range(0f, 1f)] public float vignetteIntensity = 0.25f;

        [Header("Advanced (詳細) — SelfApp post only")]
        public float bloomThreshold = 1.1f;
        [Range(0f, 1f)] public float bloomScatter = 0.7f;
        public Color bloomTint = Color.white;
        [Range(0f, 1f)] public float vignetteSmoothness = 0.5f;
        [Range(0f, 1f)] public float vignetteRoundness = 1f;
        public Color vignetteColor = Color.black;
        [Range(-180f, 180f)] public float hueShift = 0f;
        public TonemapMode tonemap = TonemapMode.ACES;
        [Tooltip("Color lookup (LUT). URP: always; PPv2: LDR/STYLY path only.")]
        public Texture lut;
        [Range(0f, 1f)] public float lutContribution = 1f;

        [Header("STYLY / Gamma path")]
        [Tooltip("Bake the color intent into lighting/environment so it survives a dropped post stack (STYLY mobile).")]
        public bool bakeIntoLighting = false;
    }

    /// <summary>Atmosphere layer: distance fog (spec §5.1 — VR on / MR usually off).</summary>
    [Serializable]
    public class AtmosphereSection
    {
        public bool enabled = false;
        public FogMode fogMode = FogMode.ExponentialSquared;
        public Color fogColor = new Color(0.6f, 0.65f, 0.7f, 1f);
        public float fogDensity = 0.02f;
        public float fogStartDistance = 10f;
        public float fogEndDistance = 60f;
    }

    /// <summary>AR ground shadow (spec inventory D1, hold (d)): a transparent
    /// shadow-catcher so AR content doesn't look like it's floating. AR context
    /// only — not used in MR (occlusion handles contact there).</summary>
    [Serializable]
    public class GroundShadowSection
    {
        public bool enabled = false;
        [Range(0f, 1f)] public float opacity = 0.6f;
        [Range(1f, 6f)] public float sizeMultiplier = 2f;
    }

    /// <summary>Material overrides layer. Reserved seam in v1 (spec §6) — boundary
    /// only, no implementation.</summary>
    [Serializable]
    public class OverridesSection
    {
        public bool enabled = false;
        // Material override entries are added in a later version.
    }
}
