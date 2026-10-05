using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EventViewerX;

/// <summary>A sequential, restartable evaluator that persists consumed correlation state rather than replaying the complete source history.</summary>
public sealed class EventDetectionReplaySession {
    private readonly EventDetectionPlan _plan;
    private readonly EventDetectionEngineOptions _options;
    private readonly EventDetectionEngine.Evaluator _evaluator;
    private readonly string _sourceIdentity;
    private readonly string _retentionIdentity;
    private readonly CancellationToken _cancellationToken;
    private bool _completed;
    private bool _invalidated;
    private DateTime? _lastTimeUtc;
    private long? _lastRecordId;
    private string _lastIdentity = string.Empty;
    private DateTime? _finalizedThroughUtc;

    /// <summary>Creates a replay over an exact source/query scope and retention generation. Change the retention identity after pruning, replacement, or historical correction.</summary>
    public EventDetectionReplaySession(EventDetectionPlan plan, string sourceIdentity, string retentionIdentity,
        EventDetectionEngineOptions? options = null, CancellationToken cancellationToken = default) {
        _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        if (string.IsNullOrWhiteSpace(sourceIdentity) || string.IsNullOrWhiteSpace(retentionIdentity)) {
            throw new ArgumentException("Replay requires exact source/query and retention generation identities.");
        }
        _options = options ?? new EventDetectionEngineOptions();
        if (_options.EventTimeOrdering != null) { throw new ArgumentException("Durable replay requires event-time ordered input; live reorder buffers are not checkpointed.", nameof(options)); }
        _sourceIdentity = sourceIdentity; _retentionIdentity = retentionIdentity;
        _cancellationToken = cancellationToken;
        _evaluator = new EventDetectionEngine.Evaluator(plan, _options, cancellationToken);
    }
    /// <summary>Last accepted event time. Resume source reads from this inclusive boundary and order by time, record ID, then evidence identity.</summary>
    public DateTime? LastEventTimeUtc => _lastTimeUtc;
    /// <summary>Last accepted record ID, used to break equal-time ties.</summary>
    public long? LastRecordId => _lastRecordId;
    /// <summary>Last accepted evidence identity, used to break equal-time and equal-record-ID ties.</summary>
    public string LastObservationIdentity => _lastIdentity;
    /// <summary>Total observations processed across successful restarts.</summary>
    public long ProcessedObservations { get; private set; }
    /// <summary>Whether late input or a failed processing call invalidated this session.</summary>
    public bool IsInvalidated => _invalidated;

    /// <summary>Processes one ordered observation. An exact repeat of the checkpoint boundary is ignored; older input invalidates the session.</summary>
    public IReadOnlyList<EventDetectionFinding> Process(EventObservation observation) {
        if (observation == null) { throw new ArgumentNullException(nameof(observation)); }
        EnsureActive(); _cancellationToken.ThrowIfCancellationRequested();
        if (_finalizedThroughUtc.HasValue && observation.EventTimeUtc < _finalizedThroughUtc.Value) {
            _invalidated = true;
            throw new InvalidDataException("Evidence predates the finalized coverage watermark; replay must be rebuilt before this evidence.");
        }
        if (_lastTimeUtc.HasValue) {
            int comparison = observation.EventTimeUtc.CompareTo(_lastTimeUtc.Value);
            if (comparison == 0) { comparison = Nullable.Compare(observation.RecordId, _lastRecordId); }
            if (comparison == 0) { comparison = string.Compare(observation.Identity, _lastIdentity, StringComparison.Ordinal); }
            if (comparison == 0) { return Array.Empty<EventDetectionFinding>(); }
            if (comparison < 0) {
                _invalidated = true;
                throw new InvalidDataException("Late or out-of-order evidence invalidated replay state. Rebuild from a checkpoint preceding this evidence or from the affected source history.");
            }
        }
        try {
            EventDetectionFinding[] findings = _evaluator.Process(observation).ToArray();
            _lastTimeUtc = observation.EventTimeUtc; _lastRecordId = observation.RecordId; _lastIdentity = observation.Identity;
            ProcessedObservations++;
            if (findings.Any(item => item.Status != EventDetectionFindingStatus.Matched)) { _invalidated = true; }
            return findings;
        } catch { _invalidated = true; throw; }
    }

