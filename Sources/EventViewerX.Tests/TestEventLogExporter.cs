using System.Text.Json;
using System.Xml.Linq;
using EventViewerX.Native;
using Xunit;

namespace EventViewerX.Tests;

[Collection("NativeOperationLifetime")]
public sealed class TestEventLogExporter {
    [Fact]
    public void JsonLinesExportIsDeterministicAndMachineReadable() {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new ExportFixture();
        var query = fixture.CreateQuery(EventReadMode.Full, maxEvents: 5);
        string firstPath = fixture.GetPath("events-first.jsonl");
        string secondPath = fixture.GetPath("events-second.jsonl");

        EventExportResult first = EventLogExporter.ExportFile(
            query,
            firstPath,
            EventExportFormat.JsonLines);
        EventExportResult second = EventLogExporter.ExportFile(
            query,
            secondPath,
            EventExportFormat.JsonLines);

        Assert.Equal(5, first.EventCount);
        Assert.Equal(first.EventCount, second.EventCount);
        Assert.Equal(first.Bytes, second.Bytes);
        Assert.Equal(first.Sha256, second.Sha256);
        string[] lines = File.ReadAllLines(firstPath);
        Assert.Equal(5, lines.Length);
        foreach (string line in lines) {
            using JsonDocument document = JsonDocument.Parse(line);
            Assert.True(document.RootElement.GetProperty("recordId").GetInt64() > 0);
            Assert.False(string.IsNullOrEmpty(
                document.RootElement.GetProperty("providerName").GetString()));
            Assert.Equal(JsonValueKind.Array,
                document.RootElement.GetProperty("properties").ValueKind);
            Assert.Equal(JsonValueKind.String,
                document.RootElement.GetProperty("messageRenderStatus").ValueKind);
            Assert.Equal(JsonValueKind.String,
                document.RootElement.GetProperty("xml").ValueKind);
        }
    }

    [Fact]
    public void CsvExportUsesStableSchemaAndOneRecordPerRowForMetadata() {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new ExportFixture();
        var query = fixture.CreateQuery(EventReadMode.Metadata, maxEvents: 7);
        string outputPath = fixture.GetPath("events.csv");

        EventExportResult result = EventLogExporter.ExportFile(
            query,
            outputPath,
            EventExportFormat.Csv);

        string[] lines = File.ReadAllLines(outputPath);
        Assert.Equal(7, result.EventCount);
        Assert.Equal(8, lines.Length);
        Assert.Equal(
            "TimeCreated,RecordId,Id,ProviderName,MachineName,LogName,Level,LevelDisplayName,Task,TaskDisplayName,Opcode,OpcodeDisplayName,Keywords,KeywordDisplayNames,ProcessId,ThreadId,UserId,MessageCulture,MessageRenderStatus,MessageRenderErrorCode,Message,Properties,Data,Attachments,Xml",
            lines[0]);
        Assert.All(lines.Skip(1), static line => Assert.False(string.IsNullOrWhiteSpace(line)));
    }

    [Fact]
    public void XmlExportProducesOneWellFormedEventElementPerRecord() {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new ExportFixture();
        var query = fixture.CreateQuery(EventReadMode.StructuredData, maxEvents: 6);
        string outputPath = fixture.GetPath("events.xml");

        EventExportResult result = EventLogExporter.ExportFile(
            query,
            outputPath,
            EventExportFormat.Xml);

        XDocument document = XDocument.Load(outputPath, LoadOptions.PreserveWhitespace);
        XNamespace eventNamespace = "http://schemas.microsoft.com/win/2004/08/events/event";
        Assert.Equal(6, result.EventCount);
        Assert.Equal(6, document.Root!.Elements(eventNamespace + "Event").Count());
    }

    [Fact]
    public void CancellationPreservesExistingDestinationAndRemovesTemporaryOutput() {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new ExportFixture();
        var query = fixture.CreateQuery(EventReadMode.Full);
        string outputPath = fixture.GetPath("existing.jsonl");
        File.WriteAllText(outputPath, "preserve-me");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            EventLogExporter.ExportFile(
                query,
                outputPath,
                EventExportFormat.JsonLines,
                overwrite: true,
                cancellation.Token));

