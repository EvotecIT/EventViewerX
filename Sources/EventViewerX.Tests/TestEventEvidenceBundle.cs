using System.IO.Compression;
using System.Text;
using System.Text.Json;
using EventViewerX.Reporting;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventEvidenceBundle {
    [Fact]
    public void RoundTripPreservesQueryOrderSchemasContextAndIncompleteEvidence() {
        EventReport report = CreateReport();
        using var output = new MemoryStream();
        using var rendered = new MemoryStream(Encoding.UTF8.GetBytes("<html>report</html>"));
        EventEvidenceBundleManifest written = EventEvidenceBundle.Write(report, output, new EventEvidenceBundleOptions {
            QueryJson = "{\"MaxEvents\":2}", DefinitionJson = "{\"Name\":\"Generic\"}",
            PackProvenanceJson = "[{\"Version\":\"1.0.0\",\"Hash\":\"example\"}]",
            Reports = new Dictionary<string, Stream> { ["report.html"] = rendered }
        });
        Assert.True(output.CanWrite);
        Assert.True(rendered.CanRead);
        output.Position = 0;
        EventEvidenceBundleManifest verified = EventEvidenceBundle.Verify(output);
        Assert.Equal(written.Entries.Select(static entry => entry.Sha256), verified.Entries.Select(static entry => entry.Sha256));
        Assert.Equal(1, verified.SchemaVersion);
        Assert.False(verified.Summary.GetProperty("IsComplete").GetBoolean());
        Assert.Equal(3, verified.Summary.GetProperty("EventCount").GetInt64());
        Assert.Equal(8, verified.Summary.GetProperty("EventsScanned").GetInt64());
        using var archive = new ZipArchive(output, ZipArchiveMode.Read, leaveOpen: true);
        using var reader = new StreamReader(archive.GetEntry("rows.jsonl")!.Open());
        string[] rows = reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        long[] recordIds = rows.Select(row => {
            using JsonDocument json = JsonDocument.Parse(row);
            return json.RootElement.GetProperty("RecordId").GetInt64();
        }).ToArray();
        // Section grouping would produce 2, 3, 1; the bundle must retain the query order.
        Assert.Equal(new long[] { 2, 1, 3 }, recordIds);
        Assert.Contains(verified.Entries, static entry => entry.Name == "schemas.json");
        Assert.Contains(verified.Entries, static entry => entry.Name == "query.json");
        Assert.Contains(verified.Entries, static entry => entry.Name == "definition.json");
        Assert.Contains(verified.Entries, static entry => entry.Name == "packs.json");
        Assert.Contains(verified.Entries, static entry => entry.Name == "report.html");
    }

    [Theory]
    [InlineData("tamper")]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("traversal")]
    [InlineData("duplicate")]
    [InlineData("schema")]
    [InlineData("completion")]
    [InlineData("row-count")]
    public void VerificationRejectsModifiedOrAmbiguousInventory(string change) {
        using var output = new MemoryStream();
        EventEvidenceBundle.Write(CreateReport(), output);
        using (var archive = new ZipArchive(output, ZipArchiveMode.Update, leaveOpen: true)) {
            if (change is "tamper" or "missing") {
                archive.GetEntry("rows.jsonl")!.Delete();
                if (change == "tamper") { using var writer = new StreamWriter(archive.CreateEntry("rows.jsonl").Open()); writer.Write("{}\n"); }
            } else if (change is "schema" or "completion" or "row-count") {
                ZipArchiveEntry entry = archive.GetEntry("manifest.json")!;
                string json;
                using (var reader = new StreamReader(entry.Open())) { json = reader.ReadToEnd(); }
                entry.Delete();
                using var writer = new StreamWriter(archive.CreateEntry("manifest.json").Open());
                writer.Write(change == "schema" ? json.Replace("\"SchemaVersion\":1", "\"SchemaVersion\":2")
                    : change == "completion" ? json.Replace("\"IsComplete\":false", "\"IsComplete\":true")
                    : json.Replace("\"EventCount\":3", "\"EventCount\":4"));
            } else {
                string name = change == "traversal" ? "../outside.txt" : change == "duplicate" ? "rows.jsonl" : "extra.txt";
                archive.CreateEntry(name);
            }
        }
        output.Position = 0;
        Assert.Throws<InvalidDataException>(() => EventEvidenceBundle.Verify(output));
    }

    [Fact]
    public void VerificationRejectsOversizedOrInconsistentCentralDirectoriesBeforeReadingEntries() {
        using var output = new MemoryStream();
        EventEvidenceBundle.Write(CreateReport(), output);
        byte[] original = output.ToArray();
        int end = original.Length - 22; // The writer does not add an archive comment.
        Assert.Equal(0x06054b50u, BitConverter.ToUInt32(original, end));
        foreach (string change in new[] { "entry-count", "directory-size", "directory-offset" }) {
            byte[] malformed = (byte[])original.Clone();
            if (change == "entry-count") {
                BitConverter.GetBytes(ushort.MaxValue).CopyTo(malformed, end + 8);
                BitConverter.GetBytes(ushort.MaxValue).CopyTo(malformed, end + 10);
            } else if (change == "directory-size") {
                BitConverter.GetBytes(65537u).CopyTo(malformed, end + 12);
            } else {
                BitConverter.GetBytes(uint.MaxValue).CopyTo(malformed, end + 16);
            }
            using var input = new MemoryStream(malformed);
            Assert.Throws<InvalidDataException>(() => EventEvidenceBundle.Verify(input));
        }
    }

    [Fact]
    public void WritingAndVerificationEnforceContentBoundsAndCancellationWithoutOwningStreams() {
        using var output = new MemoryStream();
        using var oversized = new MemoryStream(new byte[1024 * 1024]);
        Assert.Throws<InvalidDataException>(() => EventEvidenceBundle.Write(CreateReport(), output, new EventEvidenceBundleOptions {
            MaximumUncompressedBytes = 1024 * 1024, Reports = new Dictionary<string, Stream> { ["report.xlsx"] = oversized }
        }));
        Assert.True(output.CanWrite);
        Assert.True(oversized.CanRead);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var canceled = new MemoryStream();
        Assert.Throws<OperationCanceledException>(() => EventEvidenceBundle.Write(CreateReport(), canceled, cancellationToken: cancellation.Token));
        Assert.Equal(0, canceled.Length);
        using var valid = new MemoryStream();
        EventEvidenceBundle.Write(CreateReport(), valid, new EventEvidenceBundleOptions {
            Reports = new Dictionary<string, Stream> { ["report.xlsx"] = new MemoryStream(new byte[1024 * 1024]) }
        });
        valid.Position = 0;
        Assert.Throws<InvalidDataException>(() => EventEvidenceBundle.Verify(valid, maximumUncompressedBytes: 1024 * 1024));
        valid.Position = 0;
        Assert.Throws<OperationCanceledException>(() => EventEvidenceBundle.Verify(valid, cancellationToken: cancellation.Token));
    }

    [Fact]
    public void PrivacySnapshotBundlesDoNotReintroduceOmittedEvidence() {
        EventReport original = EventReportEngine.CreateStored(new[] { new EventReportRow {
            Type = "Generic", Message = "secret", SourceComputer = "secret", Values = new Dictionary<string, object?> { ["User"] = "secret" }
        } }, new[] { EventReportSectionSchema.CreateGeneric() }, "secret");
        using var output = new MemoryStream();
        EventEvidenceBundle.Write(EventReportPrivacy.Apply(original), output, new EventEvidenceBundleOptions {
            PrivacyPolicy = new EventReportPrivacyOptions()
        });
        using var archive = new ZipArchive(output, ZipArchiveMode.Read, leaveOpen: true);
        using (var policyReader = new StreamReader(archive.GetEntry("privacy.json")!.Open())) {
            using JsonDocument policy = JsonDocument.Parse(policyReader.ReadToEnd());
            Assert.Equal(1, policy.RootElement.GetProperty("SchemaVersion").GetInt32());
            Assert.Empty(policy.RootElement.GetProperty("RetainedValueFields").EnumerateArray());
            Assert.False(policy.RootElement.GetProperty("PseudonymizeSourceIdentities").GetBoolean());
        }
        foreach (ZipArchiveEntry entry in archive.Entries) {
            using var reader = new StreamReader(entry.Open());
            Assert.DoesNotContain("secret", reader.ReadToEnd());
        }
        Assert.Equal("secret", original.Rows[0].Message);
        EventReport typed = CreateReport();
        EventReport minimized = EventReportPrivacy.Apply(typed);
        EventReport restored = EventReportEngine.CreateStored(minimized.Rows,
            minimized.Sections.Select(EventReportSectionSchema.FromSection), coverage: minimized.Coverage,
            eventsScanned: minimized.EventsScanned, completenessDiagnostic: minimized.CompletenessDiagnostic);
        Assert.Single(restored.Sections);
        Assert.All(restored.Rows, static row => Assert.Equal("Generic", row.Type));
        Assert.Equal(new long?[] { 2, 1, 3 }, restored.Rows.Select(static row => row.RecordId));
        Assert.Equal("First", typed.Rows[0].Type);
        Assert.False(EventReportSummary.Create(restored).IsComplete);
    }

    private static EventReport CreateReport() => EventReportEngine.CreateStored(new[] {
        new EventReportRow { Type = "First", RecordId = 2, EventId = 4625, TimeCreated = DateTime.UtcNow, Values = new Dictionary<string, object?> { ["User"] = "example" } },
        new EventReportRow { Type = "Second", RecordId = 1, EventId = 4624, TimeCreated = DateTime.UtcNow },
        new EventReportRow { Type = "First", RecordId = 3, EventId = 4625, TimeCreated = DateTime.UtcNow }
    }, new[] {
        new EventReportSectionSchema {
            Name = "First", DisplayName = "First", Kind = EventReportSectionKind.Custom,
            Columns = new[] {
                new EventReportColumnSchema { Name = "User", DisplayName = "User", ValueTypeName = "System.String" }
            }
        },
        new EventReportSectionSchema { Name = "Second", DisplayName = "Second", Kind = EventReportSectionKind.Custom }
    }, eventsScanned: 8, completenessDiagnostic: "Input window was incomplete.");
}
