using Xunit;

namespace EventViewerX.Tests;

public sealed partial class TestEventDetection {
    [Fact]
    public void DuplicateDistinctBurstRetainsLatestEvidenceWithoutExhaustingObservationBudget() {
        EventObservation[] repeated = Enumerable.Range(1, 500)
            .Select(index => WithField(Observe(1001, Utc(10, 0).AddTicks(index), index, "alice"),
                "SourceAddress", index % 2 == 0 ? "ÅLPHA" : "ålpha")).ToArray();
        EventObservation second = WithField(Observe(1001, Utc(10, 1), 501, "alice"), "SourceAddress", "beta");

        EventDetectionFinding finding = Assert.Single(EventDetectionEngine.Stream(
            repeated.Concat(new[] { second }), DistinctPlan(2),
            new EventDetectionEngineOptions(maximumStateObservations: 2)));

        Assert.Equal(EventDetectionFindingStatus.Matched, finding.Status);
        Assert.Equal(new long?[] { 500, 501 }, finding.Evidence.Select(static item => item.RecordId));
    }

    [Fact]
    public void DistinctLateDuplicatesCannotReplaceNewerEvidenceAndBoundaryIsInclusive() {
        EventObservation[] observations = {
            WithField(Observe(1001, Utc(10, 5), 5, "alice"), "SourceAddress", "alpha"),
            WithField(Observe(1001, Utc(10, 1), 1, "alice"), "SourceAddress", "ALPHA"),
            WithField(Observe(1001, Utc(9, 59), 9, "alice"), "SourceAddress", "expired"),
            WithField(Observe(1001, Utc(10, 0), 2, "alice"), "SourceAddress", "beta"),
            WithField(Observe(1001, Utc(10, 4), 4, "alice"), "SourceAddress", "gamma")
        };

        EventDetectionFinding finding = Assert.Single(EventDetectionEngine.Stream(observations, DistinctPlan(3),
            new EventDetectionEngineOptions(maximumStateObservations: 3)));

        Assert.Equal(new long?[] { 2, 4, 5 }, finding.Evidence.Select(static item => item.RecordId));
        Assert.Equal(TimeSpan.FromMinutes(5), finding.EndTimeUtc - finding.StartTimeUtc);
    }

    [Fact]
    public void DistinctExpiryAndCompletedGroupsReleaseTheSharedStateBudget() {
        EventObservation[] observations = {
            WithField(Observe(1001, Utc(10, 0), 1, "alice"), "SourceAddress", "expired"),
            WithField(Observe(1001, Utc(10, 4), 2, "alice"), "SourceAddress", "alpha"),
            WithField(Observe(1001, Utc(10, 6), 3, "alice"), "SourceAddress", "beta"),
            WithField(Observe(1001, Utc(10, 7), 4, "alice"), "SourceAddress", "gamma"),
            WithField(Observe(1001, Utc(10, 8), 5, "bob"), "SourceAddress", "alpha"),
            WithField(Observe(1001, Utc(10, 8), 6, "bob"), "SourceAddress", "beta"),
            WithField(Observe(1001, Utc(10, 8), 7, "bob"), "SourceAddress", "gamma")
        };

        EventDetectionFinding[] findings = EventDetectionEngine.Stream(observations, DistinctPlan(3),
            new EventDetectionEngineOptions(maximumGroups: 1, maximumStateObservations: 3)).ToArray();

        Assert.Equal(2, findings.Length);
        Assert.All(findings, static finding => Assert.Equal(EventDetectionFindingStatus.Matched, finding.Status));
        Assert.Equal(new long?[] { 2, 3, 4 }, findings[0].Evidence.Select(static item => item.RecordId));
        Assert.Equal(new long?[] { 5, 6, 7 }, findings[1].Evidence.Select(static item => item.RecordId));
    }

    [Fact]
    public void DistinctReplacementStillReportsInsufficientByteBudget() {
        EventObservation first = WithField(Observe(1001, Utc(10, 0), 1, "alice"), "SourceAddress", "alpha");
        EventObservation oversized = WithField(
            WithField(Observe(1001, Utc(10, 1), 2, "alice"), "SourceAddress", "alpha"),
            "Payload", new string('x', 20_000));

        EventDetectionFinding finding = Assert.Single(EventDetectionEngine.Stream(new[] { first, oversized },
            DistinctPlan(2), new EventDetectionEngineOptions(maximumStateBytes: 10_000)));

        Assert.Equal(EventDetectionFindingStatus.Incomplete, finding.Status);
        Assert.Contains("MaximumStateBytes=10000", finding.CompletenessDiagnostic);
        Assert.Equal(oversized.Identity, Assert.Single(finding.EvidenceIdentities));
    }

    private static EventDetectionPlan DistinctPlan(int threshold) => EventDetectionPlan.Compile(new[] {
        new EventDetectionRule(new EventDetectionRuleDefinition {
            RuleId = "EVX-DISTINCT-CONTRACT", Title = "Distinct source contract",
            Kind = EventDetectionRuleKind.DistinctValue, EventIds = new[] { 1001 },
            Threshold = threshold, Window = TimeSpan.FromMinutes(5),
            GroupBy = "Account", DistinctBy = "SourceAddress"
        })
    });
}
