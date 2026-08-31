using Xunit;

namespace EventViewerX.Tests;

public sealed class TestSavedEventReader {
    [Fact]
    public async Task CustomSavedReaderUsesTheExistingFileQuerySurfaceAndPreservesFidelity() {
        string path = Path.Combine(Path.GetTempPath(), $"eventviewerx-{Guid.NewGuid():N}.evtx");
        File.WriteAllBytes(path, new byte[] { 1 });
        try {
            var diagnostics = new List<SavedEventReadDiagnostic>();
            var reader = new FixtureSavedEventReader();
            var query = new EventLogFileQuery(path) {
                XPath = "*[System/EventID=1001]",
                Oldest = true,
                ReadMode = EventReadMode.Full,
                MaxEvents = 1,
                SavedEventReader = reader,
                SavedEventDiagnosticHandler = diagnostics.Add
            };

            var events = new List<EventObject>();
            await foreach (EventObject item in EventLogEngine.ReadFileAsync(query)) {
                events.Add(item);
            }

            EventObject result = Assert.Single(events);
            Assert.Equal(1001, result.Id);
            Assert.Equal(42, result.RecordId);
            Assert.Equal("Security", result.OriginalLogName);
            Assert.Equal(Path.GetFullPath(path), result.ContainerLogName);
            Assert.Equal(EventLogQuerySourceKind.File, result.QuerySourceKind);
            Assert.Equal("alice", result.Data["TargetUserName"]);
            Assert.Contains("TargetUserName", result.XMLData, StringComparison.Ordinal);
            Assert.Equal(string.Empty, result.Message);
            Assert.Equal(EventMessageRenderStatus.MessageResourceUnavailable, result.MessageRenderStatus);
            Assert.True(reader.ObservedOldest);
            Assert.Equal("*[System/EventID=1001]", reader.ObservedXPath);
            SavedEventReadDiagnostic diagnostic = Assert.Single(diagnostics);
            Assert.True(diagnostic.Recovered);
            Assert.Equal(128, diagnostic.FileOffset);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void SavedRecordRejectsInvalidIdentityBeforeProjection() {
        string path = Path.Combine(Path.GetTempPath(), $"eventviewerx-{Guid.NewGuid():N}.evtx");
        File.WriteAllBytes(path, new byte[] { 1 });
        try {
            var query = new EventLogFileQuery(path) {
                SavedEventReader = new InvalidSavedEventReader()
            };

            Assert.Throws<InvalidDataException>(() => EventLogEngine.ReadFile(query).ToArray());
        } finally {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, "<BookmarkList />")]
    public void CustomSavedReaderRejectsUnsupportedBookmarkSemantics(
        bool includeBookmark,
        string? bookmarkXml) {

        string path = Path.Combine(Path.GetTempPath(), $"eventviewerx-{Guid.NewGuid():N}.evtx");
        File.WriteAllBytes(path, new byte[] { 1 });
        try {
            var query = new EventLogFileQuery(path) {
                IncludeBookmark = includeBookmark,
                BookmarkXml = bookmarkXml,
                SavedEventReader = new FixtureSavedEventReader()
            };

            NotSupportedException exception = Assert.Throws<NotSupportedException>(
                () => EventLogEngine.ReadFile(query).ToArray());
            Assert.Contains("bookmark", exception.Message, StringComparison.OrdinalIgnoreCase);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void ParserBackedBatchCombinesSelectorsWithoutTurningThemIntoAWindowsStructuredQuery() {
        string path = Path.Combine(Path.GetTempPath(), $"eventviewerx-{Guid.NewGuid():N}.evtx");
        var reader = new FixtureSavedEventReader();
        var first = new EventLogFileQuery(path) {
            XPath = "*[System/EventID=1001]",
            SavedEventReader = reader,
            Oldest = true,
            ReadMode = EventReadMode.StructuredData
        };
        var second = new EventLogFileQuery(path) {
            XPath = "*[System/EventID=1002]",
            SavedEventReader = reader,
            Oldest = true,
            ReadMode = EventReadMode.StructuredData
        };

        EventLogBatchQuery consolidated = EventLogBatchConsolidator.Consolidate(
            EventLogBatchQuery.ForFiles(new[] { first, second }));

        EventLogFileQuery result = Assert.Single(consolidated.FileQueries);
        Assert.Empty(consolidated.StructuredQueries);
        Assert.Same(reader, result.SavedEventReader);
        Assert.Equal("(*[System/EventID=1001]) or (*[System/EventID=1002])", result.XPath);
    }

    [Fact]
    public void ParserBackedBatchPreservesIndependentBoundedQueryLimits() {
        string path = Path.Combine(Path.GetTempPath(), $"eventviewerx-{Guid.NewGuid():N}.evtx");
        var reader = new FixtureSavedEventReader();
        var first = new EventLogFileQuery(path) {
            XPath = "*[System/EventID=1001]",
            MaxEvents = 1,
            SavedEventReader = reader
        };
        var second = new EventLogFileQuery(path) {
            XPath = "*[System/EventID=1002]",
            MaxEvents = 1,
            SavedEventReader = reader
        };

        EventLogBatchQuery independent = EventLogBatchConsolidator.Consolidate(
            EventLogBatchQuery.ForFiles(new[] { first, second }));
        first.BatchSourceIdentity = "shared-partition";
        second.BatchSourceIdentity = "shared-partition";
        EventLogBatchQuery shared = EventLogBatchConsolidator.Consolidate(
            EventLogBatchQuery.ForFiles(new[] { first, second }));

        Assert.Equal(2, independent.FileQueries.Count);
        Assert.All(independent.FileQueries, static query => Assert.Equal(1, query.MaxEvents));
        Assert.Equal(
            "(*[System/EventID=1001]) or (*[System/EventID=1002])",
            Assert.Single(shared.FileQueries).XPath);
    }

    private sealed class FixtureSavedEventReader : ISavedEventReader {
        internal bool ObservedOldest { get; private set; }
        internal string ObservedXPath { get; private set; } = string.Empty;

        public IEnumerable<SavedEventRecord> Read(
            EventLogFileQuery query,
            Action<SavedEventReadDiagnostic>? diagnosticHandler = null,
            CancellationToken cancellationToken = default) {

            ObservedOldest = query.Oldest;
            ObservedXPath = query.XPath;
            diagnosticHandler?.Invoke(new SavedEventReadDiagnostic {
                Code = "EVX-SAVED-RECOVERED",
                Severity = SavedEventReadDiagnosticSeverity.Warning,
                Message = "Recovered a fixture record.",
                FileOffset = 128,
                Recovered = true
            });
            yield return CreateRecord(42);
            yield return CreateRecord(43);
        }

        private static SavedEventRecord CreateRecord(long recordId) => new() {
            ProviderName = "Microsoft-Windows-Security-Auditing",
            EventId = 1001,
            RecordId = recordId,
            Channel = "Security",
            Computer = "server01",
            TimeCreatedUtc = new DateTime(2026, 8, 28, 10, 0, 0, DateTimeKind.Utc),
            RawXml = "<Event><EventData><Data Name=\"TargetUserName\">alice</Data></EventData></Event>",
            Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                ["TargetUserName"] = "alice"
            },
            MessageRenderStatus = EventMessageRenderStatus.MessageResourceUnavailable,
            Recovered = true,
            FileOffset = 128
        };
    }

    private sealed class InvalidSavedEventReader : ISavedEventReader {
        public IEnumerable<SavedEventRecord> Read(
            EventLogFileQuery query,
            Action<SavedEventReadDiagnostic>? diagnosticHandler = null,
            CancellationToken cancellationToken = default) {

            yield return new SavedEventRecord {
                EventId = -1,
                TimeCreatedUtc = DateTime.UtcNow
            };
        }
    }
}
