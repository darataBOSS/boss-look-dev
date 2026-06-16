// Meta (Quest) passthrough-state observation. v1.x non-goal (spec §9): the
// look/bake/State-toggle core already runs on Quest device-agnostically; only
// vendor passthrough auto-follow + MR occlusion are Meta-specific drop-ins.
// Empty seam — kept as a boundary only (spec §6), compiles to nothing without
// the Meta XR SDK.
//
// TODO: confirm the Meta XR SDK package name / version for the versionDefine
// (placeholder: com.meta.xr.sdk.core) and add the SDK assembly reference when
// implementing in v1.x.
#if BOSS_LOOK_DEV_HAS_META
using System;

namespace Boss.LookDev.Meta
{
    public sealed class MetaPassthroughStateProvider : IPassthroughStateProvider
    {
        public bool IsPassthroughActive => false;     // TODO: read from Meta XR SDK
        public LookContext Current => LookContext.VR;  // TODO: derive from passthrough
        public event Action<LookContext> Changed;

        private void Raise(LookContext c) => Changed?.Invoke(c);
    }
}
#endif
