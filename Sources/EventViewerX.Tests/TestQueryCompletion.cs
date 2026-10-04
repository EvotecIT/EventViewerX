using System.Security.Cryptography;
using System.Text.Json;
using EventViewerX.Reporting;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestQueryCompletion {
    [Fact]
    public void SummaryDistinguishesFailedSourcesAndBoundsFromAnExhaustiveEmptyResult() {
        var source = new EventReportCoverage { MachineName = "server01", LogName = "Security", Succeeded = false,
            Status = "AccessDenied", Detail = "Read access denied" };
        var failed = new EventReportSummary(0, 0, false, null, new[] { source });
        source.Succeeded = true;

        Assert.False(failed.IsComplete);
        Assert.True(failed.HasDeclaredCoverage);
        Assert.Equal("AccessDenied", Assert.Single(failed.Coverage).Status);
        Assert.False(new EventReportSummary(0, 1, true, "Candidate limit reached").IsComplete);
        Assert.True(new EventReportSummary(0, 0, false, null).IsComplete);
        Assert.False(new EventReportSummary(0, 0, false, null).HasDeclaredCoverage);
    }

    [Fact]
    public async Task SingleCsvMetadataPreservesCompletenessAndBindsItToTheExportedBytes() {
        string fixture = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "Tests", "Logs", "NamedFilterExamples.evtx"));
        EventReportRequest request = EventReportRequest.ForFiles(fixture);
        request.MaxEvents = 1;
        EventReport report = await EventReportEngine.QueryAsync(request);
        string csvPath = Path.Combine(Path.GetTempPath(), "evx-metadata-" + Guid.NewGuid().ToString("N") + ".csv");
        try {
            EventReportCsvRenderer.Save(report, csvPath);
            using JsonDocument metadata = JsonDocument.Parse(File.ReadAllText(csvPath + ".metadata.json"));
            JsonElement summary = metadata.RootElement.GetProperty("Summary");
            Assert.False(summary.GetProperty("IsComplete").GetBoolean());
            Assert.True(summary.GetProperty("ScanLimitReached").GetBoolean());
            Assert.Equal(report.CompletenessDiagnostic, summary.GetProperty("CompletenessDiagnostic").GetString());
            Assert.Equal(report.Coverage.Count, summary.GetProperty("Coverage").GetArrayLength());
            Assert.Equal(Path.GetFileName(csvPath), metadata.RootElement.GetProperty("FileName").GetString());
            using SHA256 hash = SHA256.Create();
            Assert.Equal(Convert.ToHexString(hash.ComputeHash(File.ReadAllBytes(csvPath))).ToLowerInvariant(),
                metadata.RootElement.GetProperty("Sha256").GetString());
        } finally {
            File.Delete(csvPath);
            File.Delete(csvPath + ".metadata.json");
        }
    }
}
