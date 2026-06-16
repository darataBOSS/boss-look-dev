using UnityEngine;

namespace Boss.LookDev
{
    /// <summary>
    /// (A) The serializable look definition — the data the whole tool revolves
    /// around. Lighting is the foundation; color / atmosphere / overrides are
    /// optional layers on top (spec §3.1, §3.4). Metadata records the target
    /// context / pipeline / delivery target; presets are NOT portable across
    /// pipelines (spec §3.3) so a mismatch only warns.
    ///
    /// This is the phase-2 skeleton. Section contents are filled out as legacy
    /// logic is ported (docs/legacy-inventory.md).
    /// </summary>
    [CreateAssetMenu(menuName = "BOSS/Look Dev/Look Definition", fileName = "NewLook")]
    public class LookDefinition : ScriptableObject
    {
        [Header("Metadata")]
        public string lookName = "Look";
        [TextArea] public string description;
        public LookContext targetContext = LookContext.VR;
        public LookPipeline targetPipeline = LookPipeline.URP;
        public LookTarget target = LookTarget.SelfApp;

        [Tooltip("Reserved for location-based presets (spec §6). Unused in v1.")]
        public string locationKey;

        [Header("Layers (lighting is the foundation)")]
        public LightingSection lighting = new LightingSection();
        public ColorSection color = new ColorSection();
        public AtmosphereSection atmosphere = new AtmosphereSection();
        public BackgroundSection background = new BackgroundSection();
        public GroundShadowSection groundShadow = new GroundShadowSection();
        public OverridesSection overrides = new OverridesSection();

        [Header("Runtime context states (VR↔MR — spec §5.1)")]
        public ContextStateSet states = new ContextStateSet();

        /// <summary>True when this look's target pipeline differs from the one
        /// passed in (host project's active pipeline). Callers warn only — there
        /// is no translator (spec §3.3).</summary>
        public bool IsPipelineMismatch(LookPipeline active) => targetPipeline != active;
    }
}
