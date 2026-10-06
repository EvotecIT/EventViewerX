using Xunit;

namespace EventViewerX.Tests;

public sealed partial class TestEventDetection {
    [Fact]
    public void FailedOrTruncatedInputCannotEstablishAbsenceEvenWithCompleteReceipts() {
        EventObject start = CreateEvent(1, Utc(10, 0), 1, "Tasks", "Provider"); start.Data["TaskId"] = "42";
        EventObject end = CreateEvent(2, Utc(10, 2), 2, "Tasks", "Provider"); end.Data["TaskId"] = "42";
        var window = new EventDetectionAbsenceWindow(Utc(10, 10), new[] {
            new EventCoverageWindow { ScopeIdentity = "server01/tasks/all", StartUtc = Utc(10, 0), EndUtc = Utc(10, 10), IsComplete = true }
        });
        var failed = new EventDetectionEngineOptions(coverage: EventDetectionCoverage.Create(failures: new[] { "Interrupted read" })).WithAbsenceWindow(window);
        Assert.Equal(EventDetectionFindingStatus.Incomplete, Assert.Single(EventDetectionEngine.Evaluate(new[] { start }, AbsencePlan(), failed).Findings).Status);
        EventDetectionImpactPreview preview = EventDetectionEngine.PreviewChanges(new[] { EventObservation.Create(start), EventObservation.Create(end) },
            AbsencePlan(), AbsencePlan(), new EventDetectionEngineOptions(coverage: EventDetectionCoverage.Create()).WithAbsenceWindow(window), maximumObservations: 1);
        Assert.False(preview.IsComplete);
        Assert.Equal(0, preview.CurrentFindingCount);
    }

    private static EventDetectionPlan AbsencePlan() => EventDetectionPlan.Compile(new[] {
        new EventDetectionRule(new EventDetectionRuleDefinition {
            RuleId = "task-incomplete", Title = "Task did not complete", Kind = EventDetectionRuleKind.Absence,
            Window = TimeSpan.FromMinutes(5), GroupBy = "TaskId", CoverageScope = "server01/tasks/all",
            Steps = new[] {
                new EventDetectionStepDefinition { Name = "Started", EventIds = new[] { 1 } },
                new EventDetectionStepDefinition { Name = "Completed", EventIds = new[] { 2 } }
            }
        })
    });

    [Fact]
    public void DurablePlanPreservesEffectiveTuningAndSuppressions() {
        var rule = new EventDetectionRule(new EventDetectionRuleDefinition {
            RuleId = "tuned", Title = "Tuned", Kind = EventDetectionRuleKind.Threshold, Threshold = 5
        });
        var tuning = new EventDetectionTuning(thresholdOverrides: new Dictionary<string, int> { ["tuned"] = 2 },
            severityOverrides: new Dictionary<string, EventDetectionSeverity> { ["tuned"] = EventDetectionSeverity.High },
            suppressions: new[] { new EventDetectionSuppression("tuned", EventPredicate.Compare("Account", EventPredicateOperator.Equal, "ignored"), reason: "Approved maintenance") });
        EventDetectionPlan original = EventDetectionPlan.Compile(new[] { rule }, tuning);
        EventDetectionPlan restored = EventDetectionPlan.FromJson(original.ToJson());
        Assert.Equal(original.PlanHash, restored.PlanHash);
        Assert.Equal(2, restored.Rules[0].Threshold);
        EventObject source = CreateEvent(1, Utc(10, 0), 1, "Tasks", "Provider"); source.Data["Account"] = "ignored";
        Assert.True(Assert.Single(EventDetectionEngine.Explain(EventObservation.Create(source), restored, EventDetectionCoverage.Create())).Suppressed);
    }

    [Theory]
    [InlineData(true, false, EventDetectionFindingStatus.Matched)]
    [InlineData(false, false, EventDetectionFindingStatus.Incomplete)]
    [InlineData(true, true, EventDetectionFindingStatus.Incomplete)]
    public void AbsenceRequiresCompleteScopeAndDeadline(bool complete, bool gap, EventDetectionFindingStatus expected) {
        EventObject source = CreateEvent(1, Utc(10, 0), 1, "Tasks", "Provider");
        source.Data["TaskId"] = "42";
        var receipt = new EventCoverageWindow { ScopeIdentity = "server01/tasks/all", StartUtc = Utc(10, 0),
            EndUtc = gap ? Utc(10, 4) : Utc(10, 5), IsComplete = complete };
        EventDetectionEngineOptions options = new EventDetectionEngineOptions(coverage: EventDetectionCoverage.Create())
            .WithAbsenceWindow(new EventDetectionAbsenceWindow(Utc(10, 10), new[] { receipt }));
        EventDetectionFinding finding = Assert.Single(EventDetectionEngine.Evaluate(new[] { source }, AbsencePlan(), options).Findings);
        Assert.Equal(expected, finding.Status);
        Assert.Equal(Utc(10, 5), finding.EndTimeUtc);
    }

