using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Boss.LookDev.Runtime
{
    /// <summary>
    /// (R) SelfApp runtime component placed on each state object (VR_State /
    /// MR_State). The author builds the two states under a switch root; toggling
    /// which is active (SetActive) swaps the look — engine-standard, so C# /
    /// PlayMaker / Timeline can all drive it (spec §5.1). On enable this applies
    /// its state's delta, optionally blended over a transition.
    ///
    /// Shared bake (spec §5.1): only runtime-changeable params are touched here —
    /// camera clear flags (skybox vs passthrough), fog, ambient, reflection.
    /// Per-state color-grade deltas are a later refinement (would need a runtime
    /// color adapter); the shared color section is baked at author time.
    /// </summary>
    [AddComponentMenu("BOSS/Look Dev/Look Dev State")]
    public class BossLookDevState : MonoBehaviour
    {
        [Tooltip("The look this state belongs to (shared bake, per-state delta).")]
        public LookDefinition look;

        [Tooltip("false = state A (e.g. VR), true = state B (e.g. MR).")]
        public bool isSecondaryState;

        [Tooltip("Camera to drive (clear flags). Defaults to Camera.main.")]
        public Camera targetCamera;

        private StateDelta Delta =>
            look == null ? null : (isSecondaryState ? look.states.stateB.delta : look.states.stateA.delta);

        private void OnEnable()
        {
            var d = Delta;
            if (d == null) return;
            if (Application.isPlaying && look.states.transition.smooth)
                StartCoroutine(Blend(d, look.states.transition));
            else
                ApplyImmediate(d);
        }

        private void ApplyImmediate(StateDelta d)
        {
            var cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam != null)
                cam.clearFlags = d.skyboxVisible ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            RenderSettings.fog = d.fogEnabled;
            RenderSettings.ambientIntensity = d.ambientIntensity;
            RenderSettings.reflectionIntensity = Mathf.Clamp01(d.reflectionIntensity);
        }

        private IEnumerator Blend(StateDelta d, TransitionSettings tr)
        {
            var cam = targetCamera != null ? targetCamera : Camera.main;
            // Discrete switches apply at the start of the blend.
            if (cam != null)
                cam.clearFlags = d.skyboxVisible ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            RenderSettings.fog = d.fogEnabled;

            float startAmbient = RenderSettings.ambientIntensity;
            float startRefl = RenderSettings.reflectionIntensity;
            float targetRefl = Mathf.Clamp01(d.reflectionIntensity);
            float dur = Mathf.Max(0.0001f, tr.durationSeconds);
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = tr.curve.Evaluate(Mathf.Clamp01(t / dur));
                RenderSettings.ambientIntensity = Mathf.Lerp(startAmbient, d.ambientIntensity, k);
                RenderSettings.reflectionIntensity = Mathf.Lerp(startRefl, targetRefl, k);
                yield return null;
            }
            ApplyImmediate(d);
        }
    }
}
