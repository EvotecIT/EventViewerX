using EventViewerX.Evtx;
using System.Diagnostics;
using System.Text;
using Xunit;

namespace EventViewerX.Portability.Tests;

public sealed class TestSavedEventPortability {
    [Fact]
    public void ExactSystemSelectorPrefilterPreservesPortableXPathResults() {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "NamedFilterExamples.evtx");
        var reader = new EvtxSavedEventReader();
        SavedEventRecord[] all = reader.Read(new EventLogFileQuery(path) { Oldest = true }).ToArray();
        long selectedId = Assert.IsType<long>(all[^1].RecordId);
        string exact = $"*[System[EventRecordID={selectedId}]]";

        SavedEventRecord selected = Assert.Single(reader.Read(new EventLogFileQuery(path) {
            Oldest = true,
            XPath = exact
        }));
        Assert.Equal(selectedId, selected.RecordId);
        Assert.Equal(all[^1].ProviderName, selected.ProviderName);
        Assert.Equal(all[^1].TimeCreatedUtc, selected.TimeCreatedUtc);

        var matcher = new EvtxXPathMatcher(exact);
        Assert.True(matcher.CanRejectBeforeRendering("1", 7040));
        Assert.False(matcher.CanRejectBeforeRendering(selectedId.ToString(System.Globalization.CultureInfo.InvariantCulture), 7040));
        Assert.False(new EvtxXPathMatcher($"*[System[(EventRecordID={selectedId} or EventID=7040)]]")
            .CanRejectBeforeRendering("1", 1));

