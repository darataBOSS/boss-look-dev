namespace Boss.LookDev
{
    /// <summary>Modality of the look. Drives what bake faces (HDRI/skybox for VR
    /// vs real-world-behind for AR/MR) and what is meaningful (skybox/IBL/fog are
    /// VR-only). See spec §3.2.</summary>
    public enum LookContext
    {
        VR = 0,
        AR = 1,
        MR = 2,
    }

    /// <summary>Render pipeline a look targets / the host project runs. Pipelines
    /// are a branch, not a ranking; presets are NOT portable across them
    /// (spec §3.3) — a mismatch only warns.</summary>
    public enum LookPipeline
    {
        BuiltIn = 0,
        URP = 1,
    }

    /// <summary>Delivery target. Decides runtime capability, rig/emitter shape,
    /// allowed components, and transition style (spec §3.2). STYLY implies
    /// Built-in + no custom C# + PPv1 + Default layer constraints.</summary>
    public enum LookTarget
    {
        SelfApp = 0, // own app (Pico/Quest etc.): editor authoring + runtime C#
        STYLY = 1,   // STYLY (PlayMaker): editor authoring + bake only, no runtime C#
    }

    /// <summary>How the camera background is rendered (spec §3.2 stylized).
    /// Skybox = HDRI/CG sky (VR). SolidColor = flat color (stylized VR e.g. deep
    /// sea, or AR uses transparent via state). Gradient = vertical gradient sky
    /// (depth feel) via a generated skybox material.</summary>
    public enum BackgroundMode
    {
        Skybox = 0,
        SolidColor = 1,
        Gradient = 2,
    }

    /// <summary>Ambient (indirect) light source. Skybox = from the skybox SH.
    /// Flat = single color. Gradient = sky/equator/ground (Unity Trilight) —
    /// great for underwater (brighter above, darker below).</summary>
    public enum LookAmbientMode
    {
        Skybox = 0,
        Flat = 1,
        Gradient = 2,
    }

    /// <summary>Tonemapping operator for the color layer (SelfApp / HDR path).
    /// STYLY's LDR/Gamma path ignores this (no tonemapper).</summary>
    public enum TonemapMode
    {
        None = 0,
        Neutral = 1,
        ACES = 2,
    }

    /// <summary>The independent look levers, layered on top of lighting
    /// (spec §3.1 / §3.4). Used for module registration and UI color-coding.</summary>
    public enum LookLayer
    {
        Lighting = 0,   // foundation (required)
        Color = 1,
        Atmosphere = 2,
        Overrides = 3,  // material overrides (reserved seam in v1)
    }
}
