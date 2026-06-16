using Boss.LookDev.Editor.Adapters;

namespace Boss.LookDev.Editor
{
    /// <summary>(C) Selects the color/material adapter that matches a pipeline
    /// (spec §3.4, §7). Editor-authoring adapters live in this asmdef (guarded) to
    /// avoid a circular dependency with the editor ops; runtime adapters live in
    /// the per-pipeline asmdefs. Returns null when the backend is unavailable —
    /// callers warn.</summary>
    public static class AdapterRegistry
    {
        public static IColorAdapter GetColorAdapter(LookPipeline pipeline)
        {
            switch (pipeline)
            {
                case LookPipeline.URP:
#if BOSS_LOOK_DEV_HAS_URP && BOSS_LOOK_DEV_HAS_SRPCORE
                    return new UrpColorAuthoringAdapter();
#else
                    return null;
#endif
                case LookPipeline.BuiltIn:
#if BOSS_LOOK_DEV_HAS_PPV2
                    return new BuiltInColorAuthoringAdapter();
#else
                    return null;
#endif
                default:
                    return null;
            }
        }

        public static IColorAdapter GetActiveColorAdapter() =>
            GetColorAdapter(RenderPipelineDetector.Active);

        public static IMaterialAdapter GetMaterialAdapter(LookPipeline pipeline)
        {
            switch (pipeline)
            {
                case LookPipeline.URP:
#if BOSS_LOOK_DEV_HAS_URP && BOSS_LOOK_DEV_HAS_SRPCORE
                    return new UrpMaterialAuthoringAdapter();
#else
                    return null;
#endif
                case LookPipeline.BuiltIn:
                    return new BuiltInMaterialAuthoringAdapter();
                default:
                    return null;
            }
        }

        public static IMaterialAdapter GetActiveMaterialAdapter() =>
            GetMaterialAdapter(RenderPipelineDetector.Active);
    }
}
