using Xunit;

namespace EventViewerX.Tests;

public sealed partial class TestEventDetection {
    [Fact]
    public void TimelineReportPreservesClockBoundsAndExplicitUnknowns() {
        EventObservation observation = EventObservation.Create(CreateEvent(1, Utc(10, 0), 1, "Tasks", "Provider"));
        EventTimeline known = EventTimelineEngine.Create(new[] { observation }, null, null, new[] {
            new EventClockEvidence("server01", Utc(9, 0), Utc(11, 0), TimeSpan.FromSeconds(-2), TimeSpan.FromSeconds(5), "Independent clock sample")
        });
        EventTimeline unknown = EventTimelineEngine.Create(new[] { observation }, null);
        var report = EventViewerX.Reporting.EventReportEngine.Create(known.Entries.Concat(unknown.Entries).Cast<object>());
        var rows = Assert.Single(report.Sections).Rows;
        Assert.Equal(true, rows[0].Values["ClockBounded"]);
        Assert.Equal(Utc(10, 0).AddSeconds(-7), rows[0].Values["ClockEarliestUtc"]);
        Assert.Equal(Utc(10, 0).AddSeconds(3), rows[0].Values["ClockLatestUtc"]);
        Assert.Equal("Independent clock sample", rows[0].Values["ClockProvenance"]);
        Assert.Equal(false, rows[1].Values["ClockBounded"]);
        Assert.Null(rows[1].Values["ClockEarliestUtc"]);
        Assert.Equal(observation.EventTimeUtc, rows[0].Values[nameof(EventTimelineEntry.EventTimeUtc)]);
        Assert.Equal(observation.ReceivedTimeUtc, rows[0].Values[nameof(EventTimelineEntry.ReceivedTimeUtc)]);
        Assert.Equal(observation.ProcessedTimeUtc, rows[0].Values[nameof(EventTimelineEntry.ProcessedTimeUtc)]);
    }

    [Fact]
    public void ArtifactHashingObservesCancellationDuringTheCurrentFile() {
        using var cancellation = new CancellationTokenSource();
        using var stream = new CancellingEvidenceStream(cancellation);
        Assert.Throws<OperationCanceledException>(() => EventInvestigationSession.HashArtifact(stream, cancellation.Token));
        Assert.Equal(1, stream.ReadCalls);
        byte[] payload = Enumerable.Range(0, 200000).Select(i => (byte)i).ToArray();
        using var input = new MemoryStream(payload);
        Assert.Equal(System.Security.Cryptography.SHA256.HashData(payload), EventInvestigationSession.HashArtifact(input, default));
    }

    private sealed class CancellingEvidenceStream : MemoryStream {
        private readonly CancellationTokenSource cancellation;
        internal CancellingEvidenceStream(CancellationTokenSource cancellation) : base(new byte[200000]) { this.cancellation = cancellation; }
        internal int ReadCalls { get; private set; }
        public override int Read(byte[] buffer, int offset, int count) {
            ReadCalls++;
            int result = base.Read(buffer, offset, count);
            cancellation.Cancel();
            return result;
        }
    }

