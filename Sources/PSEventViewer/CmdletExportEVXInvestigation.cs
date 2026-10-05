namespace PSEventViewer;

/// <summary>Captures a reproducible investigation with copied input evidence, canonical observations, effective rules, and verified outputs.</summary>
/// <para>Use a new directory. The manifest declares the full input window, including correlation warm-up. Existing evidence is never overwritten.</para>
[Cmdlet(VerbsData.Export, "EVXInvestigation", SupportsShouldProcess = true)]
[OutputType(typeof(EventInvestigationSession))]
public sealed class CmdletExportEVXInvestigation : AsyncPSCmdlet {
    private readonly List<EventObservation> _observations = new();
    private EventTypeProjectionPlan? _projection;
    /// <para>Canonical observation or detached event to retain.</para>
    [Parameter(Mandatory = true, ValueFromPipeline = true)]
    public object InputObject { get; set; } = null!;
    /// <para>New investigation directory.</para>
    [Parameter(Mandatory = true, Position = 0)]
    public string Path { get; set; } = string.Empty;
    /// <para>Source receipts, query identity, time range, parser versions, and execution limits.</para>
    [Parameter(Mandatory = true)]
    public EventInvestigationManifest Manifest { get; set; } = null!;
    /// <para>Effective detection plan to retain and evaluate.</para>
    [Parameter(Mandatory = true)]
    public EventDetectionPlan Plan { get; set; } = null!;
    /// <para>Original input files to copy and hash inside the session.</para>
    [Parameter]
    public string[] InputPath { get; set; } = Array.Empty<string>();
    /// <para>Expected and observed collection coverage.</para>
    [Parameter]
    public EventDetectionCoverage? Coverage { get; set; }

    /// <inheritdoc />
    protected override Task BeginProcessingAsync() {
        _projection = Plan.RequiredEventTypes.Count == 0 ? null : EventTypeCatalog.CompileProjectionPlan(Plan.RequiredEventTypes);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        if (_observations.Count >= Manifest.Limits.MaximumObservations) { throw new PSArgumentException("Investigation observation limit exceeded."); }
        object value = InputObject;
        while (value is PSObject wrapper && wrapper.BaseObject != value) { value = wrapper.BaseObject; }
        _observations.Add(value switch {
            EventObservation observation => observation,
            EventObject source => EventObservation.Create(source, _projection == null ? null : EventTypeCatalog.CreateEventRule(source, _projection)),
            EventTypeRecord typed => EventObservation.Create(typed.SourceEvent, typed),
            CustomEventRecord custom => EventObservation.FromCustomRecord(custom),
            _ => throw new PSArgumentException("InputObject must be an EventViewerX observation or event.")
        });
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override Task EndProcessingAsync() {
        string directory = SessionState.Path.GetUnresolvedProviderPathFromPSPath(Path);
        string[] inputs = InputPath.Select(SessionState.Path.GetUnresolvedProviderPathFromPSPath).ToArray();
        if (ShouldProcess(directory, "Create reproducible event investigation")) {
            WriteObject(EventInvestigationSession.Create(directory, Manifest, _observations, Plan, inputs, Coverage, CancelToken), false);
        }
        return Task.CompletedTask;
    }
}
