using Xunit;

namespace EventViewerX.Tests;

public sealed partial class TestEventDetection {
    [Fact]
    public void ReplayObservationLimitSurvivesRepeatedRestartsAndBoundaryDuplicates() {
        EventDetectionPlan plan = EventDetectionPlan.Compile(new[] { new EventDetectionRule(new EventDetectionRuleDefinition {
            RuleId = "bounded", Title = "Bounded", EventIds = new[] { 1 }
        }) });
        var options = new EventDetectionEngineOptions(maximumObservations: 2);
        var session = new EventDetectionReplaySession(plan, "query", "generation", options);
        for (int index = 0; index < 2; index++) {
            EventObservation observation = EventObservation.Create(CreateEvent(1, Utc(10, index), index + 1, "Tasks", "Provider"));
            Assert.Equal(EventDetectionFindingStatus.Matched, Assert.Single(session.Process(observation)).Status);
            string checkpoint = session.ExportCheckpoint();
            Assert.Throws<InvalidDataException>(() => EventDetectionReplaySession.Restore(checkpoint, plan, "query", "generation",
                new EventDetectionEngineOptions(maximumObservations: 3)));
            session = EventDetectionReplaySession.Restore(checkpoint, plan, "query", "generation", options);
            Assert.Empty(session.Process(observation));
            Assert.Equal(index + 1, session.ProcessedObservations);
        }
        EventDetectionFinding limit = Assert.Single(session.Process(EventObservation.Create(CreateEvent(1, Utc(10, 2), 3, "Tasks", "Provider"))));
        Assert.Equal(EventDetectionFindingStatus.Incomplete, limit.Status);
        Assert.True(session.IsInvalidated);
        Assert.Throws<InvalidOperationException>(() => session.ExportCheckpoint());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ImpactUsesEffectiveStepSourceRequirements(bool unrestrictedStep) {
        EventDetectionPlan previous = EventDetectionPlan.Compile(new[] { new EventDetectionRule(new EventDetectionRuleDefinition {
            RuleId = "old", Title = "Old", EventIds = new[] { 1 }, Channels = new[] { "Old" }, Providers = new[] { "OldProvider" }
        }) });
        EventDetectionPlan current = EventDetectionPlan.Compile(new[] { new EventDetectionRule(new EventDetectionRuleDefinition {
            RuleId = "steps", Title = "Steps", Kind = EventDetectionRuleKind.Temporal, Window = TimeSpan.FromMinutes(5),
            Steps = new[] {
                new EventDetectionStepDefinition { Name = "Start", EventIds = new[] { 1 }, Channels = new[] { "Tasks" }, Providers = new[] { "Provider" } },
                new EventDetectionStepDefinition { Name = "End", EventIds = new[] { 2 },
                    Channels = unrestrictedStep ? Array.Empty<string>() : new[] { "Tasks" },
                    Providers = unrestrictedStep ? Array.Empty<string>() : new[] { "Provider" } }
            }
        }) });
        var preview = EventDetectionEngine.PreviewChanges(Array.Empty<EventObservation>(), previous, current);
        Assert.Equal(unrestrictedStep, preview.NewSourceRequirements.Contains("Channel:*"));
        Assert.Equal(unrestrictedStep, preview.NewSourceRequirements.Contains("Provider:*"));
        if (!unrestrictedStep) {
            Assert.Contains("Channel:Tasks", preview.NewSourceRequirements);
            Assert.Contains("Provider:Provider", preview.NewSourceRequirements);
        }
        Assert.Contains("EventId:2", preview.NewSourceRequirements);
    }
}
