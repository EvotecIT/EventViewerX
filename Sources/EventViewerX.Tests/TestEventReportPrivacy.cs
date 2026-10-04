using System.Text.Json;
using EventViewerX.Reporting;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventReportPrivacy {
    [Fact]
    public void OmissionRemovesRawAndAlternateDisclosurePathsAndPreservesCompletion() {
        EventReport original = CreateSensitiveReport();
        EventReport exported = EventReportPrivacy.Apply(original);
        string json = SerializeExport(exported);
        Assert.DoesNotContain("private-account", json);
        Assert.DoesNotContain("private-host", json);
        Assert.DoesNotContain("private-path", json);
        Assert.DoesNotContain("private-detail", json);
        Assert.Empty(exported.Rows[0].Values);
        Assert.Empty(exported.Rows[0].NormalizedValues);
        Assert.Null(exported.Rows[0].ActivityId);
        Assert.False(EventReportSummary.Create(exported).IsComplete);
        Assert.Equal(original.EventsScanned, exported.EventsScanned);
        Assert.Equal("private-account", original.Rows[0].Values["User"]);
        Assert.Equal("private-host", original.Rows[0].SourceComputer);
        Assert.Equal("private-detail", original.Coverage[0].Detail);
    }

    [Fact]
    public void ExplicitPayloadPolicyProducesStableFieldScopedTokensWithoutKeyOrRawValue() {
        EventReport original = CreateSensitiveReport();
        byte[] key = Enumerable.Range(0, 32).Select(static item => (byte)item).ToArray();
        var policy = new EventReportPrivacyOptions {
            RetainedValueFields = new[] { "Count" }, PseudonymizedValueFields = new[] { "User", "OtherUser" },
            PseudonymizeSourceIdentities = true
        };
        EventReport first = EventReportPrivacy.Apply(original, policy, key);
        EventReport second = EventReportPrivacy.Apply(original, policy, key);
        Assert.Equal(3, first.Rows[0].Values["Count"]);
        Assert.Equal(first.Rows[0].Values["User"], second.Rows[0].Values["User"]);
        Assert.NotEqual(first.Rows[0].Values["User"], first.Rows[0].Values["OtherUser"]);
        Assert.StartsWith("hmac-sha256:", first.Rows[0].SourceComputer);
        Assert.DoesNotContain("private-account", SerializeExport(first));
        key[0]++;
        Assert.NotEqual(first.Rows[0].Values["User"], EventReportPrivacy.Apply(original, policy, key).Rows[0].Values["User"]);
        first.Rows[0].SourceComputer = "changed-export";
        Assert.Equal("private-host", original.Rows[0].SourceComputer);
    }

    [Fact]
    public void ContradictoryOrUnsafePoliciesFailBeforeReturningAnExport() {
        EventReport original = CreateSensitiveReport();
        Assert.Throws<ArgumentException>(() => EventReportPrivacy.Apply(original,
            new EventReportPrivacyOptions { PseudonymizedValueFields = new[] { "User" } }, new byte[31]));
        Assert.Throws<ArgumentException>(() => EventReportPrivacy.Apply(original,
            new EventReportPrivacyOptions { RetainedValueFields = new[] { "User" }, PseudonymizedValueFields = new[] { "USER" } }, new byte[32]));
        Assert.Throws<ArgumentException>(() => EventReportPrivacy.Apply(original,
            new EventReportPrivacyOptions { RetainedValueFields = new[] { "Message" } }));
        Assert.Throws<ArgumentException>(() => EventReportPrivacy.Apply(original,
            new EventReportPrivacyOptions { RetainedValueFields = new[] { "Nested" } }));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => EventReportPrivacy.Apply(original, cancellationToken: cancellation.Token));
    }

    [Fact]
    public void LossDiagnosticControlsTheRenderedCompletenessEvenWithoutACapOrSourceFailure() {
        EventReport report = EventReportEngine.CreateStored(Array.Empty<EventReportRow>(),
            new[] { EventReportSectionSchema.CreateGeneric() }, completenessDiagnostic: "A provider record could not be projected.");
        string html = EventReportHtmlRenderer.Render(report);
        Assert.False(report.ScanLimitReached);
        Assert.All(report.Coverage, static source => Assert.True(source.Succeeded));
        Assert.Contains(">Incomplete<", html, StringComparison.Ordinal);
        Assert.Contains("A provider record could not be projected.", html, StringComparison.Ordinal);
    }

    private static string SerializeExport(EventReport report) => JsonSerializer.Serialize(new {
        report.Title, Summary = EventReportSummary.Create(report),
        Schemas = report.Sections.Select(EventReportSectionSchema.FromSection),
        Rows = report.Rows.Select(static row => EventReportJsonProjection.Project(row))
    });

    private static EventReport CreateSensitiveReport() {
        var row = new EventReportRow {
            Type = "Generic", EventId = 4625, TimeCreated = DateTime.UtcNow, Message = "private-account",
            SourceComputer = "private-host", CollectorComputer = "private-host", ContainerLog = "private-path",
            ObservationIdentity = "private-path", ActivityId = Guid.NewGuid(), SourceLog = "Security",
            Values = new Dictionary<string, object?> {
                ["User"] = "private-account", ["OtherUser"] = "private-account", ["Count"] = 3,
                ["Nested"] = new { Secret = "private-account" }
            }
        };
        EventReportSectionSchema schema = EventReportSectionSchema.CreateGeneric();
        schema.Description = "private-account";
        return EventReportEngine.CreateStored(new[] { row }, new[] { schema }, "private-account",
            new[] { new EventReportCoverage { MachineName = "private-host", LogName = "private-path", Succeeded = false,
                Status = "private-detail", Detail = "private-detail" } }, completenessDiagnostic: "private-detail");
    }
}
