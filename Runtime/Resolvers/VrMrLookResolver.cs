namespace Boss.LookDev.Runtime
{
    /// <summary>(D) VR↔MR resolver: maps the observed runtime context (from an
    /// <see cref="IContextStateProvider"/>, optionally a vendor passthrough
    /// provider) to the matching state. Observation only — it follows passthrough,
    /// never controls it (spec §5.1). Location-based resolvers ride the same seam.
    ///
    /// Phase-2 skeleton: wiring is present; the provider is supplied by the
    /// SelfApp runtime (or a Pico/Meta vendor adapter) in phase 5.
    /// </summary>
    public sealed class VrMrLookResolver : ILookResolver
    {
        private readonly IContextStateProvider _provider;

        public VrMrLookResolver(IContextStateProvider provider)
        {
            _provider = provider;
        }

        public LookSelection Resolve(LookDefinition look)
        {
            var current = _provider != null ? _provider.Current : look.states.stateA.context;
            var pair = look.states.stateB.context == current ? look.states.stateB : look.states.stateA;
            return new LookSelection(pair);
        }
    }
}