    /// <summary>Finalizes pending absence decisions. Save a checkpoint before completion when subsequent input must continue the same correlation.</summary>
    public IReadOnlyList<EventDetectionFinding> Complete() {
        EnsureActive();
        try { return _evaluator.Complete().ToArray(); }
        finally { _completed = true; }
    }

    /// <summary>Finalizes due absence decisions while retaining later triggers. Call only after all source input before AsOfUtc has been processed. Later evidence before this global watermark invalidates replay.</summary>
    public IReadOnlyList<EventDetectionFinding> AdvanceWatermark(EventDetectionAbsenceWindow window) {
        if (window == null) { throw new ArgumentNullException(nameof(window)); }
        EnsureActive();
        if (_finalizedThroughUtc.HasValue && window.AsOfUtc < _finalizedThroughUtc.Value) { throw new ArgumentException("A finalized watermark cannot move backwards.", nameof(window)); }
        try {
            EventDetectionFinding[] findings = _evaluator.FinalizeAbsence(window, true).ToArray();
            if (findings.Any(item => item.Status != EventDetectionFindingStatus.Matched)) { _invalidated = true; }
            _finalizedThroughUtc = window.AsOfUtc;
            return findings;
        } catch { _invalidated = true; throw; }
    }

    /// <summary>Exports bounded state with plan, engine, source, retention, limits, cursor, and corruption-detection identities.</summary>
    public string ExportCheckpoint(int maximumCheckpointBytes = 64 * 1024 * 1024) {
        EnsureActive();
        if (maximumCheckpointBytes <= 0) { throw new ArgumentOutOfRangeException(nameof(maximumCheckpointBytes)); }
        string state = _evaluator.CaptureState();
        var document = new ReplayCheckpoint {
            SchemaVersion = 1, EngineIdentity = EventInvestigationSession.CurrentEngineIdentity,
            PlanHash = _plan.PlanHash, SourceIdentity = _sourceIdentity, RetentionIdentity = _retentionIdentity,
            LimitsIdentity = LimitsIdentity(_options), State = state,
            LastEventTimeUtc = _lastTimeUtc, LastRecordId = _lastRecordId, LastObservationIdentity = _lastIdentity,
            ProcessedObservations = ProcessedObservations, FinalizedThroughUtc = _finalizedThroughUtc,
            CoverageJson = (_options.Coverage ?? EventDetectionCoverage.Unknown()).ToJson()
        };
        document.StateHash = CheckpointHash(document);
        string json = JsonSerializer.Serialize(document, EventAnalysisJson.CreateSerializerOptions());
        if (Encoding.UTF8.GetByteCount(json) > maximumCheckpointBytes) { throw new InvalidDataException("Checkpoint exceeds its serialized byte bound."); }
        return json;
    }

