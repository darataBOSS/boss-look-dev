namespace Boss.LookDev.Runtime
{
    /// <summary>(D) v1 default resolver: picks a state explicitly chosen by the
    /// author/engineer. No observation. The simplest seam implementation.</summary>
    public sealed class ManualLookResolver : ILookResolver
    {
        private bool _useStateB;

        /// <summary>Select state A (e.g. VR) or state B (e.g. MR).</summary>
        public void Select(bool useStateB) => _useStateB = useStateB;

        public LookSelection Resolve(LookDefinition look)
        {
            var pair = _useStateB ? look.states.stateB : look.states.stateA;
            return new LookSelection(pair);
        }
    }
}
