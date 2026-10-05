using Xunit;

namespace EventViewerX.Tests;

public sealed partial class TestEventDetection {
    [Fact]
    public void LaterSourceCoverageCannotFillAnEarlierBatchGapInPositiveCorrelation() {
        EventDetectionPlan plan = EventDetectionPlan.Compile(new[] { new EventDetectionRule(new EventDetectionRuleDefinition {
            RuleId = "threshold", Title = "Threshold", Kind = EventDetectionRuleKind.Threshold, Threshold = 2
        }) });
        var initial = new EventDetectionEngineOptions(coverage: EventDetectionCoverage.Create(expectedTargets: new[] { "server01" }));
        var next = new EventDetectionEngineOptions(coverage: EventDetectionCoverage.Create(expectedTargets: new[] { "server01" }, observedTargets: new[] { "server01" }));
        var session = new EventDetectionReplaySession(plan, "query", "generation", initial);
        session.Process(EventObservation.Create(CreateEvent(1, Utc(10, 0), 1, "Tasks", "Provider")));
        EventDetectionReplaySession restored = EventDetectionReplaySession.Restore(session.ExportCheckpoint(), plan, "query", "generation", next);
        EventDetectionFinding finding = Assert.Single(restored.Process(EventObservation.Create(CreateEvent(1, Utc(10, 1), 2, "Tasks", "Provider"))));
        Assert.Equal(EventDetectionFindingStatus.Matched, finding.Status);
        Assert.False(finding.Coverage.IsComplete);
        Assert.Contains(finding.Coverage.Failures, failure => failure.Contains("server01", StringComparison.Ordinal));
    }

    [Fact]
    public void RestartCannotUpgradeFailedHistoricalCoverageToConfirmedAbsence() {
        var options = new EventDetectionEngineOptions(coverage: EventDetectionCoverage.Create(failures: new[] { "Interrupted read" }));
        EventDetectionPlan plan = AbsencePlan();
        EventObject source = CreateEvent(1, Utc(10, 0), 1, "Tasks", "Provider"); source.Data["TaskId"] = "42";
        var session = new EventDetectionReplaySession(plan, "query", "generation", options);
        session.Process(EventObservation.Create(source));
        var window = new EventDetectionAbsenceWindow(Utc(10, 10), new[] {
            new EventCoverageWindow { ScopeIdentity = "server01/tasks/all", StartUtc = Utc(10, 0), EndUtc = Utc(10, 10), IsComplete = true }
        });
        EventDetectionReplaySession restored = EventDetectionReplaySession.Restore(session.ExportCheckpoint(), plan, "query", "generation",
            new EventDetectionEngineOptions(coverage: EventDetectionCoverage.Create()));
        EventDetectionFinding original = Assert.Single(session.AdvanceWatermark(window));
        EventDetectionFinding resumed = Assert.Single(restored.AdvanceWatermark(window));
        Assert.Equal(EventDetectionFindingStatus.Incomplete, original.Status);
        Assert.Equal(original.Status, resumed.Status);
        Assert.Contains("Interrupted read", resumed.Coverage.Failures);
    }

    [Fact]
    public void CheckpointConsumesFinalizedAbsenceButRetainsLaterTriggers() {
        EventDetectionPlan plan = AbsencePlan();
        var options = new EventDetectionEngineOptions(coverage: EventDetectionCoverage.Create());
        var session = new EventDetectionReplaySession(plan, "query", "generation", options);
        foreach (int minute in new[] { 0, 8 }) {
            EventObject source = CreateEvent(1, Utc(10, minute), minute + 1, "Tasks", "Provider");
            source.Data["TaskId"] = minute.ToString();
            session.Process(EventObservation.Create(source));
        }
        EventDetectionAbsenceWindow Window(int end) => new(Utc(10, end), new[] {
            new EventCoverageWindow { ScopeIdentity = "server01/tasks/all", StartUtc = Utc(10, 0), EndUtc = Utc(10, end), IsComplete = true }
        });
        Assert.Equal("0", Assert.Single(session.AdvanceWatermark(Window(10))).Entities["TaskId"]);
        string checkpoint = session.ExportCheckpoint();
        session = EventDetectionReplaySession.Restore(checkpoint, plan, "query", "generation", options);
        Assert.Empty(session.AdvanceWatermark(Window(10)));
        Assert.Equal("8", Assert.Single(session.AdvanceWatermark(Window(15))).Entities["TaskId"]);
        Assert.Empty(session.Complete());
        session = EventDetectionReplaySession.Restore(checkpoint, plan, "query", "generation", options);
        Assert.Throws<InvalidDataException>(() => session.Process(EventObservation.Create(CreateEvent(2, Utc(10, 9), 20, "Tasks", "Provider"))));
    }

