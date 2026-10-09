using EventViewerX.Reporting;
using System.Text.RegularExpressions;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEndpointReportNavigation {
    [Fact]
    public void FindingsLinkToKnownEvidenceAndRetainUnknownArtifactCoordinates() {
        var analysis = new EventEndpointAnalysis {
            Records = new[] { new EventDiagnosticRecord { Identity = "log-id", Source = "installer.log", LineStart = 7, LineEnd = 9, ByteStart = 120, ByteEnd = 240, Message = "private-log-message", RawText = "private-log-frame" } },
            IdentitySnapshots = new[] { new EventDsRegSnapshot { EvidenceIdentity = "identity-id", Fields = new[] { new EventDsRegField { Name = "TenantId", Value = "private-tenant-value" } } } },
            Findings = new[] { new EventDiagnosticFinding { Title = "Installation failed", EvidenceIdentities = new[] { "log-id", "identity-id", "unavailable-artifact" } } }
        };

        string html = EventEndpointHtmlRenderer.Render(analysis);
        Assert.Contains("data-hfx-monitoring-record-link=\"evidence-log-id\"", html);
        Assert.Contains("installer.log: lines 7-9, bytes 120-240", html);
        Assert.Contains("data-hfx-monitoring-record-link=\"artifact-identity-id\"", html);
        Assert.Contains("SHA-256: unavailable-artifact", html);
        Assert.DoesNotContain("data-hfx-monitoring-record-link=\"artifact-unavailable-artifact\"", html);
        foreach (string raw in new[] { "private-log-message", "private-log-frame", "private-tenant-value" }) {
            Assert.DoesNotContain(raw, html);
            Assert.Contains(raw, EventEndpointHtmlRenderer.Render(analysis, includeSensitiveEvidence: true));
        }
        string sensitive = EventEndpointHtmlRenderer.Render(analysis, includeSensitiveEvidence: true);
        Match preset = Regex.Match(sensitive, "data-hfx-monitoring-column-group=\"All evidence\" data-hfx-monitoring-column-state=\"([^\"]*)\"");
        Assert.Contains("message", preset.Groups[1].Value.Split(','));
    }

    [Fact]
    public void RepeatedArtifactBytesHaveOneNavigationTargetAndKeepAllCaptureContexts() {
        DateTimeOffset first = DateTimeOffset.Parse("2026-10-09T10:00:00Z"), second = first.AddMinutes(5);
        var analysis = new EventEndpointAnalysis {
            IdentitySnapshots = new[] {
                new EventDsRegSnapshot { EvidenceIdentity = "shared-id", ExecutionContext = "SYSTEM", CapturedAt = first },
                new EventDsRegSnapshot { EvidenceIdentity = "shared-id", ExecutionContext = "SYSTEM", CapturedAt = second },
                new EventDsRegSnapshot { EvidenceIdentity = "shared-id", ExecutionContext = "User", CapturedAt = second }
            },
            Findings = new[] { new EventDiagnosticFinding { EvidenceIdentities = new[] { "shared-id" } } }
        };

        string html = EventEndpointHtmlRenderer.Render(analysis);
        Assert.Contains("data-hfx-monitoring-record-link=\"artifact-shared-id\"", html);
        Assert.Single(Regex.Matches(html, "data-hfx-monitoring-record-row=\"artifact-shared-id\""));
        Assert.Contains("SYSTEM, User", html);
        Assert.Contains(first.ToString("O"), html);
        Assert.Contains(second.ToString("O"), html);
        Assert.Contains("SYSTEM at " + first.ToString("O"), html);
        Assert.Contains("SYSTEM at " + second.ToString("O"), html);
        Assert.Contains("User at " + second.ToString("O"), html);
    }
}
