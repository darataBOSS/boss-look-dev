using System;
using UnityEngine;

namespace Boss.LookDev
{
    /// <summary>The runtime-changeable delta of a single context-state. The bake
    /// (GI/lightmap) is SHARED between states and cannot change at runtime, so a
    /// state only carries what CAN change live (spec §5.1). Anything needing a
    /// different bake is deliberately not representable here.</summary>
    [Serializable]
    public class StateDelta
    {
        [Tooltip("Camera clear flags: true = Skybox (VR), false = passthrough transparent (MR / AR finalize).")]
        public bool skyboxVisible = true;

        [Tooltip("Fog on/off for this state (VR on / MR usually off).")]
        public bool fogEnabled = true;

        [Tooltip("Exposure / ambient nudge — MR matches passthrough brightness.")]
        public float exposure = 0f;
        [Range(0f, 8f)] public float ambientIntensity = 1f;

        [Tooltip("Reflection intensity for this state.")]
        [Range(0f, 1f)] public float reflectionIntensity = 1f;

        [Tooltip("Color grade delta applied on top of the shared color section.")]
        public float colorGradeExposureDelta = 0f;
    }

    /// <summary>One named context-state (e.g. VR_State / MR_State). Kept generic
    /// — NOT hard-coded to VR/MR — so future pairs (AR↔VR, ...) ride the same
    /// machine (spec §5.1, the seam). v1 ships only VR↔MR.</summary>
    [Serializable]
    public class ContextStatePair
    {
        public string stateName = "State";
        public LookContext context = LookContext.VR;
        public StateDelta delta = new StateDelta();
    }

    /// <summary>Smooth VR↔MR transition (SelfApp only in v1; STYLY is instant,
    /// smooth is v1.x — spec §5.1).</summary>
    [Serializable]
    public class TransitionSettings
    {
        public bool smooth = true;
        [Range(0f, 5f)] public float durationSeconds = 0.4f;
        public AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    /// <summary>A shared-bake pair of context-states plus its transition. The two
    /// states toggle via GameObject SetActive (spec §5.1): exactly one active at
    /// a time. Defaults are seeded as VR / MR for v1.</summary>
    [Serializable]
    public class ContextStateSet
    {
        public bool enabled = false;

        public ContextStatePair stateA = new ContextStatePair
        {
            stateName = "VR_State",
            context = LookContext.VR,
            delta = new StateDelta { skyboxVisible = true, fogEnabled = true },
        };

        public ContextStatePair stateB = new ContextStatePair
        {
            stateName = "MR_State",
            context = LookContext.MR,
            delta = new StateDelta { skyboxVisible = false, fogEnabled = false },
        };

        public TransitionSettings transition = new TransitionSettings();
    }
}
