using System;

namespace Boss.LookDev
{
    /// <summary>Where a look is being applied. Kept as an opaque marker so Core
    /// stays free of UnityEditor / pipeline references; adapters and modules in
    /// the outer asmdefs supply the concrete scene/asset access they need.</summary>
    public interface ILookContextScope
    {
        LookDefinition Look { get; }
        LookPipeline ActivePipeline { get; }
        LookTarget Target { get; }
    }

    // ----- (C) Pipeline adapter layer -----

    /// <summary>Absorbs the pipeline-specific part of (B). Color: PPv2 (Built-in)
    /// vs URP Volume. lighting/bake/IBL/probe need no adapter — common core
    /// (spec §3.4). The Editor's AdapterRegistry selects by active pipeline.</summary>
    public interface IColorAdapter
    {
        LookPipeline Pipeline { get; }
        void Setup(ILookContextScope scope);
        void Apply(ILookContextScope scope);
        void Remove(ILookContextScope scope);
    }

    /// <summary>Material adapter: Standard (Built-in) vs URP-Lit / Shader Graph.
    /// Property overrides and uplift differ per pipeline (spec §3.3, §7).
    /// Returns the number of materials touched.</summary>
    public interface IMaterialAdapter
    {
        LookPipeline Pipeline { get; }
        int NormalizePbr(ILookContextScope scope);
    }

    // ----- (B) Look module (registered lever) -----

    /// <summary>An independent look lever (lighting / color / atmosphere /
    /// overrides). Registered, so new levers are added without touching the core
    /// (spec §3.4). AR-only modules (e.g. ground shadow) gate on context.</summary>
    public interface ILookModule
    {
        string Id { get; }
        LookLayer Layer { get; }
        bool SupportsContext(LookContext context);
        void Apply(ILookContextScope scope);
    }

    // ----- (D) Resolver layer -----

    /// <summary>The state a resolver currently selects.</summary>
    public readonly struct LookSelection
    {
        public readonly ContextStatePair State;
        public LookSelection(ContextStatePair state) { State = state; }
    }

    /// <summary>Decides which preset/state to use or blend. Swappable: v1 has a
    /// manual resolver plus the VR↔MR resolver; location-based is a later add on
    /// the same seam (spec §3.4 D, §6).</summary>
    public interface ILookResolver
    {
        LookSelection Resolve(LookDefinition look);
    }

    // ----- (R) Runtime seam: context-state observation -----

    /// <summary>Observes the current runtime context-state (e.g. which of the
    /// VR/MR state objects is active). The seam the VR↔MR resolver rides.</summary>
    public interface IContextStateProvider
    {
        LookContext Current { get; }
        event Action<LookContext> Changed;
    }

    /// <summary>Vendor-specific passthrough-state observation (PICO / Meta), guarded
    /// by SDK version defines. Observation only — never controls passthrough; the
    /// look follows it (spec §5.1). v1 ships PICO as an optional SelfApp seam;
    /// Meta is v1.x.</summary>
    public interface IPassthroughStateProvider : IContextStateProvider
    {
        bool IsPassthroughActive { get; }
    }
}