    [Fact]
    public void CompletionAcknowledgesOneMatchingTaskAndLeavesOtherGroupsPending() {
        EventObject[] sources = { CreateEvent(1, Utc(10, 0), 1, "Tasks", "Provider"),
            CreateEvent(1, Utc(10, 1), 2, "Tasks", "Provider"), CreateEvent(2, Utc(10, 2), 3, "Tasks", "Provider") };
        sources[0].Data["TaskId"] = "42"; sources[1].Data["TaskId"] = "43"; sources[2].Data["TaskId"] = "42";
        EventDetectionFinding finding = Assert.Single(EventDetectionEngine.Evaluate(sources, AbsencePlan()).Findings);
        Assert.Equal("43", finding.Entities["TaskId"]);
        Assert.Equal(EventDetectionFindingStatus.Incomplete, finding.Status);
    }

    [Fact]
    public void SessionReplaysExactPlanClocksFieldsAndRejectsChangedEvidence() {
        string root = Path.Combine(Path.GetTempPath(), "evx-investigation-" + Guid.NewGuid().ToString("N"));
        try {
            EventObject source = CreateEvent(1, Utc(10, 0), 1, "Tasks", "Provider");
            source.Data["TaskId"] = "42";
            EventObservation observation = EventObservation.Create(source, receivedTimeUtc: Utc(10, 1), processedTimeUtc: Utc(10, 2));
            EventInvestigationSession created = EventInvestigationSession.Create(root, new EventInvestigationManifest {
                StartUtc = Utc(10, 0), EndUtc = Utc(10, 10), QueryIdentity = "all-tasks-v1",
                ParserVersions = new() { ["fixture"] = "1" },
                Sources = new[] { new EventCoverageWindow { ScopeIdentity = "server01/tasks/all", StartUtc = Utc(10, 0), EndUtc = Utc(10, 10), IsComplete = true } }
            }, new[] { observation }, AbsencePlan(), coverage: EventDetectionCoverage.Create());
            EventInvestigationSession reopened = EventInvestigationSession.Open(root);
            EventDetectionExecutionResult result = reopened.Replay();
            EventObservation restored = Assert.Single(result.Observations);
            Assert.Equal(observation.Identity, restored.Identity);
            Assert.Equal(observation.CollectorComputer, restored.CollectorComputer);
            Assert.Equal(observation.ReceivedTimeUtc, restored.ReceivedTimeUtc);
            Assert.Equal(observation.ProcessedTimeUtc, restored.ProcessedTimeUtc);
            Assert.Equal("42", restored.Fields["TaskId"]);
            Assert.Equal(EventDetectionFindingStatus.Matched, Assert.Single(result.Findings).Status);
            Assert.Equal(created.Manifest.PlanHash, reopened.Manifest.PlanHash);
            File.AppendAllText(Path.Combine(root, "observations.jsonl"), "tampered");
            Assert.Throws<InvalidDataException>(() => reopened.Replay());
        } finally { if (Directory.Exists(root)) { Directory.Delete(root, true); } }
    }

    [Fact]
    public void ImpactPreviewUsesSameBoundedHistoryAndShowsNewSourceRequirements() {
        EventDetectionPlan Plan(int id) => EventDetectionPlan.Compile(new[] {
            new EventDetectionRule(new EventDetectionRuleDefinition { RuleId = "test", Title = "Test", EventIds = new[] { id } }) });
        EventObservation[] observations = Enumerable.Range(1, 3).Select(id => EventObservation.Create(
            CreateEvent(id, Utc(10, id), id, "Tasks", "Provider"))).ToArray();
        EventDetectionImpactPreview preview = EventDetectionEngine.PreviewChanges(observations, Plan(1), Plan(2),
            new EventDetectionEngineOptions(coverage: EventDetectionCoverage.Create()), maximumObservations: 2);
        Assert.False(preview.IsComplete);
        Assert.Single(preview.Appearing);
        Assert.Single(preview.Disappearing);
        Assert.Contains("EventId:2", preview.NewSourceRequirements);
        Assert.Equal(0, preview.FindingCountChange);
    }

    [Fact]
    public void TimelineClockBoundsPreserveRawTimesAndRequireIndependentEvidence() {
        EventObservation[] observations = new[] { Utc(10, 0), Utc(10, 1) }.Select((time, index) => EventObservation.Create(
            CreateEvent(1, time, index + 1, "Tasks", "Provider"), receivedTimeUtc: time.AddHours(2), processedTimeUtc: time.AddHours(2))).ToArray();
        EventTimeline unknown = EventTimelineEngine.Create(observations, null);
        Assert.Equal(EventClockOrder.Ambiguous, unknown.Entries[0].CompareClockOrder(unknown.Entries[1]));
        EventTimeline known = EventTimelineEngine.Create(observations, null, null, new[] {
            new EventClockEvidence("server01", Utc(9, 0), Utc(11, 0), TimeSpan.FromSeconds(-2), TimeSpan.FromSeconds(5), "Independent clock sample") });
        Assert.Equal(EventClockOrder.Before, known.Entries[0].CompareClockOrder(known.Entries[1]));
        Assert.Equal(Utc(10, 0), known.Entries[0].EventTimeUtc);
        Assert.Equal(Utc(10, 0).AddSeconds(-7), known.Entries[0].ClockInterval!.EarliestUtc);
    }
}
