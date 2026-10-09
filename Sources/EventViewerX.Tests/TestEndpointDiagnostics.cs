using System.Text;
using System.Text.Json;
using EventViewerX.Reporting;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEndpointDiagnostics : IDisposable {
    private readonly string _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "evx-endpoint-" + Guid.NewGuid().ToString("N"));
    public TestEndpointDiagnostics() { Directory.CreateDirectory(_root); }
    private string PathOf(string name) => System.IO.Path.Combine(_root, name);
    private static string Frame(string message, string time = "10:00:00.123+120", string context = "") =>
        "<![LOG[" + message + "]LOG]!><time=\"" + time + "\" date=\"10-9-2026\" component=\"IME\" context=\"" + context + "\" type=\"1\" thread=\"77\" file=\"\">\r\n";

    [Theory]
    [InlineData("-2147024891", EventDiagnosticCodeKind.HResult, "Failure", "0x80070005")]
    [InlineData("0x80070005", EventDiagnosticCodeKind.HResult, "Failure", "0x80070005")]
    [InlineData("3010", EventDiagnosticCodeKind.WindowsInstaller, "SuccessRestartRequired", "0x00000BC2")]
    [InlineData("1641", EventDiagnosticCodeKind.WindowsInstaller, "SuccessRestartInitiated", "0x00000669")]
    [InlineData("3010", EventDiagnosticCodeKind.ProcessExit, "Unknown", "0x00000BC2")]
    [InlineData("1603", EventDiagnosticCodeKind.Unknown, "Unknown", "0x00000643")]
    public void CodeOutcomeRequiresDeclaredContext(string text, EventDiagnosticCodeKind kind, string outcome, string hex) {
        EventDiagnosticCode result = EventDiagnosticCode.Resolve(text, kind);
        Assert.Equal(text, result.Original); Assert.Equal(outcome, result.Outcome); Assert.Equal(hex, result.Hex);
        if (kind == EventDiagnosticCodeKind.HResult) { Assert.Equal((uint)5, result.Win32Value); }
    }

    [Fact]
    public void UnknownWriterOffsetDoesNotUseReaderZone() {
        string path = PathOf("time.log"); File.WriteAllText(path, Frame("record", "10:00:00.123"), new UTF8Encoding(false));
        EventDiagnosticRecord unknown = Assert.Single(EventDiagnosticLogReader.Read(path).Records);
        Assert.Null(unknown.Timestamp); Assert.Equal("UnknownOffset", unknown.TimeQuality);
        EventDiagnosticRecord supplied = Assert.Single(EventDiagnosticLogReader.Read(path, new EventDiagnosticReadOptions { UtcOffset = TimeSpan.FromHours(-4) }).Records);
        Assert.Equal(TimeSpan.FromHours(-4), supplied.Timestamp!.Value.Offset); Assert.Equal("SuppliedOffset", supplied.TimeQuality);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IncrementalFramesRetainMultilineAndSplitEncodedWrites(bool utf16) {
        string path = PathOf("partial.log"); Encoding encoding = utf16 ? new UnicodeEncoding(false, true) : new UTF8Encoding(false);
        string complete = Frame("first\nsecond \u20ac"); byte[] bytes = encoding.GetPreamble().Concat(encoding.GetBytes(complete)).ToArray();
        int split = Array.IndexOf(bytes, (byte)0xac);
        if (utf16) { split = bytes.Length - 3; }
        Assert.True(split > 0);
        File.WriteAllBytes(path, bytes.Take(split).ToArray());
        var options = new EventDiagnosticReadOptions { FinalFile = false };
        EventDiagnosticReadResult pending = EventDiagnosticLogReader.Read(path, options);
        Assert.Empty(pending.Records); Assert.True(pending.HasMore);
        using (var append = new FileStream(path, FileMode.Append)) { append.Write(bytes, split, bytes.Length - split); }
        EventDiagnosticReadResult result = EventDiagnosticLogReader.Read(path, options, pending.Checkpoint);
        EventDiagnosticRecord record = Assert.Single(result.Records);
        Assert.Equal("first\nsecond \u20ac", record.Message); Assert.Equal(2, record.LineEnd); Assert.True(result.IsComplete);
        Assert.Equal(TimeSpan.FromHours(2), record.Timestamp!.Value.Offset);
        Assert.Empty(EventDiagnosticLogReader.Read(path, options, result.Checkpoint).Records);
    }

    [Fact]
    public void ResumeDetectsEmptyTruncationAndSameLengthOverwrite() {
        string path = PathOf("rotating.log"); File.WriteAllText(path, "alpha\n", new UTF8Encoding(false));
        EventDiagnosticReadResult first = EventDiagnosticLogReader.Read(path);
        File.WriteAllText(path, "bravo\n", new UTF8Encoding(false));
        EventDiagnosticReadResult rewritten = EventDiagnosticLogReader.Read(path, checkpoint: first.Checkpoint);
        Assert.True(rewritten.GenerationChanged); Assert.NotEqual(first.Records[0].Identity, rewritten.Records[0].Identity);
        File.WriteAllText(path, string.Empty);
        EventDiagnosticReadResult empty = EventDiagnosticLogReader.Read(path, checkpoint: rewritten.Checkpoint);
        Assert.True(empty.GenerationChanged); Assert.Equal(0, empty.Checkpoint.Offset);
        File.WriteAllText(path, "after\n", new UTF8Encoding(false));
        EventDiagnosticReadResult after = EventDiagnosticLogReader.Read(path, checkpoint: empty.Checkpoint);
        Assert.Equal("after", Assert.Single(after.Records).Message);
        Assert.Equal(empty.Checkpoint.Generation, after.Checkpoint.Generation);
    }

    [Fact]
    public void BoundsMarkOversizedEvidenceAndLeaveTrailingFramesPending() {
        string path = PathOf("bounded.log"); File.WriteAllText(path, Frame(new string('x', 800)) + "<![LOG[pending", new UTF8Encoding(false));
        EventDiagnosticReadResult result = EventDiagnosticLogReader.Read(path, new EventDiagnosticReadOptions { MaximumRecordBytes = 128, MaximumBatchBytes = 4096 });
        EventDiagnosticRecord record = Assert.Single(result.Records);
        Assert.Equal("Oversized", record.Format); Assert.True(record.RawText.Length <= 128); Assert.False(result.IsComplete); Assert.True(result.HasMore);
        Assert.True(result.Checkpoint.Offset < new FileInfo(path).Length);
        Assert.Throws<OperationCanceledException>(() => EventDiagnosticLogReader.Read(path, cancellationToken: new CancellationToken(true)));
    }

    [Fact]
    public void SerializedCheckpointResumesOnceAcrossBatchesAndPhysicalReplacement() {
        string path = PathOf("batch.log"); File.WriteAllText(path, "one\ntwo\nthree\n", new UTF8Encoding(false));
        var options = new EventDiagnosticReadOptions { MaximumRecords = 1 };
        EventDiagnosticReadResult first = EventDiagnosticLogReader.Read(path, options);
        EventDiagnosticCheckpoint checkpoint = JsonSerializer.Deserialize<EventDiagnosticCheckpoint>(JsonSerializer.Serialize(first.Checkpoint))!;
        EventDiagnosticReadResult second = EventDiagnosticLogReader.Read(path, options, checkpoint);
        Assert.Equal("two", Assert.Single(second.Records).Message); Assert.Equal(2, second.Records[0].LineStart);
        Assert.NotEqual(first.Records[0].Identity, second.Records[0].Identity);
        File.Move(path, PathOf("old.log")); File.WriteAllText(path, "one\ntwo\nthree\n", new UTF8Encoding(false));
        EventDiagnosticReadResult replaced = EventDiagnosticLogReader.Read(path, options, second.Checkpoint);
        Assert.True(replaced.GenerationChanged); Assert.Equal("one", Assert.Single(replaced.Records).Message);
    }

    [Theory]
    [InlineData("installation command configured", "Unknown")]
    [InlineData("installation has not started", "Unknown")]
    [InlineData("installation starts", "Unknown")]
    [InlineData("installation completed", "Failure")]
    public void NegativeDetectionRequiresObservedExecution(string phase, string expected) {
        string path = PathOf("execution.log");
        const string app = "AppId: 11111111-1111-1111-1111-111111111111 context=SYSTEM attempt=one ";
        File.WriteAllText(path, Frame(app + phase) + Frame(app + "applicationDetected: False", "10:01:00+120"), new UTF8Encoding(false));
        EventIntuneApplicationAttempt attempt = Assert.Single(EventIntuneApplicationAnalyzer.Analyze(EventDiagnosticLogReader.Read(path).Records).Applications);
        Assert.Equal(expected, attempt.Outcome);
    }

    [Fact]
    public async Task ConcurrentTruncationStopsWithoutWaitingForCancellation() {
        string path = PathOf("truncate.log");
        File.WriteAllText(path, string.Empty); _ = EventDiagnosticLogReader.Read(path);
        using (var file = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete)) { file.SetLength(32 * 1024 * 1024); }
        using var started = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Task<EventDiagnosticReadResult> reading = Task.Factory.StartNew(() => {
            started.Set();
            return EventDiagnosticLogReader.Read(path, new EventDiagnosticReadOptions { MaximumBatchBytes = 64 * 1024 * 1024, MaximumRecordBytes = 128 }, cancellationToken: cancellation.Token);
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try {
            Assert.True(started.Wait(TimeSpan.FromSeconds(2)));
            await Task.Delay(100);
            using (var truncate = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete)) { truncate.SetLength(0); }
            EventDiagnosticReadResult result = await reading;
            Assert.False(result.IsComplete); Assert.Empty(result.Records);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Contains("changed during"));
        } finally {
            cancellation.Cancel();
            try { await reading; } catch (OperationCanceledException) { }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MixedTimestampQualityKeepsEntireSourceInByteOrder(bool declaredDevice) {
        string path = PathOf("mixed-time.log");
        const string app = "AppId: 11111111-1111-1111-1111-111111111111 context=SYSTEM attempt=one ";
        File.WriteAllText(path, Frame(app + "applicationDetected: False", "10:00:00") + Frame(app + "MSI exit code 0", "10:01:00+120"), new UTF8Encoding(false));
        var records = EventDiagnosticLogReader.Read(path, new EventDiagnosticReadOptions { Device = declaredDevice ? "captured-device" : string.Empty }).Records;
        EventIntuneApplicationAttempt attempt = Assert.Single(EventIntuneApplicationAnalyzer.Analyze(records).Applications);
        Assert.Equal("InstallerSucceededDetectionUnknown", attempt.Outcome);
        Assert.Equal(records.Select(record => record.Identity), attempt.EvidenceIdentities);
    }

    [Theory]
    [InlineData("User")]
    [InlineData("user")]
    [InlineData("USER")]
    [InlineData("Unknown")]
    [InlineData("unknown")]
    [InlineData("UNKNOWN")]
    public void GenericContextNeverCorrelatesAcrossFiles(string context) {
        string first = PathOf("generic-first.log"), second = PathOf("generic-second.log");
        const string app = "AppId: 11111111-1111-1111-1111-111111111111 attempt=one context=";
        File.WriteAllText(first, Frame(app + context + " MSI exit code 0"));
        File.WriteAllText(second, Frame(app + context + " applicationDetected: False", "10:01:00+120"));
        var options = new EventDiagnosticReadOptions { Device = "captured-device" };
        var records = EventDiagnosticLogReader.Read(first, options).Records.Concat(EventDiagnosticLogReader.Read(second, options).Records);
        EventEndpointAnalysis result = EventIntuneApplicationAnalyzer.Analyze(records);
        Assert.Equal(2, result.Applications.Length); Assert.DoesNotContain(result.Applications, attempt => attempt.Outcome == "Failure");
    }

    [Theory]
    [InlineData("utf-8")]
    [InlineData("utf-16le")]
    [InlineData("utf-16be")]
    public void CapturedTextSupportsBomEncodingsWhileRetainingOriginalBytes(string format) {
        Encoding encoding = format switch { "utf-16le" => new UnicodeEncoding(false, true), "utf-16be" => new UnicodeEncoding(true, true), _ => new UTF8Encoding(true) };
        string dsreg = PathOf("status.txt"), facts = PathOf("facts.json");
        File.WriteAllText(dsreg, "| Device State |\r\nAzureAdJoined : YES\r\nDomainJoined : NO\r\nEnterpriseJoined : NO\r\n", encoding);
        File.WriteAllText(facts, "{\"collector\":\"synthetic\"}", encoding);
        byte[] original = File.ReadAllBytes(dsreg);
        EventInvestigationSession session = EventInvestigationSession.CreateEndpoint(PathOf("encoded-case"), new EventEndpointCapture { ExpectedJoin = "Hybrid", Inputs = new[] {
            new EventEndpointInput { Path = dsreg, Kind = "DsRegCmd" }, new EventEndpointInput { Path = facts, Kind = "Facts" } } });
        EventEndpointAnalysis replay = session.ReplayEndpoint();
        Assert.Equal("YES", Assert.Single(replay.IdentitySnapshots).Fields.Single(field => field.Name == "AzureAdJoined").Value);
        Assert.Contains(replay.Findings, finding => finding.RuleId == "endpoint.identity.join" && finding.Status == "Failure");
        Assert.Equal(original, File.ReadAllBytes(System.IO.Path.Combine(session.DirectoryPath, "input-000001.evidence")));
    }

    [Fact]
    public void EndpointRecordsVerifyBytesConsumedAfterPreflight() {
        string path = PathOf("consumed.log"); File.WriteAllText(path, Frame("AppId: 11111111-1111-1111-1111-111111111111 MSI exit code 1603"));
        EventInvestigationSession session = EventInvestigationSession.CreateEndpoint(PathOf("consumed-case"), new EventEndpointCapture { Inputs = new[] { new EventEndpointInput { Path = path } } });
        session.Verify();
        // Enter the existing record-consumption boundary after a successful preflight, without adding a production test hook.
        var reader = typeof(EventInvestigationSession).GetMethod("ReadDiagnosticRecords", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        Type receiptType = reader.GetParameters()[0].ParameterType;
        object receipt = JsonSerializer.Deserialize(File.ReadAllText(System.IO.Path.Combine(session.DirectoryPath, "endpoint-capture.json")), receiptType,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        string records = System.IO.Path.Combine(session.DirectoryPath, "endpoint-records.jsonl");
        File.WriteAllText(records, File.ReadAllText(records).Replace("1603", "3010"), new UTF8Encoding(false));
        var consumed = (IEnumerable<EventDiagnosticRecord>)reader.Invoke(session, new[] { receipt, (object)CancellationToken.None })!;
        Assert.Throws<InvalidDataException>(() => consumed.ToArray());
    }

    [Fact]
    public void CrossFileCorrelationNeedsDeclaredDeviceAndKnownInstants() {
        string a = PathOf("workload.log"), b = PathOf("actions.log");
        File.WriteAllText(a, Frame("AppId: 11111111-1111-1111-1111-111111111111 context=SYSTEM attempt=one MSI exit code 3010"), new UTF8Encoding(false));
        File.WriteAllText(b, Frame("AppId: 11111111-1111-1111-1111-111111111111 context=SYSTEM attempt=one applicationDetected: True", "10:01:00+120"), new UTF8Encoding(false));
        EventDiagnosticRecord[] records = EventDiagnosticLogReader.Read(a).Records.Concat(EventDiagnosticLogReader.Read(b).Records).ToArray();
        Assert.Equal(2, EventIntuneApplicationAnalyzer.Analyze(records).Applications.Length);
        foreach (EventDiagnosticRecord record in records) { record.Device = "captured-device"; }
        Assert.Equal("RestartRequired", Assert.Single(EventIntuneApplicationAnalyzer.Analyze(records).Applications).Outcome);
        records[1].Timestamp = null;
        Assert.Equal(2, EventIntuneApplicationAnalyzer.Analyze(records).Applications.Length);
    }

    [Fact]
    public void ApplicationEvidenceDoesNotBleedBetweenAppsOrUnattributedLines() {
        const string a = "11111111-1111-1111-1111-111111111111", b = "22222222-2222-2222-2222-222222222222";
        string path = PathOf("apps.log");
        File.WriteAllText(path, Frame("AppId: " + a + " context=SYSTEM attempt=one installation starts") +
            Frame("AppId: " + b + " context=SYSTEM attempt=two installation starts") +
            Frame("MSI exit code 1603") + Frame("AppId: " + a + " context=SYSTEM attempt=one MSI exit code 3010") +
            Frame("AppId: " + b + " context=SYSTEM attempt=two applicationDetected: True"), new UTF8Encoding(false));
        EventEndpointAnalysis analysis = EventIntuneApplicationAnalyzer.Analyze(EventDiagnosticLogReader.Read(path).Records);
        Assert.Equal("RestartRequired", analysis.Applications.Single(app => app.ApplicationId == a).Outcome);
        Assert.Equal("Installed", analysis.Applications.Single(app => app.ApplicationId == b).Outcome);
        Assert.Contains(analysis.CoverageDiagnostics, diagnostic => diagnostic.Contains("not correlated"));
        Assert.All(analysis.Findings, finding => Assert.NotEmpty(finding.NextChecks));
    }

    [Fact]
    public void DsRegUnknownIntentAndSystemContextCannotDiagnoseUserFailure() {
        string text = "| Device State |\nAzureAdJoined : YES\nDomainJoined : NO\n| Tenant Details |\nMdmUrl : https://example.invalid/mdm\n| SSO State |\nAzureAdPrt : NO\n| User State |\nNgcSet : NO\n";
        EventDsRegSnapshot snapshot = EventDsRegAnalyzer.Parse(text, "evidence", "SYSTEM");
        EventDiagnosticFinding[] findings = EventDsRegAnalyzer.Analyze(snapshot);
        Assert.DoesNotContain(findings, finding => finding.Status == "Failure");
        Assert.Contains(findings, finding => finding.RuleId == "endpoint.identity.prt" && finding.Explanation.Contains("SYSTEM"));
        Assert.Equal("Failure", EventDsRegAnalyzer.Analyze(snapshot, "Hybrid").Single(finding => finding.RuleId == "endpoint.identity.join").Status);
        snapshot.Fields = snapshot.Fields.Concat(new[] { new EventDsRegField { Section = "Device State", Name = "AzureAdJoined", Value = "NO" } }).ToArray();
        Assert.Equal("InsufficientEvidence", EventDsRegAnalyzer.Analyze(snapshot, "Hybrid").Single(finding => finding.RuleId == "endpoint.identity.join").Status);
    }

    [Fact]
    public void CapturedSessionReplayUsesHashedOriginalBytesAndRetainsOtherArtifactKinds() {
        string path = PathOf("app.log"), facts = PathOf("facts.json"), registry = PathOf("registry.reg");
        File.WriteAllText(path, Frame("AppId: 11111111-1111-1111-1111-111111111111 context=SYSTEM applicationDetected: True"), new UTF8Encoding(false));
        File.WriteAllText(facts, "{\"intendedContext\":\"SYSTEM\"}"); File.WriteAllText(registry, "Windows Registry Editor Version 5.00\n");
        EventInvestigationSession session = EventInvestigationSession.CreateEndpoint(PathOf("session"), new EventEndpointCapture { Inputs = new[] {
            new EventEndpointInput { Path = path }, new EventEndpointInput { Path = facts, Kind = "Facts" }, new EventEndpointInput { Path = registry, Kind = "Registry" } } });
        EventEndpointAnalysis original = session.ReadEndpointAnalysis();
        File.WriteAllText(path, "source changed after capture");
        EventInvestigationSession opened = EventInvestigationSession.Open(session.DirectoryPath);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(opened.ReplayEndpoint()));
        Assert.Contains(opened.Manifest.Artifacts, artifact => artifact.Kind == "Registry" && artifact.Sensitivity == "Sensitive");
        string minimal = EventEndpointHtmlRenderer.Render(original);
        Assert.DoesNotContain("applicationDetected: True", minimal);
        Assert.Contains("applicationDetected: True", EventEndpointHtmlRenderer.Render(original, true));
        Assert.Empty(opened.Replay().Findings);
        EventInvestigationArtifact input = opened.Manifest.Artifacts.First(artifact => artifact.Role == "input");
        File.AppendAllText(System.IO.Path.Combine(session.DirectoryPath, input.Path), "tampered");
        Assert.Throws<InvalidDataException>(() => EventInvestigationSession.Open(session.DirectoryPath));
    }

    [Fact]
    public void MixedEvidenceReplaysNativeDetectionAndEndpointAnalysisWithoutSyntheticEvents() {
        string path = PathOf("mixed.log"); File.WriteAllText(path, Frame("AppId: 11111111-1111-1111-1111-111111111111 MSI exit code 1603"), new UTF8Encoding(false));
        DateTime instant = new(2026, 10, 9, 8, 0, 0, DateTimeKind.Utc);
        EventObservation observation = EventObservation.Create(new SavedEventRecord { EventId = 42, ProviderName = "SyntheticProvider", Channel = "SyntheticChannel", Computer = "SyntheticHost", TimeCreatedUtc = instant }.ToEventObject("synthetic.evtx", EventReadMode.Full));
        EventDetectionPlan plan = EventDetectionPlan.Compile(new[] { new EventDetectionRule(new EventDetectionRuleDefinition { RuleId = "synthetic.event", Title = "Synthetic event", EventIds = new[] { 42 } }) });
        EventInvestigationSession session = EventInvestigationSession.CreateEndpoint(PathOf("mixed-session"), new EventEndpointCapture { Inputs = new[] { new EventEndpointInput { Path = path } } },
            new EventInvestigationManifest { StartUtc = instant.AddMinutes(-1), EndUtc = instant.AddMinutes(1), QueryIdentity = "synthetic-mixed" }, new[] { observation }, plan);
        Assert.Equal(42, Assert.Single(Assert.Single(session.Replay().Findings).Evidence).EventId);
        Assert.Equal("Failure", Assert.Single(session.ReplayEndpoint().Applications).Outcome);
        Assert.Equal(1, session.Manifest.ObservationCount);
    }

    [Fact]
    public void CaptureBoundsCannotCommitACompleteSession() {
        string path = PathOf("large.log"); File.WriteAllText(path, new string('a', 100));
        string directory = PathOf("rejected-session");
        Assert.Throws<InvalidDataException>(() => EventInvestigationSession.CreateEndpoint(directory, new EventEndpointCapture { Inputs = new[] { new EventEndpointInput { Path = path } }, MaximumInputBytes = 50 }));
        Assert.False(File.Exists(System.IO.Path.Combine(directory, "manifest.json")));
    }

    public void Dispose() { if (Directory.Exists(_root)) { Directory.Delete(_root, true); } }
}