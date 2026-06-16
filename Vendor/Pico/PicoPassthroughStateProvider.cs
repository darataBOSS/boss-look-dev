// PICO passthrough-state observation (spec §5.1, §8-5) — optional SelfApp
// auto-follow. Observation only; never controls passthrough. Empty seam in v1:
// compiles only when the PICO SDK is present, and even then is a skeleton until
// wired to the SDK's passthrough state.
//
// TODO: confirm the actual PICO SDK package name for the versionDefine in the
// asmdef (placeholder: com.unity.xr.picoxr) and add the SDK assembly reference
// when implementing.
#if BOSS_LOOK_DEV_HAS_PICO
using System;

namespace Boss.LookDev.Pico
{
    public sealed class PicoPassthroughStateProvider : IPassthroughStateProvider
    {
        public bool IsPassthroughActive => false;          // TODO: read from PICO SDK
        public LookContext Current => LookContext.VR;       // TODO: derive from passthrough
        public event Action<LookContext> Changed;

        // Suppress unused-event warning until wired up.
        private void Raise(LookContext c) => Changed?.Invoke(c);
    }
}
#endif