    [Theory]
    [InlineData(EventDetectionRuleKind.Threshold)]
    [InlineData(EventDetectionRuleKind.DistinctValue)]
    [InlineData(EventDetectionRuleKind.Temporal)]
    [InlineData(EventDetectionRuleKind.OrderedTemporal)]
    public void CheckpointPreservesConsumedAndPendingCorrelation(EventDetectionRuleKind kind) {
        EventDetectionPlan plan = EventDetectionPlan.Compile(new[] {
            new EventDetectionRule(new EventDetectionRuleDefinition {
                RuleId = "restart", Title = "Restart", Kind = kind, Threshold = 2, Window = TimeSpan.FromMinutes(10),
                DistinctBy = kind == EventDetectionRuleKind.DistinctValue ? "TaskId" : null,
                Steps = kind is EventDetectionRuleKind.Temporal or EventDetectionRuleKind.OrderedTemporal ? new[] {
                    new EventDetectionStepDefinition { Name = "One", EventIds = new[] { 1 } },
                    new EventDetectionStepDefinition { Name = "Two", EventIds = new[] { 2 } }
                } : Array.Empty<EventDetectionStepDefinition>()
            }) });
        EventObservation[] observations = Enumerable.Range(0, 6).Select(index => {
            EventObject source = CreateEvent(index % 2 + 1, Utc(10, index), index + 1, "Tasks", "Provider");
            source.Data["TaskId"] = index.ToString();
            return EventObservation.Create(source, receivedTimeUtc: Utc(10, index), processedTimeUtc: Utc(10, index));
        }).ToArray();
        var options = new EventDetectionEngineOptions(coverage: EventDetectionCoverage.Create());
        string[] expected = EventDetectionEngine.Evaluate(observations, plan, options).Findings.Select(item => EventAnalysisJson.Serialize(item)).ToArray();
        var session = new EventDetectionReplaySession(plan, "query", "retention-1", options);
        var actual = new List<EventDetectionFinding>();
        foreach (EventObservation observation in observations.Take(3)) { actual.AddRange(session.Process(observation)); }
        string checkpoint = session.ExportCheckpoint();
        session = EventDetectionReplaySession.Restore(checkpoint, plan, "query", "retention-1", options);
        Assert.Empty(session.Process(observations[2]));
        foreach (EventObservation observation in observations.Skip(3)) { actual.AddRange(session.Process(observation)); }
        actual.AddRange(session.Complete());
        Assert.Equal(expected, actual.Select(item => EventAnalysisJson.Serialize(item)));
        Assert.Equal(6, session.ProcessedObservations);
    }

    [Fact]
    public void CheckpointRejectsRuleRetentionAndLateEvidenceChanges() {
        EventDetectionPlan plan = AbsencePlan();
        EventObject source = CreateEvent(1, Utc(10, 0), 1, "Tasks", "Provider"); source.Data["TaskId"] = "42";
        var session = new EventDetectionReplaySession(plan, "query", "generation-1");
        session.Process(EventObservation.Create(source));
        string checkpoint = session.ExportCheckpoint();
        Assert.Throws<InvalidDataException>(() => EventDetectionReplaySession.Restore(checkpoint, plan, "query", "generation-2"));
        Assert.Throws<InvalidDataException>(() => EventDetectionReplaySession.Restore(
            checkpoint.Replace("generation-1", "generation-9"), plan, "query", "generation-9"));
        EventDetectionPlan changed = EventDetectionPlan.Compile(new[] {
            new EventDetectionRule(new EventDetectionRuleDefinition { RuleId = "other", Title = "Other" }) });
        Assert.Throws<InvalidDataException>(() => EventDetectionReplaySession.Restore(checkpoint, changed, "query", "generation-1"));
        EventDetectionReplaySession restored = EventDetectionReplaySession.Restore(checkpoint, plan, "query", "generation-1");
        Assert.Throws<InvalidDataException>(() => restored.Process(EventObservation.Create(CreateEvent(1, Utc(9, 59), 2, "Tasks", "Provider"))));
        Assert.True(restored.IsInvalidated);
        Assert.Throws<InvalidOperationException>(() => restored.ExportCheckpoint());
        restored = EventDetectionReplaySession.Restore(checkpoint, plan, "query", "generation-1");
        EventDetectionFinding unknown = Assert.Single(restored.Complete());
        Assert.Equal(EventDetectionFindingStatus.Incomplete, unknown.Status);
    }
}
