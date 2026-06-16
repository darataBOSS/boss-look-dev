namespace Boss.LookDev.Editor
{
    /// <summary>Editor-side <see cref="ILookContextScope"/>: the apply context
    /// passed to adapters/modules during authoring. Pipeline defaults to the
    /// detected active pipeline; the look's targetPipeline is compared against it
    /// for the mismatch warning (spec §3.3).</summary>
    public sealed class EditorLookScope : ILookContextScope
    {
        public LookDefinition Look { get; }
        public LookPipeline ActivePipeline { get; }
        public LookTarget Target { get; }

        public EditorLookScope(LookDefinition look)
        {
            Look = look;
            ActivePipeline = RenderPipelineDetector.Active;
            Target = look != null ? look.target : LookTarget.SelfApp;
        }
    }
}
