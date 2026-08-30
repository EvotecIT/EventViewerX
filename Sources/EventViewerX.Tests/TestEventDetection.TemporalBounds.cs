using Xunit;

namespace EventViewerX.Tests;

public sealed partial class TestEventDetection {
    [Fact]
    public void TemporalRulesRejectStepCountsThatExceedTheBoundedMatcherContract() {
        EventDetectionStepDefinition[] steps = Enumerable.Range(0, 65)
            .Select(index => new EventDetectionStepDefinition {
                Name = "step-" + index,
                EventIds = new[] { 2000 + index }
            })
            .ToArray();

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            EventDetectionPlan.Compile(new[] {
                new EventDetectionRule(new EventDetectionRuleDefinition {
                    RuleId = "EVX-TEST-TEMPORAL-STEP-BOUND",
                    Title = "Temporal step bound",
                    Kind = EventDetectionRuleKind.Temporal,
                    Window = TimeSpan.FromMinutes(5),
                    Steps = steps
                })
            }));

        Assert.Contains("more than 64 steps", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnorderedTemporalMatcherHandlesTheMaximumLengthAugmentingPathIteratively() {
        const int stepCount = 64;
        EventDetectionStepDefinition[] steps = Enumerable.Range(0, stepCount)
            .Select(index => new EventDetectionStepDefinition {
                Name = "step-" + index,
                EventIds = index == stepCount - 1
                    ? new[] { 2000 + index }
                    : new[] { 2000 + index, 2001 + index }
            })
            .ToArray();
        EventDetectionPlan plan = EventDetectionPlan.Compile(new[] {
            new EventDetectionRule(new EventDetectionRuleDefinition {
                RuleId = "EVX-TEST-TEMPORAL-ITERATIVE-PATH",
                Title = "Iterative temporal assignment",
                Kind = EventDetectionRuleKind.Temporal,
                Window = TimeSpan.FromMinutes(5),
                GroupBy = "Account",
                Steps = steps
            })
        });
        EventObservation[] observations = Enumerable.Range(0, stepCount)
            .Select(index => Observe(
                2000 + (stepCount - 1 - index),
                Utc(10, 0).AddSeconds(index),
                index + 1,
                "alice"))
            .ToArray();

        EventDetectionFinding finding = Assert.Single(EventDetectionEngine.Stream(observations, plan));

        Assert.Equal(stepCount, finding.Evidence.Count);
        Assert.Equal(
            Enumerable.Range(1, stepCount).Select(static value => (long?)value),
            finding.Evidence.Select(static item => item.RecordId));
    }

    [Fact]
    public void UnorderedTemporalCandidateMetadataCountsTowardTheStateByteLimit() {
        EventObservation observation = Observe(1001, Utc(10, 0), 1, "alice");
        EventDetectionPlan thresholdPlan = EventDetectionPlan.Compile(new[] {
            Rule("EVX-TEST-STATE-BASELINE", EventDetectionRuleKind.Threshold, threshold: 2, groupBy: "Account")
        });
        EventDetectionPlan temporalPlan = EventDetectionPlan.Compile(new[] {
            TemporalRule("EVX-TEST-STATE-TEMPORAL", EventDetectionRuleKind.Temporal)
        });

        long thresholdBudget = FindMinimumAcceptedStateBudget(thresholdPlan, observation);
        long temporalBudget = FindMinimumAcceptedStateBudget(temporalPlan, observation);

        Assert.True(temporalBudget > thresholdBudget);
    }

    [Fact]
    public void HallDeficientTemporalGraphsStopAtTheCumulativeMatchingWorkLimit() {
        const int stepCount = 64;
        const int selectableSteps = 9;
        const int candidateCount = (1 << selectableSteps) - 1;
        EventDetectionStepDefinition[] steps = Enumerable.Range(0, stepCount)
            .Select(stepIndex => new EventDetectionStepDefinition {
                Name = "step-" + stepIndex,
                EventIds = stepIndex < selectableSteps
                    ? Enumerable.Range(1, candidateCount)
                        .Where(mask => (mask & (1 << stepIndex)) != 0)
                        .Select(static mask => 3000 + mask)
                        .ToArray()
                    : new[] { 10000 + stepIndex }
            })
            .ToArray();
        EventDetectionPlan plan = EventDetectionPlan.Compile(new[] {
            new EventDetectionRule(new EventDetectionRuleDefinition {
                RuleId = "EVX-TEST-TEMPORAL-WORK-BOUND",
                Title = "Bound Hall-deficient temporal matching",
                Kind = EventDetectionRuleKind.Temporal,
                Window = TimeSpan.FromMinutes(5),
                GroupBy = "Account",
                Steps = steps
            })
        });
        EventObservation[] observations = Enumerable.Range(1, candidateCount)
            .Select(mask => Observe(
                3000 + mask,
                Utc(10, 0).AddMilliseconds(mask),
                mask,
                "alice"))
            .ToArray();

        EventDetectionFinding incomplete = Assert.Single(EventDetectionEngine.Stream(observations, plan));

        Assert.Equal(EventDetectionFindingStatus.Incomplete, incomplete.Status);
        Assert.Contains("matching work limit", incomplete.CompletenessDiagnostic, StringComparison.Ordinal);
    }

    private static long FindMinimumAcceptedStateBudget(
        EventDetectionPlan plan,
        EventObservation observation) {

        long low = 1;
        long high = 1024L * 1024L;
        while (low < high) {
            long middle = low + ((high - low) / 2);
            bool rejected = EventDetectionEngine.Stream(
                    new[] { observation },
                    plan,
                    new EventDetectionEngineOptions(maximumStateBytes: middle))
                .Any(static finding => finding.Status == EventDetectionFindingStatus.Incomplete);
            if (rejected) {
                low = middle + 1;
            } else {
                high = middle;
            }
        }
        return low;
    }
}
