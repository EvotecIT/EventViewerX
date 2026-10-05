namespace PSEventViewer;

/// <summary>Compares detection pack content or previews changes against bounded historical observations.</summary>
[Cmdlet(VerbsData.Compare, "EVXDetectionPack")]
[OutputType(typeof(EventDetectionPackComparison))]
[OutputType(typeof(EventDetectionImpactPreview))]
public sealed class CmdletCompareEVXDetectionPack : AsyncPSCmdlet {
    private readonly List<EventObservation> _observations = new();
    /// <para>Original version of the pack.</para>
    [Parameter(Mandatory = true)]
    public EventDetectionPack Previous { get; set; } = null!;
    /// <para>Proposed version of the pack.</para>
    [Parameter(Mandatory = true)]
    public EventDetectionPack Current { get; set; } = null!;
    /// <para>Evaluates both versions against the same historical sample instead of comparing definitions only.</para>
    [Parameter]
    public SwitchParameter Historical { get; set; }
    /// <para>Canonical historical observation to compare.</para>
    [Parameter(ValueFromPipeline = true)]
    public EventObservation? InputObject { get; set; }
    /// <para>Maximum historical observations retained for comparison.</para>
    [Parameter]
    [ValidateRange(1, int.MaxValue - 1)]
    public int MaximumObservations { get; set; } = 10000;
    /// <para>Maximum findings retained from each plan.</para>
    [Parameter]
    [ValidateRange(1, int.MaxValue - 1)]
    public int MaximumFindings { get; set; } = 100000;
    /// <para>Coverage and evaluator bounds shared by both versions.</para>
    [Parameter]
    public EventDetectionEngineOptions? Options { get; set; }
    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        if (InputObject != null) {
            if (!Historical) { throw new PSArgumentException("Historical is required when supplying observations."); }
            if (_observations.Count <= MaximumObservations) { _observations.Add(InputObject); }
        }
        return Task.CompletedTask;
    }
    /// <inheritdoc />
    protected override Task EndProcessingAsync() {
        WriteObject(Historical ? EventDetectionEngine.PreviewChanges(_observations,
            EventDetectionPlan.Compile(Previous.GetRules()), EventDetectionPlan.Compile(Current.GetRules()),
            Options, MaximumObservations, MaximumFindings, CancelToken) : Previous.CompareTo(Current), false);
        return Task.CompletedTask;
    }
}
