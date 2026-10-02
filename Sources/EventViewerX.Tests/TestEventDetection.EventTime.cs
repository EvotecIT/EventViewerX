using Xunit;

namespace EventViewerX.Tests;

public sealed partial class TestEventDetection {
    [Fact]
    public async Task ReorderWindowRestoresOrderedTemporalCorrelationAndFlushesAtCompletion() {
        EventDetectionPlan plan = EventDetectionPlan.Compile(new[] {
            TemporalRule("EVX-TEST-REORDER", EventDetectionRuleKind.OrderedTemporal)
        });
        EventObservation[] arriving = {
            Observe(1002, Utc(10, 0).AddSeconds(10), 2, "alice"),
            Observe(1001, Utc(10, 0), 1, "alice")
        };
        var options = new EventDetectionEngineOptions(new EventTimeOrderingOptions(TimeSpan.FromSeconds(30)));

        Assert.Empty(EventDetectionEngine.Stream(arriving, plan));
        EventDetectionFinding ordered = Assert.Single(EventDetectionEngine.Stream(arriving, plan, options));
        Assert.Equal(new long?[] { 1, 2 }, ordered.Evidence.Select(static observation => observation.RecordId));
        var asyncFindings = new List<EventDetectionFinding>();
        await foreach (EventDetectionFinding finding in EventDetectionEngine.StreamAsync(AsObservations(), plan, options)) {
            asyncFindings.Add(finding);
        }
        Assert.Equal(ordered.EvidenceIdentities, Assert.Single(asyncFindings).EvidenceIdentities);

        async IAsyncEnumerable<EventObservation> AsObservations() {
            foreach (EventObservation observation in arriving) {
                await Task.Yield();
                yield return observation;
            }
        }
    }

    [Theory]
    [InlineData(EventLateArrivalPolicy.SkipWithDiagnostic)]
    [InlineData(EventLateArrivalPolicy.Throw)]
    public void EventsBeyondTheWatermarkFollowTheExplicitLatenessPolicy(EventLateArrivalPolicy policy) {
        EventDetectionPlan plan = EventDetectionPlan.Compile(new[] { Rule("EVX-TEST-LATE") });
        EventObservation[] arriving = {
            Observe(1001, Utc(10, 0), 1, "alice"),
            Observe(1001, Utc(10, 2), 2, "alice"),
            Observe(1001, Utc(10, 1), 3, "alice")
        };
        var options = new EventDetectionEngineOptions(new EventTimeOrderingOptions(
            TimeSpan.FromSeconds(30), lateArrivalPolicy: policy));
        if (policy == EventLateArrivalPolicy.Throw) {
            Assert.Throws<InvalidDataException>(() => EventDetectionEngine.Stream(arriving, plan, options).ToArray());
        } else {
            EventDetectionFinding[] findings = EventDetectionEngine.Stream(arriving, plan, options).ToArray();
            EventDetectionFinding diagnostic = Assert.Single(findings, static finding =>
                finding.Status == EventDetectionFindingStatus.Incomplete);
            Assert.Equal(3, Assert.Single(diagnostic.Evidence).RecordId);
            Assert.Contains("watermark", diagnostic.CompletenessDiagnostic, StringComparison.Ordinal);
            Assert.Equal(new long?[] { 1, 2 }, findings.Where(static finding =>
                finding.Status == EventDetectionFindingStatus.Matched).Select(static finding => finding.Evidence[0].RecordId));
        }
    }

    [Theory]
    [InlineData(1, 1048576)]
    [InlineData(100, 1)]
    public void ReorderBufferBoundsProduceIncompleteEvidence(int maximumCount, long maximumBytes) {
        EventDetectionPlan plan = EventDetectionPlan.Compile(new[] { Rule("EVX-TEST-REORDER-BOUND") });
        EventObservation[] arriving = {
            Observe(1001, Utc(10, 0), 1, "alice"),
            Observe(1001, Utc(10, 0).AddSeconds(1), 2, "alice")
        };
        var options = new EventDetectionEngineOptions(new EventTimeOrderingOptions(
            TimeSpan.FromMinutes(1), maximumCount, maximumBytes));

        EventDetectionFinding[] findings = EventDetectionEngine.Stream(arriving, plan, options).ToArray();
        Assert.Contains(findings, static finding => finding.Status == EventDetectionFindingStatus.Incomplete &&
            finding.CompletenessDiagnostic!.Contains("reorder buffer limit", StringComparison.Ordinal));
        Assert.Equal(maximumCount == 1 ? 1 : 0,
            findings.Count(static finding => finding.Status == EventDetectionFindingStatus.Matched));
    }
}