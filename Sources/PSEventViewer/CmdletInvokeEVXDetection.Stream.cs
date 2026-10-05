namespace PSEventViewer;

public sealed partial class CmdletInvokeEVXDetection {
    private EventDetectionReplaySession? _replay;
    private EventTypeProjectionPlan? _streamProjection;
    private string? _checkpointOutputPath;

    /// <summary>Evaluates ordered pipeline input incrementally without retaining and sorting the whole investigation. Do not combine with FromStore, Trace, Explain, or ReportKind.</summary>
    [Parameter]
    public SwitchParameter Stream { get; set; }
    /// <summary>Optional checkpoint to resume. Requires Stream, SourceIdentity, and RetentionIdentity.</summary>
    [Parameter]
    public string? CheckpointIn { get; set; }
    /// <summary>New checkpoint file written after successful streaming input and due AbsenceWindow decisions. Later triggers remain pending. Existing files are never replaced.</summary>
    [Parameter]
    public string? CheckpointOut { get; set; }
    /// <summary>Stable identity of the exact ordered source and query contract.</summary>
    [Parameter]
    public string? SourceIdentity { get; set; }
    /// <summary>Source generation that changes after retention, replacement, or historical correction.</summary>
    [Parameter]
    public string? RetentionIdentity { get; set; }
    /// <summary>Explicit as-of time and complete source-window receipts for absence rules.</summary>
    [Parameter]
    public EventDetectionAbsenceWindow? AbsenceWindow { get; set; }

    /// <inheritdoc />
    protected override Task BeginProcessingAsync() {
        bool checkpoint = CheckpointIn != null || CheckpointOut != null;
        if (!Stream) {
            if (checkpoint || SourceIdentity != null || RetentionIdentity != null) { throw new PSArgumentException("Checkpoint and replay identities require Stream."); }
            return Task.CompletedTask;
        }
        if (Explain || Trace || ReportKind.HasValue || !string.IsNullOrWhiteSpace(FromStore) || StartTime.HasValue || EndTime.HasValue) {
            throw new PSArgumentException("Stream accepts ordered pipeline input; apply time/source selection upstream and omit Explain, Trace, ReportKind, and FromStore.");
        }
        if (checkpoint && (string.IsNullOrWhiteSpace(SourceIdentity) || string.IsNullOrWhiteSpace(RetentionIdentity))) {
            throw new PSArgumentException("Checkpoint replay requires explicit SourceIdentity and RetentionIdentity.");
        }
        EventDetectionPlan plan = CompilePlan().Plan;
        _streamProjection = plan.RequiredEventTypes.Count == 0 ? null : EventTypeCatalog.CompileProjectionPlan(plan.RequiredEventTypes);
        EventDetectionEngineOptions options = new EventDetectionEngineOptions(MaximumObservations, MaximumGroups,
            MaximumStateObservations, MaximumStateBytes, coverage: Coverage).WithAbsenceWindow(AbsenceWindow);
        string source = SourceIdentity ?? "PowerShell ordered pipeline";
        string retention = RetentionIdentity ?? "transient";
        if (CheckpointIn == null) {
            _replay = new EventDetectionReplaySession(plan, source, retention, options, CancelToken);
        } else {
            string path = SessionState.Path.GetUnresolvedProviderPathFromPSPath(CheckpointIn);
            using var checkpointFile = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (checkpointFile.Length > 64 * 1024 * 1024) { throw new InvalidDataException("Checkpoint exceeds 64 MiB."); }
            using var checkpointReader = new StreamReader(checkpointFile);
            _replay = EventDetectionReplaySession.Restore(checkpointReader.ReadToEnd(), plan, source, retention, options, cancellationToken: CancelToken);
        }
        _checkpointOutputPath = CheckpointOut == null ? null : SessionState.Path.GetUnresolvedProviderPathFromPSPath(CheckpointOut);
        if (_checkpointOutputPath != null && File.Exists(_checkpointOutputPath)) { throw new IOException("CheckpointOut must name a new file."); }
        return Task.CompletedTask;
    }

    private void ProcessStreaming(object? value) {
        if (value == null || _replay!.IsInvalidated) { return; }
        EventObservation observation;
        if (value is EventObservation canonical) {
            observation = canonical;
        } else if (value is CustomEventRecord customRecord) {
            observation = EventObservation.FromCustomRecord(customRecord);
        } else {
            EventObject source = value switch {
                EventObject raw => raw, EventTypeRecord typed => typed.SourceEvent, CustomEventRecord custom => custom.SourceEvent,
                _ => throw new PSArgumentException("Stream input must be an EventObservation, EventObject, or projected event.")
            };
            observation = EventObservation.Create(source, _streamProjection == null ? value as EventTypeRecord : EventTypeCatalog.CreateEventRule(source, _streamProjection));
        }
        foreach (EventDetectionFinding finding in _replay.Process(observation)) { WriteObject(finding, enumerateCollection: false); }
    }

    private void CompleteStreaming() {
        if (_replay == null || _replay.IsInvalidated) { return; }
        CancelToken.ThrowIfCancellationRequested();
        if (_checkpointOutputPath != null) {
            if (AbsenceWindow != null) {
                foreach (EventDetectionFinding finding in _replay.AdvanceWatermark(AbsenceWindow)) { WriteObject(finding, enumerateCollection: false); }
            }
            if (!_replay.IsInvalidated) { _replay.SaveCheckpoint(_checkpointOutputPath); }
        } else {
            foreach (EventDetectionFinding finding in _replay.Complete()) { WriteObject(finding, enumerateCollection: false); }
        }
    }
}