        int selectedEventId = all[^1].EventId;
        SavedEventRecord[] eventMatches = reader.Read(new EventLogFileQuery(path) {
            Oldest = true,
            XPath = $"*[System[EventID={selectedEventId}]]"
        }).ToArray();
        Assert.Equal(all.Count(record => record.EventId == selectedEventId), eventMatches.Length);
        Assert.False(new EvtxXPathMatcher($"*[System[EventID={selectedEventId}]]")
            .CanRejectBeforeRendering(null, 0));
    }

    [Fact]
    public void ManagedReaderRetainsAllParseableRecordsFromRetainedTruncatedFixture() {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "NamedFilterExamples-Truncated.evtx");
        var diagnostics = new List<SavedEventReadDiagnostic>();
        var query = new EventLogFileQuery(path) { Oldest = true, XPath = "*" };

        SavedEventRecord[] records = new EvtxSavedEventReader().Read(query, diagnostics.Add).ToArray();

        Assert.Equal(168, records.Length);
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Code == "EVXEVTX002");
        Assert.Equal(records.OrderBy(static record => record.RecordId).Select(static record => record.RecordId),
            records.Select(static record => record.RecordId));
    }

    [Fact]
    public void ManagedReaderParsesRetainedLiteralForwardedEventFixture() {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ForwardedEvents-Literal-Sanitized.evtx");
        var diagnostics = new List<SavedEventReadDiagnostic>();
        var query = new EventLogFileQuery(path) { Oldest = true, XPath = "*" };

        SavedEventRecord record = Assert.Single(new EvtxSavedEventReader().Read(query, diagnostics.Add));

        Assert.Equal("Microsoft-Windows-Security-Auditing", record.ProviderName);
        Assert.Equal(4625, record.EventId);
        Assert.Equal(1_000_000_001, record.RecordId);
        Assert.Equal("Security", record.Channel);
        Assert.Equal("forwarded.example.test", record.Computer);
        Assert.Equal("evxuser", record.Data["TargetUserName"]);
        Assert.Equal("EVX-TESTLAB", record.Data["TargetDomainName"]);
        Assert.Equal("192.168.100.42", record.Data["IpAddress"]);
        Assert.Equal("51432", record.Data["IpPort"]);
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Code == "EVXEVTX005");
        Assert.DoesNotContain(diagnostics,
            static diagnostic => diagnostic.Code is "EVXEVTX002" or "EVXEVTX003" or "EVXEVTX004");
    }

    [Fact]
    public void ManagedReaderAppliesCompiledUtcTimeBounds() {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ForwardedEvents-Literal-Sanitized.evtx");
        var reader = new EvtxSavedEventReader();
        SavedEventRecord source = Assert.Single(reader.Read(new EventLogFileQuery(path) {
            Oldest = true,
            XPath = "*"
        }));
        string inclusiveXPath = EventFilterCompiler.BuildXPath(new EventFilter {
            StartTime = source.TimeCreatedUtc,
            EndTime = source.TimeCreatedUtc
        });
        string excludedXPath = EventFilterCompiler.BuildXPath(new EventFilter {
            StartTime = source.TimeCreatedUtc.AddTicks(1)
        });

        SavedEventRecord included = Assert.Single(reader.Read(new EventLogFileQuery(path) {
            Oldest = true,
            XPath = inclusiveXPath
        }));
        SavedEventRecord[] excluded = reader.Read(new EventLogFileQuery(path) {
            Oldest = true,
            XPath = excludedXPath
        }).ToArray();

        Assert.Equal(source.RecordId, included.RecordId);
        Assert.Empty(excluded);
    }

    [Fact]
    public void ManagedReaderRejectsOutOfBoundsLiteralNameReferences() {
        string source = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ForwardedEvents-Literal-Sanitized.evtx");
        string path = Path.Combine(Path.GetTempPath(), $"eventviewerx-literal-bounds-{Guid.NewGuid():N}.evtx");
        byte[] bytes = File.ReadAllBytes(source);
        const int rootNameOffset = 4096 + 512 + 24 + 4 + 1 + 2 + 4;
        Array.Fill(bytes, byte.MaxValue, rootNameOffset, sizeof(uint));
        File.WriteAllBytes(path, bytes);
        try {
            var query = new EventLogFileQuery(path) { Oldest = true, XPath = "*" };

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                new EvtxSavedEventReader().Read(query).ToArray());

            Assert.Contains("name offset", exception.Message, StringComparison.OrdinalIgnoreCase);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void LiteralReaderRejectsInvalidRecordSignaturesInsideUsedChunkSpace() {
        string source = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ForwardedEvents-Literal-Sanitized.evtx");
        byte[] bytes = File.ReadAllBytes(source);
        const int recordOffset = 4096 + 512;
        Array.Clear(bytes, recordOffset, sizeof(int));
        using var stream = new MemoryStream(bytes, writable: false);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            EvtxLiteralRecordReader.Read(stream, CancellationToken.None).ToArray());

        Assert.Contains("invalid record signature", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("free-space offset", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LiteralReaderRejectsFreeSpaceBeforeTheFirstRecord() {
        string source = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ForwardedEvents-Literal-Sanitized.evtx");
        byte[] bytes = File.ReadAllBytes(source);
        const int freeSpaceOffset = 4096 + 0x30;
        BitConverter.GetBytes(512u).CopyTo(bytes, freeSpaceOffset);
        using var stream = new MemoryStream(bytes, writable: false);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            EvtxLiteralRecordReader.Read(stream, CancellationToken.None).ToArray());

        Assert.Contains("record and free-space offsets", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ManagedReaderRejectsDeterministicMalformedContainersWithoutFatalFailures() {
        var random = new Random(811_221);
        foreach (int length in new[] { 0, 1, 8, 512, 4095, 4096, 8192, 65_536 }) {
            string path = Path.Combine(Path.GetTempPath(), $"eventviewerx-malformed-{Guid.NewGuid():N}.evtx");
            var bytes = new byte[length];
            random.NextBytes(bytes);
            File.WriteAllBytes(path, bytes);
            try {
                var query = new EventLogFileQuery(path) { Oldest = true, XPath = "*" };

                Exception? exception = Record.Exception(() =>
                    new EvtxSavedEventReader().Read(query).Take(1).ToArray());

                Assert.NotNull(exception);
                Assert.False(exception is OutOfMemoryException);
            } finally {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void EvtxDumpJsonProjectionPreservesEventIdentityAndPayload() {
        const string json = """
            {"Event":{"#attributes":{"xmlns":"http://schemas.microsoft.com/win/2004/08/events/event"},"System":{"Provider":{"#attributes":{"Name":"Microsoft-Windows-Security-Auditing","Guid":"{54849625-5478-4994-a5ba-3e3b0328c30d}"}},"EventID":4624,"Version":2,"Level":0,"Task":12544,"Opcode":0,"Keywords":"0x8020000000000000","TimeCreated":{"#attributes":{"SystemTime":"2026-08-28T10:11:12.1234567Z"}},"EventRecordID":42,"Execution":{"#attributes":{"ProcessID":812,"ThreadID":1216}},"Channel":"Security","Computer":"dc01.ad.evotec.xyz","Security":null},"EventData":{"TargetUserName":"alice","IpAddress":"10.0.0.15"},"UserData":{"ns0:Audit":{"ns0:Value":"preserved"}}}}
            """;

        SavedEventRecord record = EvtxDumpJsonProjector.Create(json);

        Assert.Equal("Microsoft-Windows-Security-Auditing", record.ProviderName);
        Assert.Equal(4624, record.EventId);
        Assert.Equal(42, record.RecordId);
        Assert.Equal("Security", record.Channel);
        Assert.Equal("dc01.ad.evotec.xyz", record.Computer);
        Assert.Equal(DateTime.Parse("2026-08-28T10:11:12.1234567Z").ToUniversalTime(), record.TimeCreatedUtc);
        Assert.Equal("alice", record.Data["TargetUserName"]);
        Assert.Equal("10.0.0.15", record.Data["IpAddress"]);
        Assert.Contains("<Audit>", record.RawXml, StringComparison.Ordinal);
        Assert.Contains("<Value>preserved</Value>", record.RawXml, StringComparison.Ordinal);
    }

    [Fact]
    public void EvtxDumpReaderReportsActionableExecutableFailure() {
        string path = Path.Combine(Path.GetTempPath(), $"eventviewerx-{Guid.NewGuid():N}.evtx");
        File.WriteAllBytes(path, new byte[4096]);
        try {
            var reader = new EvtxDumpSavedEventReader($"eventviewerx-missing-{Guid.NewGuid():N}");
            var query = new EventLogFileQuery(path) { XPath = "*", Oldest = true };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => reader.Read(query).ToArray());

            Assert.Contains("Install evtx_dump or provide its exact path", exception.Message, StringComparison.Ordinal);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void EvtxDumpReaderRejectsInvalidProcessBounds() {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EvtxDumpSavedEventReader(maximumRuntime: TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EvtxDumpSavedEventReader(maximumInactivity: TimeSpan.Zero));
    }

    [Fact]
    public void EvtxDumpReaderHonorsCancellationBeforeStartingTheParser() {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "NamedFilterExamples.evtx");
        var reader = new EvtxDumpSavedEventReader($"eventviewerx-missing-{Guid.NewGuid():N}");
        var query = new EventLogFileQuery(path) { XPath = "*", Oldest = true };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        OperationCanceledException exception = Assert.ThrowsAny<OperationCanceledException>(() =>
            reader.Read(query, cancellationToken: cancellation.Token).ToArray());

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    [Fact]
    public void EvtxDumpXmlFramerPreservesMultilineRecords() {
        var framer = new EvtxDumpXmlRecordFramer();

        Assert.False(framer.TryAdd("<?xml version=\"1.0\" encoding=\"utf-8\"?>", out _));
        Assert.False(framer.TryAdd("<Event><System>", out _));
        Assert.False(framer.TryAdd(string.Empty, out _));
        Assert.False(framer.TryAdd("<EventID>42</EventID></System>", out _));
        Assert.True(framer.TryAdd("<EventData><Data>line</Data></EventData></Event>", out string? xml));

        Assert.Equal(
            "<Event><System>\n\n<EventID>42</EventID></System>\n<EventData><Data>line</Data></EventData></Event>",
            xml);
        framer.Complete();
    }

    [Fact]
    public void EvtxDumpXmlFramerDoesNotEndAtNestedEventClosingTag() {
        var framer = new EvtxDumpXmlRecordFramer();

        Assert.False(framer.TryAdd("<Event><UserData>", out _));
        Assert.False(framer.TryAdd("<Event><Value>nested</Value></Event>", out _));
        Assert.True(framer.TryAdd("</UserData></Event>", out string? xml));

        Assert.Equal(
            "<Event><UserData>\n<Event><Value>nested</Value></Event>\n</UserData></Event>",
            xml);
        framer.Complete();
    }

    [Fact]
    public void EvtxDumpXmlFramerRecoversAtNextDocumentDeclaration() {
        var framer = new EvtxDumpXmlRecordFramer();
        const string firstDocument = "<?xml version=\"1.0\"?><Event><System /></Event>";
        const string secondDocument = "<?xml version=\"1.0\"?><Event><System /></Event>";

        Assert.False(framer.TryAdd("<Event><UserData>", out _, out _));
        Assert.True(framer.TryAdd(
            firstDocument,
            out string? first,
            out string? recoveryError));
        Assert.True(framer.TryAdd(secondDocument, out string? second, out _));

        Assert.Contains("previous Event fragment ended", recoveryError, StringComparison.Ordinal);
        Assert.Equal(firstDocument, first);
        Assert.Equal(secondDocument, second);
        framer.Complete();
    }

    [Fact]
    public void EvtxDumpXmlFramerPreservesCompactDeclarationAndEvent() {
        var framer = new EvtxDumpXmlRecordFramer();
        const string value = "<?xml version=\"1.0\"?><Event><System /></Event>";

        Assert.True(framer.TryAdd(value, out string? xml));

        Assert.Equal(value, xml);
        framer.Complete();
    }

    [Fact]
    public void EvtxDumpXmlFramerRejectsStylesheetProcessingInstructionAsPreamble() {
        var framer = new EvtxDumpXmlRecordFramer();

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            framer.TryAdd("<?xml-stylesheet?><Event />", out _));

        Assert.Contains("unexpected output", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EvtxDumpXmlFramerIgnoresClosingTagTextInsideCData() {
        var framer = new EvtxDumpXmlRecordFramer();

        Assert.False(framer.TryAdd("<Event><UserData><![CDATA[", out _));
        Assert.False(framer.TryAdd("</Event>", out _));
        Assert.True(framer.TryAdd("]]></UserData></Event>", out string? xml));

        Assert.Equal(
            "<Event><UserData><![CDATA[\n</Event>\n]]></UserData></Event>",
            xml);
        framer.Complete();
    }

    [Fact]
    public void EvtxDumpXmlFramerDoesNotResynchronizeAtDeclarationTextInsideCData() {
        var framer = new EvtxDumpXmlRecordFramer();

        Assert.False(framer.TryAdd("<Event><UserData><![CDATA[", out _));
        Assert.False(framer.TryAdd("<?xml version=\"1.0\"?><Event><Value>embedded</Value></Event>", out _));
        Assert.True(framer.TryAdd("]]></UserData></Event>", out string? xml));

        Assert.Equal(
            "<Event><UserData><![CDATA[\n<?xml version=\"1.0\"?><Event><Value>embedded</Value></Event>\n]]></UserData></Event>",
            xml);
        framer.Complete();
    }

    [Theory]
    [InlineData("<Event><![CDATA[value]]", "></Missing>", "]]></Event>")]
    [InlineData("<Event><!-- value-", "-></Missing>", "--></Event>")]
    [InlineData("<Event><?probe value?", "></Missing>", "?></Event>")]
    public void EvtxDumpXmlFramerPreservesTerminatorPrefixesAcrossReconstructedLineBreaks(
        string firstLine,
        string secondLine,
        string thirdLine) {

        var framer = new EvtxDumpXmlRecordFramer();

        Assert.False(framer.TryAdd(firstLine, out _));
        Assert.False(framer.TryAdd(secondLine, out _));
        Assert.True(framer.TryAdd(thirdLine, out string? xml));

        Assert.Equal($"{firstLine}\n{secondLine}\n{thirdLine}", xml);
        framer.Complete();
    }

    [Fact]
    public void EvtxDumpXmlFramerRejectsRootNamePrefixCollision() {
        var framer = new EvtxDumpXmlRecordFramer();

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            framer.TryAdd("<Eventual />", out _));

        Assert.Contains("unexpected output", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EvtxDumpXmlFramerRecoversAfterMalformedTrailingMarkup() {
        var framer = new EvtxDumpXmlRecordFramer();

        Assert.Throws<InvalidDataException>(() =>
            framer.TryAdd("<Event></Event></Event>", out _));

        Assert.True(framer.TryAdd("<Event />", out string? xml));
        Assert.Equal("<Event />", xml);
        framer.Complete();
    }

    [Fact]
    public void EvtxDumpXmlFramerRejectsIncompleteRecords() {
        var framer = new EvtxDumpXmlRecordFramer();
        Assert.False(framer.TryAdd("<Event><System>", out _));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => framer.Complete());

        Assert.Contains("ended inside", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EvtxDumpXmlFramerRejectsOversizedSingleLineRecords() {
        var framer = new EvtxDumpXmlRecordFramer();
        string xml = "<Event>" +
                     new string('x', EvtxDumpXmlRecordFramer.MaximumRecordCharacters) +
                     "</Event>";

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => framer.TryAdd(xml, out _));

        Assert.Contains("larger than", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EvtxDumpBoundedLineReaderRejectsLineBeforeMaterializingPastLimit() {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("123456789\n"));
        using var textReader = new StreamReader(stream, Encoding.UTF8);
        var reader = new EvtxDumpBoundedLineReader(textReader, maximumLineCharacters: 8);
        int boundaryChecks = 0;

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => reader.ReadLine(() => boundaryChecks++));

        Assert.Contains("larger than", exception.Message, StringComparison.Ordinal);
        Assert.True(boundaryChecks > 0);
    }

    [Fact]
    public void EvtxDumpReadBoundaryExcludesConsumerDelayFromInactivity() {
        string text = new string('x', 65_535) + "\nsecond\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        using var textReader = new StreamReader(stream, Encoding.UTF8);
        var reader = new EvtxDumpBoundedLineReader(textReader, maximumLineCharacters: 65_535);
        var inactivity = new Stopwatch();
        int boundaryChecks = 0;
        Action boundaryCheck = () => boundaryChecks++;

        string? first = EvtxDumpSavedEventReader.ReadLineBounded(reader, boundaryCheck, inactivity);
        TimeSpan beforeConsumerDelay = inactivity.Elapsed;
        Thread.Sleep(100);
        Assert.Equal(beforeConsumerDelay, inactivity.Elapsed);
        string? second = EvtxDumpSavedEventReader.ReadLineBounded(reader, boundaryCheck, inactivity);

        Assert.Equal(65_535, first!.Length);
        Assert.Equal("second", second);
        Assert.True(boundaryChecks >= 2);
        Assert.False(inactivity.IsRunning);
    }

    [Fact]
    public void EvtxRecordHeaderCursorRestoresFileOffsetWithoutReplacingEventTime() {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "NamedFilterExamples.evtx");
        var header = new byte[24];
        using (FileStream stream = File.OpenRead(path)) {
            stream.Position = 4096 + 512;
            stream.ReadExactly(header);
        }
        long recordId = BitConverter.ToInt64(header, 8);
        DateTime timestampUtc = DateTime.FromFileTimeUtc(BitConverter.ToInt64(header, 16));
        var actual = new SavedEventRecord {
            RecordId = recordId,
            TimeCreatedUtc = timestampUtc.AddTicks(-1),
            RawXml = $"<Event><System><TimeCreated SystemTime=\"{timestampUtc.AddTicks(-1):yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'}\" /></System></Event>"
        };

        using var cursor = new EvtxRecordHeaderCursor(path, CancellationToken.None);
        bool matched = cursor.TryApply(actual);

        Assert.True(matched);
        Assert.Equal(timestampUtc.AddTicks(-1), actual.TimeCreatedUtc);
        Assert.Equal(4096 + 512, actual.FileOffset);
    }

    [Fact]
    public void EvtxRecordHeaderCursorPreservesOffsetWhenHeaderTimestampIsInvalid() {
        string source = Path.Combine(AppContext.BaseDirectory, "Fixtures", "NamedFilterExamples.evtx");
        string path = Path.Combine(Path.GetTempPath(), $"EventViewerX-{Guid.NewGuid():N}.evtx");
        File.Copy(source, path);
        try {
            long recordId;
            using (var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read)) {
                stream.Position = 4096 + 512 + 8;
                using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
                recordId = reader.ReadInt64();
                stream.Position = 4096 + 512 + 16;
                using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
                writer.Write(long.MaxValue);
            }
            var actual = new SavedEventRecord { RecordId = recordId };

            using var cursor = new EvtxRecordHeaderCursor(path, CancellationToken.None);
            bool matched = cursor.TryApply(actual);

            Assert.True(matched);
            Assert.Equal(4096 + 512, actual.FileOffset);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void EvtxRecordHeaderCursorResynchronizesAfterDamagedRecordHeader() {
        string source = Path.Combine(AppContext.BaseDirectory, "Fixtures", "NamedFilterExamples.evtx");
        string path = Path.Combine(Path.GetTempPath(), $"EventViewerX-{Guid.NewGuid():N}.evtx");
        File.Copy(source, path);
        try {
            const long firstRecordOffset = 4096 + 512;
            long firstRecordId;
            long secondRecordId;
            int firstRecordSize;
            using (var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read)) {
                stream.Position = firstRecordOffset + 4;
                using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
                firstRecordSize = checked((int)reader.ReadUInt32());
                firstRecordId = reader.ReadInt64();
                stream.Position = firstRecordOffset + firstRecordSize + 8;
                secondRecordId = reader.ReadInt64();
                stream.Position = firstRecordOffset;
                using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
                writer.Write(0);
            }
            var damagedRecord = new SavedEventRecord { RecordId = firstRecordId };
            var recoveredRecord = new SavedEventRecord { RecordId = secondRecordId };

            using var cursor = new EvtxRecordHeaderCursor(path, CancellationToken.None);

            Assert.False(cursor.TryApply(damagedRecord));
            Assert.True(cursor.TryApply(recoveredRecord));
            Assert.Equal(firstRecordOffset + firstRecordSize, recoveredRecord.FileOffset);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void ParserNeutralReaderDoesNotInvokeWindowsEventingApis() {
        string path = Path.Combine(Path.GetTempPath(), $"eventviewerx-portable-{Guid.NewGuid():N}.evtx");
        File.WriteAllBytes(path, new byte[] { 1 });
        try {
            var query = new EventLogFileQuery(path) {
                SavedEventReader = new PortableFixtureReader(),
                ReadMode = EventReadMode.StructuredData,
                Oldest = true
            };

            EventObject result = Assert.Single(EventLogEngine.ReadFile(query));

            Assert.Equal(7001, result.Id);
            Assert.Equal(7, result.RecordId);
            Assert.Equal("portable-host", result.SourceComputer);
            Assert.Equal("value", result.Data["PortableField"]);
            Assert.Equal(EventLogQuerySourceKind.File, result.QuerySourceKind);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void ParserBackedConsolidationPreservesCaseDistinctFilesOnCaseSensitivePlatforms() {
        string directory = Path.Combine(Path.GetTempPath(), $"eventviewerx-case-{Guid.NewGuid():N}");
        if (!FileSystemPathIdentity.IsCaseSensitive(directory)) {
            return;
        }
        var reader = new PortableFixtureReader();
        var lower = new EventLogFileQuery(Path.Combine(directory, "events.evtx")) {
            SavedEventReader = reader,
            XPath = "*"
        };
        var upper = new EventLogFileQuery(Path.Combine(directory, "Events.evtx")) {
            SavedEventReader = reader,
            XPath = "*"
        };

        EventLogBatchQuery consolidated = EventLogBatchConsolidator.Consolidate(
            EventLogBatchQuery.ForFiles(new[] { lower, upper }));

        Assert.Equal(2, consolidated.FileQueries.Count);
    }

    [Fact]
    public void FileQueryBuilderPreservesCaseDistinctPathsOnCaseSensitivePlatforms() {
        string directory = Path.Combine(Path.GetTempPath(), $"eventviewerx-builder-case-{Guid.NewGuid():N}");
        string lower = Path.Combine(directory, "events.evtx");
        string upper = Path.Combine(directory, "Events.evtx");
        var builder = new EventQueryDefinitionBuilder();
        Directory.CreateDirectory(directory);
        try {
            if (!FileSystemPathIdentity.IsCaseSensitive(directory)) {
                return;
            }
            File.WriteAllBytes(lower, Array.Empty<byte>());
            File.WriteAllBytes(upper, Array.Empty<byte>());
            builder.FromFiles(lower, upper);
            EventQueryDefinition definition = builder.Build();
            EventLogBatchQuery batch = EventQueryPlanner.CreateBatch(definition);
            EventLogStructuredQuery structured = EventLogStructuredQuery.ForFiles(new[] { lower, upper });

            Assert.False(FileSystemPathIdentity.Equals(lower, upper));
            Assert.Equal(2, definition.Paths!.Count);
            Assert.Equal(2, batch.StructuredQueries.SelectMany(static query => query.ResolveSources()).Count());
            Assert.Equal(2, structured.ResolveSources().Count);
            Assert.Equal(2, structured.GetIndependentSourceCount());
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void PathComparerUsesTheContainingFilesystemCaseRules() {
        string directory = Path.Combine(Path.GetTempPath(), $"eventviewerx-path-identity-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string actual = Path.Combine(directory, "CaseProbe.evtx");
        string alternate = Path.Combine(directory, "caseProbe.evtx");
        File.WriteAllBytes(actual, Array.Empty<byte>());
        try {
            bool caseSensitive = FileSystemPathIdentity.IsCaseSensitive(directory);

            Assert.Equal(caseSensitive, !File.Exists(alternate));
            Assert.Equal(caseSensitive ? 2 : 1,
                new[] { actual, alternate }.Distinct(FileSystemPathIdentity.Comparer).Count());
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task TypedPortableQueryEvaluatesCombinedChannelAndEventSelectors() {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "NamedFilterExamples.evtx");
        var query = new EventTypeQuery(new[] { EventType.ADUserLogonFailed }) {
            Paths = new[] { path },
            SavedEventReader = new SecurityLogonFixtureReader(),
            Oldest = true
        };
        var records = new List<EventTypeRecord>();

        await foreach (EventTypeRecord record in EventTypeEngine.ReadAsync(query)) {
            records.Add(record);
        }

        EventTypeRecord result = Assert.Single(records);
        Assert.Equal("ADUserLogonFailed", result.TypeName);
        Assert.Equal("Security", result.SourceLogName);
        Assert.Equal(4625, result.EventId);
    }

    [Fact]
    public void NewestFirstBufferRetainsOnlyTheRequestedTailInReverseOrder() {
        IEnumerable<SavedEventRecord> source = Enumerable.Range(1, 10)
            .Select(static value => new SavedEventRecord { RecordId = value });

        SavedEventRecord[] records = NewestFirstSavedEventBuffer.Read(
                source,
                maximumRecords: 3,
                CancellationToken.None)
            .ToArray();

        Assert.Equal(new long?[] { 10, 9, 8 }, records.Select(static record => record.RecordId));
    }

    [Fact]
    public void XmlProjectorPreservesPortableIdentityAndPayload() {
        const string xml = """
            <Event xmlns="http://schemas.microsoft.com/win/2004/08/events/event">
              <System>
                <Provider Name="Portable-Provider" Guid="{11111111-1111-4111-8111-111111111111}" />
                <EventID>4624</EventID><Version>2</Version><Level>0</Level><Task>12544</Task><Opcode>0</Opcode>
                <Keywords>0x8020000000000000</Keywords>
                <TimeCreated SystemTime="2026-08-28T10:00:00.0000000Z" />
                <EventRecordID>42</EventRecordID><Correlation ActivityID="{22222222-2222-4222-8222-222222222222}" />
                <Execution ProcessID="123" ThreadID="456" /><Channel>Security</Channel><Computer>dc1.example.test</Computer>
              </System>
              <EventData><Data Name="TargetUserName">alice</Data><Data>unnamed</Data></EventData>
            </Event>
            """;

        SavedEventRecord record = SavedEventXmlProjector.Create(xml);

        Assert.Equal(4624, record.EventId);
        Assert.Equal(42, record.RecordId);
        Assert.Equal("Portable-Provider", record.ProviderName);
        Assert.Equal("Security", record.Channel);
        Assert.Equal("alice", record.Data["TargetUserName"]);
        Assert.Equal("unnamed", record.Data["NoNameA0"]);
        Assert.Equal(EventMessageRenderStatus.MessageResourceUnavailable, record.MessageRenderStatus);
    }

    private sealed class PortableFixtureReader : ISavedEventReader {
        public IEnumerable<SavedEventRecord> Read(
            EventLogFileQuery query,
            Action<SavedEventReadDiagnostic>? diagnosticHandler = null,
            CancellationToken cancellationToken = default) {

            yield return new SavedEventRecord {
                ProviderName = "Portable-Fixture",
                EventId = 7001,
                RecordId = 7,
                Channel = "Portable/Operational",
                Computer = "portable-host",
                TimeCreatedUtc = new DateTime(2026, 8, 28, 10, 0, 0, DateTimeKind.Utc),
                Data = new Dictionary<string, string> { ["PortableField"] = "value" },
                MessageRenderStatus = EventMessageRenderStatus.MessageResourceUnavailable
            };
        }
    }

    private sealed class SecurityLogonFixtureReader : ISavedEventReader {
        public IEnumerable<SavedEventRecord> Read(
            EventLogFileQuery query,
            Action<SavedEventReadDiagnostic>? diagnosticHandler = null,
            CancellationToken cancellationToken = default) {

            yield return new SavedEventRecord {
                ProviderName = "Microsoft-Windows-Security-Auditing",
                EventId = 4625,
                RecordId = 42,
                Channel = "Security",
                Computer = "portable-host",
                TimeCreatedUtc = new DateTime(2026, 8, 30, 10, 0, 0, DateTimeKind.Utc),
                Data = new Dictionary<string, string> {
                    ["TargetUserName"] = "alice",
                    ["TargetDomainName"] = "EVX"
                },
                MessageRenderStatus = EventMessageRenderStatus.MessageResourceUnavailable
            };
        }
    }
}