        Assert.Equal("preserve-me", File.ReadAllText(outputPath));
        Assert.Empty(Directory.GetFiles(
            fixture.DirectoryPath,
            ".existing.jsonl.*.tmp"));
    }

    [Fact]
    public void ExportNeverOverwritesItsSourceEventLog() {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new ExportFixture();
        var query = fixture.CreateQuery(EventReadMode.Full);

        IOException exception = Assert.Throws<IOException>(() =>
            EventLogExporter.ExportFile(
                query,
                fixture.SourcePath,
                EventExportFormat.JsonLines,
                overwrite: true));

        Assert.Contains("cannot overwrite the source", exception.Message);
    }

    [Fact]
    public void BatchExportNeverOverwritesAnySourceEventLog() {
        using var fixture = new ExportFixture();
        EventLogBatchQuery batch =
            EventLogBatchQuery.ForFiles(new[] {
                fixture.CreateQuery(EventReadMode.Metadata)
            });

        IOException exception = Assert.Throws<IOException>(() =>
            EventLogExporter.ExportBatch(
                batch,
                fixture.SourcePath,
                EventExportFormat.JsonLines,
                overwrite: true));

        Assert.Contains(
            "cannot overwrite a source",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StructuredExportNeverOverwritesAnySourceEventLog() {
        using var fixture = new ExportFixture();
        string queryXml = EventFilterCompiler.BuildFileQueryXml(
            new[] { fixture.SourcePath },
            new EventFilter());
        var query = new EventLogStructuredQuery(queryXml) {
            SourceKind = EventLogQuerySourceKind.File
        };

        IOException exception = Assert.Throws<IOException>(() =>
            EventLogExporter.ExportStructured(
                query,
                fixture.SourcePath,
                EventExportFormat.Xml,
                overwrite: true));

        Assert.Contains(
            "cannot overwrite a source",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CancellationAfterWritingDoesNotPromoteTemporaryOutput() {
        using var fixture = new ExportFixture();
        string outputPath = fixture.GetPath("existing.csv");
        File.WriteAllText(outputPath, "preserve-me");
        using var cancellation = new CancellationTokenSource();

        Assert.Throws<OperationCanceledException>(() =>
            EventLogExporter.ExportCore(
                outputPath,
                EventExportFormat.Csv,
                overwrite: true,
                computeSha256: true,
                cancellation.Token,
                stream => {
                    stream.WriteByte(1);
                    cancellation.Cancel();
                    return 1;
                }));

        Assert.Equal("preserve-me", File.ReadAllText(outputPath));
        Assert.Empty(Directory.GetFiles(
            fixture.DirectoryPath,
            ".existing.csv.*.tmp"));
    }

    [Fact]
    public void TemporaryCleanupDoesNotReplaceTheExportFailure() {
        using var fixture = new ExportFixture();
        string temporaryPath =
            fixture.GetPath("cleanup.tmp");
        File.WriteAllText(
            temporaryPath,
            "partial");

        EventLogExporter.DeleteTemporaryFile(
            temporaryPath,
            static _ => throw new IOException(
                "cleanup failed"));

        Assert.True(File.Exists(temporaryPath));
    }

    [Fact]
    public void NativeExportCancellationDefersCleanupUntilTheWorkerStops() {
        using var fixture = new ExportFixture();
        string outputPath = fixture.GetPath("existing.evtx");
        File.WriteAllText(outputPath, "preserve-me");
        using var cancellation = new CancellationTokenSource();
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Assert.Throws<OperationCanceledException>(() =>
            EventLogExporter.ExportEvtxCore(
                outputPath,
                overwrite: true,
                computeSha256: false,
                cancellation.Token,
                temporaryPath => {
                    using var stream = new FileStream(
                        temporaryPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None);
                    stream.WriteByte(1);
                    started.Set();
                    cancellation.Cancel();
                    release.Wait();
                }));

        Assert.True(started.IsSet);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Cancellation took {stopwatch.Elapsed.TotalMilliseconds:F0} ms.");
        Assert.Equal("preserve-me", File.ReadAllText(outputPath));
        Assert.Single(Directory.GetFiles(
            fixture.DirectoryPath,
            ".existing.evtx.*.tmp.evtx"));

        release.Set();
        Assert.True(
            SpinWait.SpinUntil(
                () => Directory.GetFiles(
                    fixture.DirectoryPath,
                    ".existing.evtx.*.tmp.evtx").Length == 0,
                TimeSpan.FromSeconds(5)),
            "The canceled native export did not remove its temporary file after the worker stopped.");
    }

    [Fact]
    public void FinalHashPassHonorsCancellation() {
        using var fixture = new ExportFixture();
        string path = fixture.GetPath("hash.bin");
        File.WriteAllBytes(path, new byte[1024]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            EventLogExporter.ComputeSha256(
                path,
                cancellation.Token));
    }

    [Fact]
    public void PromotionWithoutOverwritePreservesAConcurrentDestination() {
        using var fixture = new ExportFixture();
        string temporaryPath = fixture.GetPath("events.tmp");
        string destinationPath = fixture.GetPath("events.jsonl");
        File.WriteAllText(temporaryPath, "new-output");
        File.WriteAllText(destinationPath, "concurrent-output");

        Assert.Throws<IOException>(() =>
            EventLogExporter.PromoteTemporaryFile(
                temporaryPath,
                destinationPath,
                overwrite: false));

        Assert.Equal("concurrent-output", File.ReadAllText(destinationPath));
        Assert.Equal("new-output", File.ReadAllText(temporaryPath));
    }

    [Fact]
    public void ExportCanSkipTheFinalHashPass() {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new ExportFixture();
        var query = fixture.CreateQuery(EventReadMode.Metadata, maxEvents: 3);
        string outputPath = fixture.GetPath("events-no-hash.jsonl");

        EventExportResult result = EventLogExporter.ExportFile(
            query,
            outputPath,
            EventExportFormat.JsonLines,
            computeSha256: false);

        Assert.Equal(3, result.EventCount);
        Assert.Null(result.Sha256);
        Assert.True(result.Bytes > 0);
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public void NativeEvtxExportCanBeReopenedWithExactFilteredCount() {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new ExportFixture();
        int eventId = EventLogEngine.ReadFile(
                fixture.CreateQuery(
                    EventReadMode.Metadata,
                    maxEvents: 1))
            .Single()
            .Id;
        var query = fixture.CreateQuery(EventReadMode.Metadata);
        query.XPath = $"*[System[EventID={eventId}]]";
        string outputPath = fixture.GetPath("events.evtx");

        EventExportResult result = EventLogExporter.ExportFile(
            query,
            outputPath,
            EventExportFormat.Evtx);
        EventObject[] reopened = EventLogEngine.ReadFile(
                new EventLogFileQuery(outputPath) {
                    ReadMode = EventReadMode.Metadata
                })
            .ToArray();

        Assert.Equal(EventExportFormat.Evtx, result.Format);
        Assert.Equal(result.EventCount, reopened.Length);
        Assert.NotEmpty(reopened);
        Assert.All(reopened, item => Assert.Equal(eventId, item.Id));
    }

    [Fact]
    public void StructuredQueryExportsProjectedAndNativeFormats() {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new ExportFixture();
        int eventId = EventLogEngine.ReadFile(
                fixture.CreateQuery(
                    EventReadMode.Metadata,
                    maxEvents: 1))
            .Single()
            .Id;
        string queryXml = EventFilterCompiler.BuildFileQueryXml(
            new[] { fixture.SourcePath },
            new EventFilter {
                EventIds = new[] { eventId }
            });
        var projectedQuery =
            new EventLogStructuredQuery(queryXml) {
                SourceKind = EventLogQuerySourceKind.File,
                Oldest = true,
                ReadMode = EventReadMode.Metadata,
                MaxEvents = 3
            };

        EventExportResult json = EventLogExporter.ExportStructured(
            projectedQuery,
            fixture.GetPath("structured.jsonl"),
            EventExportFormat.JsonLines);
        var nativeQuery =
            new EventLogStructuredQuery(queryXml) {
                SourceKind = EventLogQuerySourceKind.File,
                Oldest = true
            };
        string evtxPath = fixture.GetPath("structured.evtx");
        EventExportResult evtx = EventLogExporter.ExportStructured(
            nativeQuery,
            evtxPath,
            EventExportFormat.Evtx);
        EventObject[] reopened = EventLogEngine.ReadFile(
            new EventLogFileQuery(evtxPath) {
                ReadMode = EventReadMode.Metadata
            }).ToArray();

        Assert.Equal(3, json.EventCount);
        Assert.Equal(evtx.EventCount, reopened.LongLength);
        Assert.NotEmpty(reopened);
        Assert.All(
            reopened,
            item => Assert.Equal(eventId, item.Id));
    }

    [Fact]
    public void StructuredNativeExportResolvesFileUriToLocalPath() {
        using var fixture = new ExportFixture();
        string queryXml = EventFilterCompiler.BuildFileQueryXml(
            new[] { fixture.SourcePath },
            new EventFilter());

        string source =
            WindowsEventArchive
                .ResolveSingleStructuredFileSource(
                    queryXml);

        Assert.Equal(
            Path.GetFullPath(fixture.SourcePath),
            source,
            ignoreCase: true);
        Assert.DoesNotContain(
            "file://",
            source,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StructuredNativeExportUsesQueryPathWhenSelectorOmitsPath() {
        using var fixture = new ExportFixture();
        string sourceUri =
            "file://" +
            Path.GetFullPath(fixture.SourcePath);
        string queryXml =
            "<QueryList>" +
            $"<Query Id=\"0\" Path=\"{sourceUri}\">" +
            "<Select>*</Select>" +
            "</Query>" +
            "</QueryList>";

        string source =
            WindowsEventArchive
                .ResolveSingleStructuredFileSource(
                    queryXml);

        Assert.Equal(
            Path.GetFullPath(fixture.SourcePath),
            source,
            ignoreCase: true);
    }

    [Fact]
    public void StructuredChannelExportIgnoresFileUriTextInsideXPathValues() {
        if (!OperatingSystem.IsWindows()) {
            return;
        }
        using var fixture = new ExportFixture();
        EventObject current = EventLogEngine.ReadChannel(
            new EventLogChannelQuery("System") {
                ReadMode = EventReadMode.Metadata,
                MaxEvents = 1
            }).Single();
        string queryXml =
            "<QueryList>" +
            "<Query Id=\"0\" Path=\"System\">" +
            "<Select Path=\"System\">" +
            $"*[System[EventRecordID={current.RecordId!.Value}] or " +
            "EventData[Data='file://server/share/item']]" +
            "</Select></Query>" +
            "</QueryList>";
        var query =
            new EventLogStructuredQuery(queryXml) {
                SourceKind = EventLogQuerySourceKind.Auto,
                Oldest = true
            };
        string outputPath =
            fixture.GetPath("channel-file-text.evtx");

        Assert.Equal(
            new[] { EventLogQuerySourceKind.Channel },
            query.ResolveSourceKinds());
        EventExportResult result =
            EventLogExporter.ExportStructured(
                query,
                outputPath,
                EventExportFormat.Evtx);
        EventObject[] reopened = EventLogEngine.ReadFile(
            new EventLogFileQuery(outputPath) {
                ReadMode = EventReadMode.Metadata
            }).ToArray();

        Assert.Equal(1, result.EventCount);
        Assert.Single(reopened);
        Assert.Equal(
            current.RecordId,
            reopened[0].RecordId);
    }

    [Fact]
    public void BatchExportStreamsOneGloballyOrderedOutput() {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new ExportFixture();
        long[] recordIds = EventLogEngine.ReadFile(
                fixture.CreateQuery(
                    EventReadMode.Metadata,
                    maxEvents: 100))
            .Select(static item => item.RecordId)
            .Where(static value => value.HasValue)
            .Select(static value => value!.Value)
            .Distinct()
            .Take(2)
            .ToArray();
        Assert.Equal(2, recordIds.Length);
        EventLogFileQuery[] sources = recordIds
            .Select(recordId => new EventLogFileQuery(
                fixture.SourcePath) {
                XPath = $"*[System[EventRecordID={recordId}]]",
                Oldest = true,
                ReadMode = EventReadMode.Metadata
            })
            .ToArray();
        EventLogBatchQuery batch =
            EventLogBatchQuery.ForFiles(sources);
        batch.MaxEvents = 10;
        string outputPath = fixture.GetPath("batch.jsonl");

        EventExportResult result = EventLogExporter.ExportBatch(
            batch,
            outputPath,
            EventExportFormat.JsonLines);

        JsonElement[] records = File.ReadLines(outputPath)
            .Select(line => JsonDocument.Parse(line).RootElement.Clone())
            .ToArray();
        Assert.Equal(result.EventCount, records.LongLength);
        Assert.NotEmpty(records);
        Assert.True(records
            .Select(static record =>
                record.GetProperty("timeCreated").GetDateTime())
            .SequenceEqual(records
                .Select(static record =>
                    record.GetProperty("timeCreated").GetDateTime())
                .OrderBy(static time => time)));
        Assert.All(records, record =>
            Assert.Contains(
                record.GetProperty("recordId").GetInt64(),
                recordIds));
    }

    [Fact]
    public void XmlBatchCopyPreservesTheCallerConcurrencyLimit() {
        using var fixture = new ExportFixture();
        EventLogBatchQuery batch =
            EventLogBatchQuery.ForFiles(new[] {
                fixture.CreateQuery(
                    EventReadMode.Metadata)
            });
        batch.MaxConcurrency = 1;

        EventLogBatchQuery copy =
            EventLogExporter.CopyBatchQuery(
                batch,
                EventReadMode.RawXml);

        Assert.Equal(1, copy.MaxConcurrency);
        Assert.Equal(
            EventReadMode.RawXml,
            Assert.Single(copy.FileQueries).ReadMode);
    }

    [Fact]
    public void BatchExportRejectsAFalseNativeEvtxMerge() {
        using var fixture = new ExportFixture();
        EventLogBatchQuery batch =
            EventLogBatchQuery.ForFiles(new[] {
                fixture.CreateQuery(EventReadMode.Metadata)
            });

        Assert.Throws<NotSupportedException>(() =>
            EventLogExporter.ExportBatch(
                batch,
                fixture.GetPath("batch.evtx"),
                EventExportFormat.Evtx));
    }

    [Fact]
    public void NativeEvtxExportRejectsRemoteDestinationSemanticsUpFront() {
        var channel = new EventLogChannelQuery("System") {
            MachineName = "remote.example.test"
        };
        string output = Path.Combine(
            Path.GetTempPath(),
            $"remote-{Guid.NewGuid():N}.evtx");

        NotSupportedException exception = Assert.Throws<NotSupportedException>(
            () => EventLogExporter.ExportChannel(
                channel,
                output,
                EventExportFormat.Evtx));

        Assert.Contains(
            "Run the EVTX export on the source computer",
            exception.Message,
            StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    private sealed class ExportFixture : IDisposable {
        internal ExportFixture() {
            DirectoryPath = Path.Combine(
                Path.GetTempPath(),
                $"EventViewerX-Export-{Guid.NewGuid():N}");
            Directory.CreateDirectory(DirectoryPath);
            string relativePath = Path.Combine(
                "..",
                "..",
                "..",
                "..",
                "..",
                "Tests",
                "Logs",
                "NamedFilterExamples.evtx");
            SourcePath = Path.GetFullPath(relativePath);
        }

        internal string DirectoryPath { get; }
        internal string SourcePath { get; }

        internal EventLogFileQuery CreateQuery(
            EventReadMode readMode,
            int maxEvents = 0) {

            return new EventLogFileQuery(SourcePath) {
                Oldest = true,
                ReadMode = readMode,
                MaxEvents = maxEvents
            };
        }

        internal string GetPath(string fileName) {
            return Path.Combine(DirectoryPath, fileName);
        }

        public void Dispose() {
            if (Directory.Exists(DirectoryPath)) {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
    }
}
