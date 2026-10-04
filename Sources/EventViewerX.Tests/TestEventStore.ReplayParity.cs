using EventViewerX.Reporting;
using EventViewerX.Storage;
using Xunit;

namespace EventViewerX.Tests;

public sealed partial class TestEventStore {
    [Theory]
    [InlineData(EventDetectionRuleKind.Threshold)]
    [InlineData(EventDetectionRuleKind.DistinctValue)]
    [InlineData(EventDetectionRuleKind.Temporal)]
    [InlineData(EventDetectionRuleKind.OrderedTemporal)]
    public async Task HistoricalReplayPreservesGroupsConsumedBeforeTheRequestedWindow(EventDetectionRuleKind kind) {
        string path = CreateStorePath();
        try {
            int[] minutes = { 4, 6, 7, 9, 10, 11 };
            EventObject[] events = minutes.Select((minute, index) => {
                EventObject item = CreateHistoricalEvent(index + 1, minute, "alice");
                item.Data["Value"] = (index % 3).ToString();
                return item;
            }).ToArray();
            var definition = new EventDetectionRuleDefinition {
                RuleId = "EVX-REPLAY-PARITY",
                Title = "Consumed evidence parity",
                Kind = kind,
                EventIds = new[] { 1001 },
                Threshold = 3,
                Window = TimeSpan.FromMinutes(5),
                GroupBy = "Account",
                DistinctBy = kind == EventDetectionRuleKind.DistinctValue ? "Value" : null,
                Steps = kind is EventDetectionRuleKind.Temporal or EventDetectionRuleKind.OrderedTemporal
                    ? Enumerable.Range(1, 3).Select(index => new EventDetectionStepDefinition {
                        Name = "Step" + index,
                        EventIds = new[] { 1001 }
                    }).ToArray()
                    : Array.Empty<EventDetectionStepDefinition>()
            };
            EventDetectionPlan plan = EventDetectionPlan.Compile(new[] { new EventDetectionRule(definition) });
            var store = new EventStore(path);
            await store.WriteAsync(EventReportEngine.Create(events.Cast<object>()));
            DateTime start = events[4].TimeCreated;
            EventDetectionExecutionResult continuous = EventDetectionEngine.Evaluate(
                events.Select(static item => EventObservation.Create(item)), plan);
            EventDetectionExecutionResult replay = await store.EvaluateDetectionAsync(
                new EventStoreQuery { StartTime = start, EndTime = events[5].TimeCreated }, plan,
                new EventDetectionEngineOptions(coverage: CompleteReplayCoverage(path)));
            EventDetectionFinding[] expected = continuous.Findings.Where(finding =>
                finding.Status == EventDetectionFindingStatus.Matched && finding.EndTimeUtc >= start).ToArray();

            Assert.Single(expected);
            Assert.True(replay.IsComplete);
            EventDetectionFinding actual = Assert.Single(replay.Findings);
            Assert.Equal(expected[0].EvidenceIdentities, actual.EvidenceIdentities);
            Assert.Equal(expected[0].StartTimeUtc, actual.StartTimeUtc);
            Assert.Equal(expected[0].EndTimeUtc, actual.EndTimeUtc);
            Assert.Equal(2, replay.Observations.Count);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task HistoricalReplayReportsAnObservationBoundInsteadOfClaimingAnEmptyWindowIsComplete() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            EventObject[] events = Enumerable.Range(0, 30)
                .Select(minute => CreateHistoricalEvent(minute + 1, minute, "alice")).ToArray();
            await store.WriteAsync(EventReportEngine.Create(events.Cast<object>()));
            EventDetectionPlan plan = EventDetectionPlan.Compile(new[] {
                new EventDetectionRule(new EventDetectionRuleDefinition {
                    RuleId = "EVX-REPLAY-BOUND", Title = "Bounded replay",
                    Kind = EventDetectionRuleKind.Threshold, EventIds = new[] { 1001 },
                    Threshold = 3, Window = TimeSpan.FromMinutes(5)
                })
            });
            EventDetectionExecutionResult replay = await store.EvaluateDetectionAsync(
                new EventStoreQuery { StartTime = events[29].TimeCreated }, plan,
                new EventDetectionEngineOptions(maximumObservations: 5, coverage: CompleteReplayCoverage(path)));

            Assert.Empty(replay.Observations);
            Assert.False(replay.IsComplete);
            Assert.Contains(replay.Findings, finding => finding.Status == EventDetectionFindingStatus.Incomplete &&
                finding.CompletenessDiagnostic.Contains("MaximumObservations"));
        } finally {
            DeleteStore(path);
        }
    }

    private static EventDetectionCoverage CompleteReplayCoverage(string path) => EventDetectionCoverage.Create(
        expectedTargets: new[] { path }, observedTargets: new[] { path },
        expectedChannels: new[] { "Security" }, observedChannels: new[] { "Security" },
        expectedEventIds: new[] { 1001 }, observedEventIds: new[] { 1001 });
}
