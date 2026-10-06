using Xunit;

namespace EventViewerX.Tests;

public sealed partial class TestEventDetection {
    [Fact]
    public void RegexTimeoutMarksExecutionIncompleteAndDisablesOnlyTheFailedRule() {
        EventObject source = CreateEvent(4624, Utc(10, 0), 1, "Security", "Provider");
        source.Data["Payload"] = new string('a', 30000) + "!";
        var failing = new EventDetectionRule(new EventDetectionRuleDefinition {
            RuleId = "timeout", Title = "Timed out predicate",
            Predicate = EventPredicate.Compare("Payload", EventPredicateOperator.MatchesRegex, "^(a+)+$")
        });
        var healthy = new EventDetectionRule(new EventDetectionRuleDefinition { RuleId = "healthy", Title = "Healthy rule" });
        EventDetectionExecutionResult result = EventDetectionEngine.Evaluate(
            new[] { source, source }, EventDetectionPlan.Compile(new[] { failing, healthy }),
            new EventDetectionEngineOptions(coverage: EventDetectionCoverage.Create()));
        Assert.False(result.IsComplete);
        Assert.Single(result.Findings, finding => finding.Status == EventDetectionFindingStatus.Error);
        Assert.Equal(2, result.Findings.Count(finding => finding.RuleId == "healthy"));
        Assert.Contains("RegexMatchTimeoutException", result.Findings.Single(finding => finding.RuleId == "timeout").CompletenessDiagnostic);
        EventDetectionRuleTrace trace = Assert.Single(EventDetectionEngine.Explain(
            EventObservation.Create(source), EventDetectionPlan.Compile(new[] { failing }), EventDetectionCoverage.Create()));
        Assert.Contains("error", trace.Outcome, StringComparison.OrdinalIgnoreCase);
    }
}