    [Fact]
    public void ExactReceiptsDoNotOverrideUndeclaredOverallCoverage() {
        EventObject source = CreateEvent(1, Utc(10, 0), 1, "Tasks", "Provider");
        source.Data["TaskId"] = "42";
        var options = new EventDetectionEngineOptions().WithAbsenceWindow(new EventDetectionAbsenceWindow(Utc(10, 10), new[] {
            new EventCoverageWindow { ScopeIdentity = "server01/tasks/all", StartUtc = Utc(10, 0), EndUtc = Utc(10, 10), IsComplete = true }
        }));
        Assert.Equal(EventDetectionFindingStatus.Incomplete,
            Assert.Single(EventDetectionEngine.Evaluate(new[] { source }, AbsencePlan(), options).Findings).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ImpactRequiresCoverageForBothPlans(bool broadCoverage) {
        EventDetectionPlan Plan(int id) => EventDetectionPlan.Compile(new[] { new EventDetectionRule(new EventDetectionRuleDefinition {
            RuleId = "scope", Title = "Scope", EventIds = new[] { id }
        }) });
        int[] ids = broadCoverage ? new[] { 1, 2 } : new[] { 1 };
        var options = new EventDetectionEngineOptions(coverage: EventDetectionCoverage.Create(expectedEventIds: ids, observedEventIds: ids));
        EventDetectionImpactPreview result = EventDetectionEngine.PreviewChanges(new[] {
            EventObservation.Create(CreateEvent(1, Utc(10, 0), 1, "Tasks", "Provider"))
        }, Plan(1), Plan(2), options);
        Assert.Equal(broadCoverage, result.IsComplete);
        if (!broadCoverage) { Assert.Contains(result.Diagnostics, value => value.Contains("EventId", StringComparison.Ordinal)); }
    }

    [Theory]
    [InlineData("Channel")]
    [InlineData("Provider")]
    [InlineData("UnrestrictedEventId")]
    public void ImpactDoesNotPromoteFiniteCollectionToBroaderSelectors(string dimension) {
        var oldRule = new EventDetectionRuleDefinition { RuleId = "scope", Title = "Scope", EventIds = new[] { 1 },
            Channels = new[] { "OldChannel" }, Providers = new[] { "OldProvider" } };
        var newRule = oldRule.Snapshot();
        if (dimension == "Channel") { newRule.Channels = new[] { "NewChannel" }; }
        if (dimension == "Provider") { newRule.Providers = new[] { "NewProvider" }; }
        if (dimension == "UnrestrictedEventId") { newRule.EventIds = Array.Empty<int>(); }
        var coverage = EventDetectionCoverage.Create(expectedEventIds: new[] { 1 }, observedEventIds: new[] { 1 },
            expectedChannels: new[] { "OldChannel" }, observedChannels: new[] { "OldChannel" },
            expectedProviders: new[] { "OldProvider" }, observedProviders: new[] { "OldProvider" });
        var preview = EventDetectionEngine.PreviewChanges(Array.Empty<EventObservation>(),
            EventDetectionPlan.Compile(new[] { new EventDetectionRule(oldRule) }),
            EventDetectionPlan.Compile(new[] { new EventDetectionRule(newRule) }), new EventDetectionEngineOptions(coverage: coverage));
        Assert.False(preview.IsComplete);
        Assert.Contains(preview.Diagnostics, value => value.Contains(dimension == "UnrestrictedEventId" ? "EventId" : dimension, StringComparison.Ordinal));
    }

    [Fact]
    public void ImpactChecksTypedCoverageAndDoesNotMisreadTypedSourcesAsWildcards() {
        EventDetectionPlan Plan(EventType type) => EventDetectionPlan.Compile(new[] { new EventDetectionRule(new EventDetectionRuleDefinition {
            RuleId = "typed", Title = "Typed", EventTypes = new[] { type }
        }) });
        var typedCoverage = EventDetectionCoverage.Create(expectedEventTypes: new[] { EventType.OSStartup }, observedEventTypes: new[] { EventType.OSStartup });
        var missing = EventDetectionEngine.PreviewChanges(Array.Empty<EventObservation>(), Plan(EventType.OSStartup), Plan(EventType.OSShutdown),
            new EventDetectionEngineOptions(coverage: typedCoverage));
        Assert.False(missing.IsComplete);
        Assert.Contains(missing.Diagnostics, value => value.Contains("EventType", StringComparison.Ordinal));
        IReadOnlyList<EventSourceDefinition> sources = EventTypeCatalog.GetSources(new[] { EventType.OSStartup });
        var nativeCoverage = EventDetectionCoverage.Create(expectedEventIds: sources.SelectMany(s => s.EventIds), observedEventIds: sources.SelectMany(s => s.EventIds),
            expectedChannels: sources.Select(s => s.LogName), observedChannels: sources.Select(s => s.LogName));
        Assert.True(EventDetectionEngine.PreviewChanges(Array.Empty<EventObservation>(), Plan(EventType.OSStartup), Plan(EventType.OSStartup),
            new EventDetectionEngineOptions(coverage: nativeCoverage)).IsComplete);
    }

    [Fact]
    public void ExpiredAbsenceTriggersSurviveCompletionMatchingAndRestart() {
        var session = new EventDetectionReplaySession(AbsencePlan(), "tasks", "v1",
            new EventDetectionEngineOptions(coverage: EventDetectionCoverage.Create()));
        EventObservation Observation(int id, int minute, long record) {
            EventObject source = CreateEvent(id, Utc(10, minute), record, "Tasks", "Provider");
            source.Data["TaskId"] = "shared";
            return EventObservation.Create(source);
        }
        for (int i = 1; i <= 100; i++) { session.Process(Observation(1, 0, i)); }
        session.Process(Observation(1, 9, 101));
        session.Process(Observation(2, 10, 102));
        session = EventDetectionReplaySession.Restore(session.ExportCheckpoint(), AbsencePlan(), "tasks", "v1",
            new EventDetectionEngineOptions(coverage: EventDetectionCoverage.Create()));
        session.Process(Observation(1, 11, 103));
        session.Process(Observation(2, 12, 104));
        EventDetectionFinding[] findings = session.AdvanceWatermark(new EventDetectionAbsenceWindow(Utc(10, 20), new[] {
            new EventCoverageWindow { ScopeIdentity = "server01/tasks/all", StartUtc = Utc(10, 0), EndUtc = Utc(10, 20), IsComplete = true }
        })).ToArray();
        Assert.Equal(100, findings.Length);
        Assert.All(findings, finding => {
            Assert.Equal(EventDetectionFindingStatus.Matched, finding.Status);
            Assert.Equal(Utc(10, 0), finding.StartTimeUtc);
        });
        Assert.Empty(session.Complete());
    }
}
