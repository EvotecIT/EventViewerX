using System.Text.Json;

namespace EventViewerX;

public sealed partial class EventInvestigationSession {
    /// <summary>Captures endpoint artifacts into the existing session format. Parses copied bytes, preserves original context, and commits the manifest last.</summary>
    /// <remarks>Native Windows observations and a detection plan may be supplied alongside text evidence. Diagnostic records are not filtered by the native event window.</remarks>
    public static EventInvestigationSession CreateEndpoint(string directory, EventEndpointCapture capture,
        EventInvestigationManifest? manifest = null, IEnumerable<EventObservation>? observations = null,
        EventDetectionPlan? plan = null, EventDetectionCoverage? coverage = null, CancellationToken cancellationToken = default) {
        if (capture == null) { throw new ArgumentNullException(nameof(capture)); }
        capture = JsonSerializer.Deserialize<EventEndpointCapture>(JsonSerializer.Serialize(capture, CompactOptions), CompactOptions)!;
        ValidateCapture(capture);
        manifest ??= new EventInvestigationManifest {
            StartUtc = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc),
            EndUtc = DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc),
            QueryIdentity = "endpoint-capture/v1", QueryDefinition = "Offline captured artifacts; no live collection is executed."
        };
        return CreateCore(directory, manifest, observations ?? Array.Empty<EventObservation>(),
            plan ?? EventDetectionPlan.Compile(Array.Empty<IEventDetectionRule>()), capture.Inputs.Select(input => input.Path),
            coverage, cancellationToken, capture);
    }

    /// <summary>Reads the verified original endpoint result without evaluating rules or executing collection.</summary>
    public EventEndpointAnalysis ReadEndpointAnalysis(CancellationToken cancellationToken = default) {
        RequireEndpoint(); Verify(cancellationToken);
        return JsonSerializer.Deserialize<EventEndpointAnalysis>(ReadText("endpoint-analysis.json", 256 * 1024 * 1024, true), CompactOptions)
            ?? throw new InvalidDataException("Missing endpoint analysis.");
    }

    /// <summary>Re-evaluates retained diagnostic records with verified collection context. Originals and outputs are never rewritten.</summary>
    public EventEndpointAnalysis ReplayEndpoint(bool allowDifferentEngine = false, CancellationToken cancellationToken = default) {
        RequireEndpoint();
        if (!allowDifferentEngine && _manifest.EngineIdentity != CurrentEngineIdentity) { throw new InvalidDataException("Engine build differs; explicitly opt in to comparative replay."); }
        Verify(cancellationToken);
        EndpointReceipt receipt = JsonSerializer.Deserialize<EndpointReceipt>(ReadText("endpoint-capture.json", 4 * 1024 * 1024, true), CompactOptions)
            ?? throw new InvalidDataException("Missing endpoint receipt.");
        var diagnostics = new List<string>(receipt.Diagnostics);
        if (_manifest.EngineIdentity != CurrentEngineIdentity) { diagnostics.Add("Comparative replay used a different engine build."); }
        return EventIntuneApplicationAnalyzer.Analyze(ReadDiagnosticRecords(receipt, cancellationToken), receipt.IdentitySnapshots,
            receipt.ExpectedJoin, receipt.InputComplete && _manifest.EngineIdentity == CurrentEngineIdentity, diagnostics);
    }

    private void RequireEndpoint() {
        if (_manifest.EndpointEvidenceVersion != 1) { throw new InvalidOperationException("This session has no endpoint evidence extension."); }
    }

    private static void ValidateCapture(EventEndpointCapture capture) {
        if (capture.Inputs == null || capture.Inputs.Length == 0 || capture.Inputs.Length > 128 || capture.MaximumRecords <= 0 || capture.MaximumRecords > 100_000 ||
            capture.MaximumInputBytes <= 0 || capture.MaximumInputBytes > 1024L * 1024 * 1024 || capture.MaximumRecordBytes <= 0 || capture.MaximumRecordBytes > 128L * 1024 * 1024) {
            throw new ArgumentException("Use 1-128 inputs with finite record and byte bounds.", nameof(capture));
        }
        if (capture.ExpectedJoin != null && capture.ExpectedJoin is not ("Entra" or "Hybrid" or "Domain" or "Unjoined")) { throw new ArgumentException("Unknown intended join state.", nameof(capture)); }
        foreach (EventEndpointInput input in capture.Inputs) {
            if (input == null || string.IsNullOrWhiteSpace(input.Path) || input.Kind is not ("Log" or "DsRegCmd" or "Facts" or "Registry" or "Attachment") ||
                input.ExecutionContext is not ("User" or "SYSTEM" or "Unknown") ||
                input.UtcOffset.HasValue && (input.UtcOffset.Value.Duration() > TimeSpan.FromHours(14) || input.UtcOffset.Value.Ticks % TimeSpan.TicksPerMinute != 0)) {
                throw new ArgumentException("Invalid endpoint input context.", nameof(capture));
            }
        }
    }

    private void WriteEndpoint(EventEndpointCapture capture, List<EventInvestigationArtifact> artifacts, CancellationToken token) {
        EventInvestigationArtifact[] inputs = artifacts.Where(artifact => artifact.Role == "input").ToArray();
        var records = new List<EventDiagnosticRecord>();
        var snapshots = new List<EventDsRegSnapshot>();
        var diagnostics = new List<string> { "Coverage describes supplied artifacts only. It does not prove endpoint, deployment, or lifecycle completeness." };
        long recordBytes = 0;
        bool complete = true;
        for (int index = 0; index < inputs.Length; index++) {
            token.ThrowIfCancellationRequested();
            EventEndpointInput input = capture.Inputs[index];
            EventInvestigationArtifact artifact = inputs[index];
            artifact.Kind = input.Kind; artifact.CapturedAt = input.CapturedAt; artifact.ExecutionContext = input.ExecutionContext; artifact.Sensitivity = "Sensitive";
            _manifest.ParserVersions[artifact.Path] = input.Kind switch { "Log" => "EventDiagnosticLogReader/1.0.0", "DsRegCmd" => "EventDsRegAnalyzer/1.0.0", _ => "CapturedArtifact/1.0.0" };
            if (input.Kind == "Log") {
                EventDiagnosticCheckpoint? checkpoint = null;
                while (true) {
                    int available = capture.MaximumRecords - records.Count;
                    if (available == 0) { complete = false; diagnostics.Add(artifact.Path + ": diagnostic record limit reached."); break; }
                    EventDiagnosticReadResult batch = EventDiagnosticLogReader.Read(Resolve(artifact.Path), new EventDiagnosticReadOptions {
                        Source = artifact.Path, Device = input.Device, UtcOffset = input.UtcOffset, MaximumRecords = Math.Min(10_000, available)
                    }, checkpoint, token);
                    complete &= batch.Records.All(record => record.Diagnostic == null);
                    bool capped = false;
                    foreach (EventDiagnosticRecord record in batch.Records) {
                        record.CaptureContext = input.ExecutionContext;
                        long bytes = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(record, CompactOptions));
                        if (recordBytes + bytes > capture.MaximumRecordBytes) { complete = false; capped = true; diagnostics.Add(artifact.Path + ": diagnostic byte limit reached."); break; }
                        records.Add(record); recordBytes += bytes;
                    }
                    if (capped) { break; }
                    if (!batch.HasMore) { complete &= batch.IsComplete; break; }
                    if (batch.Checkpoint.Offset == (checkpoint?.Offset ?? 0)) { complete = false; diagnostics.Add(artifact.Path + ": trailing frame incomplete or larger than the batch bound."); break; }
                    checkpoint = batch.Checkpoint;
                }
            } else if (input.Kind == "DsRegCmd") {
                snapshots.Add(EventDsRegAnalyzer.Parse(ReadText(artifact.Path, 1024 * 1024, false), artifact.Sha256, input.ExecutionContext, input.CapturedAt));
            } else if (input.Kind == "Facts") {
                using JsonDocument facts = JsonDocument.Parse(ReadText(artifact.Path, 1024 * 1024, false));
                if (facts.RootElement.ValueKind != JsonValueKind.Object) { throw new InvalidDataException("Captured facts must be a JSON object."); }
                diagnostics.Add(artifact.Path + ": structured facts retained for inspection; no unsupported policy inference was applied.");
            } else { diagnostics.Add(artifact.Path + ": original " + input.Kind + " retained as evidence; it was not imported or executed."); }
        }
        var receipt = new EndpointReceipt { ExpectedJoin = capture.ExpectedJoin, IdentitySnapshots = snapshots.ToArray(), InputComplete = complete,
            Diagnostics = diagnostics.ToArray(), MaximumRecords = capture.MaximumRecords, MaximumRecordBytes = capture.MaximumRecordBytes, RecordCount = records.Count };
        using (var stream = new FileStream(Resolve("endpoint-records.jsonl"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) {
            foreach (EventDiagnosticRecord record in records) { token.ThrowIfCancellationRequested(); writer.WriteLine(JsonSerializer.Serialize(record, CompactOptions)); }
            writer.Flush(); stream.Flush(true);
        }
        string receiptJson = JsonSerializer.Serialize(receipt, CompactOptions);
        if (Encoding.UTF8.GetByteCount(receiptJson) > 4 * 1024 * 1024) { throw new InvalidDataException("Endpoint context receipt exceeds 4 MiB."); }
        WriteNew("endpoint-capture.json", receiptJson);
        EventEndpointAnalysis analysis = EventIntuneApplicationAnalyzer.Analyze(records, snapshots, capture.ExpectedJoin, complete, diagnostics);
        string analysisJson = JsonSerializer.Serialize(analysis, CompactOptions);
        if (Encoding.UTF8.GetByteCount(analysisJson) > 256 * 1024 * 1024) { throw new InvalidDataException("Endpoint output exceeds 256 MiB."); }
        WriteNew("endpoint-analysis.json", analysisJson);
        foreach (string path in new[] { "endpoint-records.jsonl", "endpoint-capture.json", "endpoint-analysis.json" }) {
            EventInvestigationArtifact artifact = Describe(path, path == "endpoint-analysis.json" ? "output" : "diagnostics", token);
            artifact.Sensitivity = "Sensitive"; artifacts.Add(artifact);
        }
    }

    private IEnumerable<EventDiagnosticRecord> ReadDiagnosticRecords(EndpointReceipt receipt, CancellationToken token) {
        if (receipt.MaximumRecords <= 0 || receipt.MaximumRecords > 100_000 || receipt.MaximumRecordBytes <= 0 || receipt.MaximumRecordBytes > 128L * 1024 * 1024 ||
            receipt.RecordCount < 0 || receipt.RecordCount > receipt.MaximumRecords) { throw new InvalidDataException("Invalid endpoint receipt bounds."); }
        using var stream = new FileStream(Resolve("endpoint-records.jsonl"), FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > receipt.MaximumRecordBytes + receipt.MaximumRecords * 2L) { throw new InvalidDataException("Diagnostic evidence exceeds its bound."); }
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true));
        var line = new StringBuilder();
        int count = 0, value;
        while ((value = reader.Read()) >= 0) {
            token.ThrowIfCancellationRequested();
            if (value != '\n') {
                if (line.Length >= 4 * 1024 * 1024) { throw new InvalidDataException("Diagnostic record exceeds 4 MiB."); }
                line.Append((char)value); continue;
            }
            if (++count > receipt.MaximumRecords) { throw new InvalidDataException("Diagnostic record count exceeded."); }
            EventDiagnosticRecord record = JsonSerializer.Deserialize<EventDiagnosticRecord>(line.ToString(), CompactOptions) ?? throw new InvalidDataException("Missing diagnostic record.");
            if (record.ByteStart < 0 || record.ByteEnd <= record.ByteStart || record.LineStart <= 0 || record.LineEnd < record.LineStart || string.IsNullOrWhiteSpace(record.Identity)) { throw new InvalidDataException("Invalid diagnostic source coordinates."); }
            yield return record; line.Clear();
        }
        if (line.Length != 0 || count != receipt.RecordCount) { throw new InvalidDataException("Diagnostic record stream is incomplete."); }
    }

    private sealed class EndpointReceipt {
        public string? ExpectedJoin { get; set; }
        public EventDsRegSnapshot[] IdentitySnapshots { get; set; } = Array.Empty<EventDsRegSnapshot>();
        public bool InputComplete { get; set; }
        public string[] Diagnostics { get; set; } = Array.Empty<string>();
        public int MaximumRecords { get; set; }
        public long MaximumRecordBytes { get; set; }
        public int RecordCount { get; set; }
    }
}