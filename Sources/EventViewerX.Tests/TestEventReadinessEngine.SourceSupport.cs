using Xunit;

namespace EventViewerX.Tests;

public sealed partial class TestEventReadinessEngine {
    [Theory]
    [InlineData(EventType.SqlDatabaseCreated, false)]
    [InlineData(EventType.SqlDatabaseCreated, true)]
    [InlineData(EventType.NetworkSecurity, false)]
    public void UnavailableSourcesFailReadinessBeforeMachineQueries(EventType type, bool includeSupported) {
        var evidence = new FakeEvidenceProvider();
        EventReadinessReport report = EventReadinessEngine.Evaluate(
            new EventReadinessRequest { Types = includeSupported ? new[] { type, EventType.ADUserLogon } : new[] { type } }, evidence, CancellationToken.None);
        Assert.False(report.IsReady);
        Assert.False(report.IsComplete);
        Assert.Contains(report.Checks, check => check.Required && check.Status == EventReadinessStatus.Skipped);
        Assert.Contains(report.RequiredFailures, check => check.Check == "RuleCatalog" &&
            check.DiagnosticKind == EventReadinessDiagnosticKind.InvalidConfiguration);
        Assert.Empty(evidence.ProbeCalls);
        Assert.Equal(0, evidence.TargetDiscoveryCount);
        Assert.Equal(0, evidence.AuditQueryCount);
        Assert.Equal(0, evidence.ConfigurationReadCount);
    }
}