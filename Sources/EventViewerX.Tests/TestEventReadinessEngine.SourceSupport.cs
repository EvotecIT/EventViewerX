using Xunit;

namespace EventViewerX.Tests;

public sealed partial class TestEventReadinessEngine {
    [Theory]
    [InlineData(EventType.SqlDatabaseCreated)]
    [InlineData(EventType.NetworkSecurity)]
    public void UnavailableSourcesFailReadinessBeforeMachineQueries(EventType type) {
        var evidence = new FakeEvidenceProvider();
        EventReadinessReport report = EventReadinessEngine.Evaluate(
            new EventReadinessRequest { Types = new[] { type } }, evidence, CancellationToken.None);
        Assert.False(report.IsReady);
        Assert.Contains(report.RequiredFailures, check => check.Check == "RuleCatalog" &&
            check.DiagnosticKind == EventReadinessDiagnosticKind.InvalidConfiguration);
        Assert.Empty(evidence.ProbeCalls);
        Assert.Equal(0, evidence.AuditQueryCount);
        Assert.Equal(0, evidence.ConfigurationReadCount);
    }
}