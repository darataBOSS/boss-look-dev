namespace Boss.LookDev
{
    /// <summary>Light rig archetype (spec §4.1 lighting rig). ThreePoint (subject
    /// studio key/fill/back), Sun (outdoor directional + sky fill), CeilingGrid
    /// (rows of downward lights covering the probe area for large interiors).</summary>
    public enum RigType
    {
        ThreePoint = 0,
        Sun = 1,
        CeilingGrid = 2,
    }

    /// <summary>Fixture kind for the three-point rig.</summary>
    public enum RigLightKind
    {
        Spot = 0,
        Area = 1,
    }

    /// <summary>Fixture kind for the ceiling grid rig.</summary>
    public enum GridLightKind
    {
        Spot = 0,
        Point = 1,
        Area = 2,
    }

    /// <summary>How the reflection probe treats the sky when baking.</summary>
    public enum ReflectionBakeMode
    {
        UseHDRI = 0,        // bake the HDRI sky into reflections
        NeutralReplace = 1, // neutral grey probe background (sky still feeds GI)
    }

    /// <summary>Bake backend. Mapped to UnityEditor's LightingSettings.Lightmapper
    /// in the editor ops (Core stays free of UnityEditor).</summary>
    public enum BakeBackend
    {
        ProgressiveGPU = 0,
        ProgressiveCPU = 1,
    }
}
