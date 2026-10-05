using EventViewerX.Evtx;
using Xunit;

namespace EventViewerX.Portability.Tests;

public sealed class TestEvtxEvidenceBoundaries {
    [Fact]
    public void ForwardedSourceIdentityDoesNotReplaceContainerIdentity() {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ForwardedEvents-Literal-Sanitized.evtx");
        SavedEventRecord record = Assert.Single(new EvtxSavedEventReader().Read(new EventLogFileQuery(path) { Oldest = true }));
        record.FileOffset = null;
        using var cursor = new EvtxRecordHeaderCursor(path, CancellationToken.None);
        var framer = new EvtxDumpXmlRecordFramer();
        Assert.False(framer.TryAdd("Record 1", out _));
        Assert.True(framer.TryAdd(record.RawXml, out _));
        Assert.Equal(1, framer.ContainerRecordNumber);
        Assert.True(cursor.TryApply(record, framer.ContainerRecordNumber!.Value));
        Assert.Equal(1_000_000_001, record.RecordId);
        Assert.Equal(4608, record.FileOffset);
    }

    [Fact]
    public void BoundedNewestFirstReadReportsSkippedParserRecords() {
        string source = Path.Combine(AppContext.BaseDirectory, "Fixtures", "NamedFilterExamples.evtx");
        string path = Path.Combine(Path.GetTempPath(), "evx-evidence-" + Guid.NewGuid().ToString("N") + ".evtx");
        File.Copy(source, path);
        try {
            using (FileStream file = File.OpenWrite(path)) {
                file.Position = 4096 + 512 + 24;
                file.WriteByte(0xff);
            }
            var diagnostics = new List<SavedEventReadDiagnostic>();
            EventObject[] records = EventLogEngine.ReadFile(new EventLogFileQuery(path) {
                Oldest = false, MaxEvents = 1, ReadMode = EventReadMode.Metadata,
                SavedEventReader = new EvtxSavedEventReader(),
                SavedEventDiagnosticHandler = diagnostics.Add
            }).ToArray();
            Assert.Single(records);
            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "EVXEVTX202" && diagnostic.AffectsCompleteness);
        } finally {
            File.Delete(path);
        }
    }
}
