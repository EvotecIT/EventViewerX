using EventViewerX.Reporting;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestKerberosRc4Impact {
    [Fact]
    public void GroupsAuditBlocksAndExplicitDefaultsPerControllerWithoutClaimingDomainCoverage() {
        KerberosRc4ImpactAccumulator accumulator = KerberosRc4ImpactEngine.CreateAccumulator(maximumEvidencePerGroup: 1);
        accumulator.Add(Row(201, "DC01", "client$", "svc", "first"));
        accumulator.Add(Row(201, "DC01", "client$", "svc", "second"));
        accumulator.Add(Row(203, "DC01", "client$", "svc", "third"));
        accumulator.Add(Row(205, "DC02", string.Empty, string.Empty, "fourth"));
        accumulator.Add(Row(201, "DC03", "client$", "svc", "ignored", provider: "Other"));

        KerberosRc4ImpactReport report = accumulator.Complete(false, "The stored result limit was reached.");

        Assert.Equal(4, report.EventsObserved);
        Assert.Equal(3, report.Groups.Count);
        KerberosRc4ImpactGroup warning = Assert.Single(report.Groups, static group =>
            group.Impact == KerberosRc4ImpactState.MayFailUnderEnforcement);
        Assert.Equal(2, warning.EventCount);
        Assert.Equal("DC01", warning.DomainController);
        Assert.Equal("client$", warning.AccountName);
        Assert.Equal("svc", warning.ServiceName);
        Assert.Equal(KerberosKdcRc4Issue.ClientOnlySupportsInsecureEncryption, warning.Issue);
        Assert.Single(warning.EvidenceIdentities);
        Assert.True(warning.EvidenceTruncated);
        Assert.Contains(report.Groups, static group => group.Impact == KerberosRc4ImpactState.AlreadyBlocked);
        Assert.Contains(report.Groups, static group =>
            group.Impact == KerberosRc4ImpactState.ExplicitInsecureDefault && group.DomainController == "DC02");
        Assert.False(report.SelectedWindowComplete);
        Assert.Contains("every domain controller", report.CoverageStatement, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyEventWindowDoesNotImplyEnforcementReadiness() {
        KerberosRc4ImpactReport report = KerberosRc4ImpactEngine.CreateAccumulator().Complete(true);
        Assert.Empty(report.Groups);
        Assert.Equal(0, report.EventsObserved);
        Assert.Contains("Verify collection", report.CoverageStatement, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportAnalysisRetainsFailedDomainControllerCoverage() {
        EventReportRow row = Row(201, "DC01", "client$", "svc", "one");
        EventReport source = EventReportEngine.CreateStored(
            new[] { row },
            new[] { EventReportSectionSchema.FromType(EventType.KerberosKdcRc4Audit) },
            coverage: new[] {
                new EventReportCoverage {
                    MachineName = "DC02",
                    LogName = "System",
                    Succeeded = false,
                    Status = "AccessDenied",
                    Detail = "The channel could not be read"
                }
            },
            completenessDiagnostic: "Collection did not finish.");

        KerberosRc4ImpactReport impact = KerberosRc4ImpactEngine.Analyze(source);

        Assert.False(impact.SelectedWindowComplete);
        Assert.Single(impact.Groups);
        Assert.Contains("DC02/System", impact.CompletenessDiagnostic, StringComparison.Ordinal);
        Assert.Contains("AccessDenied", impact.CompletenessDiagnostic, StringComparison.Ordinal);
        Assert.Contains("Collection did not finish", impact.CompletenessDiagnostic, StringComparison.Ordinal);
    }

    private static EventReportRow Row(int eventId, string controller, string account, string service,
        string identity, string provider = "Kdcsvc") => new() {
        Type = nameof(EventType.KerberosKdcRc4Audit),
        EventId = eventId,
        Provider = provider,
        SourceLog = "System",
        SourceComputer = controller,
        ObservationIdentity = identity,
        TimeCreated = new DateTime(2026, 9, 22, 10, 0, 0, DateTimeKind.Utc),
        Values = new Dictionary<string, object?> {
            ["AccountName"] = account,
            ["ServiceName"] = service,
            ["ServiceSid"] = "S-1-5-21-1"
        }
    };
}