    /// <summary>Restores state only when every execution identity still matches. No implicit restart discards consumed evidence.</summary>
    public static EventDetectionReplaySession Restore(string checkpointJson, EventDetectionPlan plan,
        string sourceIdentity, string retentionIdentity, EventDetectionEngineOptions? options = null,
        int maximumCheckpointBytes = 64 * 1024 * 1024, CancellationToken cancellationToken = default) {
        if (checkpointJson == null) { throw new ArgumentNullException(nameof(checkpointJson)); }
        if (maximumCheckpointBytes <= 0 || Encoding.UTF8.GetByteCount(checkpointJson) > maximumCheckpointBytes) {
            throw new InvalidDataException("Checkpoint exceeds its serialized byte bound.");
        }
        if (plan == null) { throw new ArgumentNullException(nameof(plan)); }
        options ??= new EventDetectionEngineOptions();
        ReplayCheckpoint document = JsonSerializer.Deserialize<ReplayCheckpoint>(checkpointJson, EventAnalysisJson.CreateSerializerOptions())
            ?? throw new InvalidDataException("Missing replay checkpoint.");
        if (document.SchemaVersion != 1 || document.EngineIdentity != EventInvestigationSession.CurrentEngineIdentity) { throw new InvalidDataException("Checkpoint format or engine build changed."); }
        if (document.PlanHash != plan.PlanHash) { throw new InvalidDataException("Checkpoint invalidated by changed rules or tuning."); }
        if (document.SourceIdentity != sourceIdentity) { throw new InvalidDataException("Checkpoint source or query scope changed."); }
        if (document.RetentionIdentity != retentionIdentity) { throw new InvalidDataException("Checkpoint invalidated by retention or historical source changes."); }
        if (document.LimitsIdentity != LimitsIdentity(options)) { throw new InvalidDataException("Checkpoint execution bounds changed."); }
        if (document.State == null || document.StateHash != CheckpointHash(document) || document.ProcessedObservations < 0 ||
            (document.FinalizedThroughUtc.HasValue && document.FinalizedThroughUtc.Value.Kind != DateTimeKind.Utc) ||
            (document.LastEventTimeUtc.HasValue && (document.LastEventTimeUtc.Value.Kind != DateTimeKind.Utc || string.IsNullOrWhiteSpace(document.LastObservationIdentity))) ||
            (document.ProcessedObservations > 0) != document.LastEventTimeUtc.HasValue) {
            throw new InvalidDataException("Replay checkpoint is corrupt or inconsistent.");
        }
        EventDetectionCoverage coverage = EventDetectionCoverage.FromJson(document.CoverageJson)
            .Accumulate(options.Coverage ?? EventDetectionCoverage.Unknown());
        if (options.EventTimeOrdering != null) { throw new ArgumentException("Durable replay requires ordered input without a live reorder buffer.", nameof(options)); }
        var combinedOptions = new EventDetectionEngineOptions(options.MaximumObservations, options.MaximumGroups,
            options.MaximumStateObservations, options.MaximumStateBytes, options.MaximumCandidateRules, coverage).WithAbsenceWindow(options.AbsenceWindow);
        var session = new EventDetectionReplaySession(plan, sourceIdentity, retentionIdentity, combinedOptions, cancellationToken);
        session._evaluator.RestoreState(document.State, document.ProcessedObservations);
        session._lastTimeUtc = document.LastEventTimeUtc; session._lastRecordId = document.LastRecordId;
        session._lastIdentity = document.LastObservationIdentity; session.ProcessedObservations = document.ProcessedObservations;
        session._finalizedThroughUtc = document.FinalizedThroughUtc;
        return session;
    }

    /// <summary>Writes a new durable checkpoint file without replacing earlier checkpoints. Commit output findings before publishing this checkpoint.</summary>
    public void SaveCheckpoint(string path, int maximumCheckpointBytes = 64 * 1024 * 1024) {
        byte[] bytes = Encoding.UTF8.GetBytes(ExportCheckpoint(maximumCheckpointBytes));
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
    }

    private void EnsureActive() {
        if (_invalidated) { throw new InvalidOperationException("Replay state is invalid; restart from a valid earlier checkpoint."); }
        if (_completed) { throw new InvalidOperationException("Replay has been completed."); }
    }
    private static string LimitsIdentity(EventDetectionEngineOptions options) =>
        $"{options.MaximumObservations}:{options.MaximumGroups}:{options.MaximumStateObservations}:{options.MaximumStateBytes}:{options.MaximumCandidateRules}";
    private static string Hash(string text) {
        using SHA256 hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", string.Empty);
    }
    private static string CheckpointHash(ReplayCheckpoint document) {
        string savedHash = document.StateHash;
        try {
            document.StateHash = string.Empty;
            return Hash(JsonSerializer.Serialize(document, EventAnalysisJson.CreateSerializerOptions()));
        } finally { document.StateHash = savedHash; }
    }
    private sealed class ReplayCheckpoint {
        public int SchemaVersion { get; set; }
        public string EngineIdentity { get; set; } = string.Empty;
        public string PlanHash { get; set; } = string.Empty;
        public string SourceIdentity { get; set; } = string.Empty;
        public string RetentionIdentity { get; set; } = string.Empty;
        public string LimitsIdentity { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string StateHash { get; set; } = string.Empty;
        public DateTime? LastEventTimeUtc { get; set; }
        public long? LastRecordId { get; set; }
        public string LastObservationIdentity { get; set; } = string.Empty;
        public long ProcessedObservations { get; set; }
        public DateTime? FinalizedThroughUtc { get; set; }
        public string CoverageJson { get; set; } = string.Empty;
    }
}
