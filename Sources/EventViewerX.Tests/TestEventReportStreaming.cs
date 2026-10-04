using System.Text.Json;
using EventViewerX.Reporting;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventReportStreaming {
    [Theory]
    [InlineData("generic", 0, false)]
    [InlineData("typed", 0, false)]
    [InlineData("custom", 0, false)]
    [InlineData("generic", 2, false)]
    [InlineData("typed", 2, false)]
    [InlineData("custom", 2, false)]
    [InlineData("generic", 0, true)]
    [InlineData("typed", 0, true)]
    [InlineData("custom", 0, true)]
    public async Task StreamingPreservesSnapshotRowsAndCompletion(string shape, long maximum, bool loss) {
        EventReportRequest request = CreateRequest(shape, new CountingReader(3, loss));
        request.MaxEvents = maximum;
        EventReport report = await EventReportEngine.QueryAsync(request);
        var expected = new List<string>();
        foreach (EventReportRow row in report.Rows) {
            EventReportSection section = report.Sections.Single(section => section.Rows.Contains(row));
            expected.Add(JsonSerializer.Serialize(EventReportJsonProjection.Project(row, section)));
        }
        var actual = new List<string>();
        EventReportSummary summary = await EventReportEngine.StreamRowsAsync(request, (row, section, _) => {
            Assert.Empty(section.Rows);
            actual.Add(JsonSerializer.Serialize(EventReportJsonProjection.Project(row, section)));
            return Task.CompletedTask;
        });

        Assert.Equal(expected, actual);
        Assert.Equal(JsonSerializer.Serialize(EventReportSummary.Create(report)), JsonSerializer.Serialize(summary));
        Assert.Equal(maximum == 0 ? 3 : maximum, summary.EventCount);
        Assert.Equal(maximum == 0 && !loss, summary.IsComplete);
        if (shape == "custom") {
            Assert.Contains("\"EventId\":\"domain-value\"", actual[0], StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task StreamingDeliversBeforeExhaustionAndAwaitsTheConsumer() {
        var reader = new CountingReader(1000);
        var firstRow = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int delivered = 0;
        Task<EventReportSummary> execution = EventReportEngine.StreamRowsAsync(CreateRequest("generic", reader), async (_, _, _) => {
            delivered++;
            if (delivered == 1) {
                firstRow.SetResult(true);
                await release.Task;
            }
        });
        await firstRow.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(execution.IsCompleted);
        Assert.Equal(1, delivered);
        Assert.InRange(reader.ReadCount, 1, 68); // The reader retains its existing 64-row bounded buffer and merge heads.
        Assert.False(reader.Disposed);
        release.SetResult(true);
        EventReportSummary summary = await execution;
        Assert.Equal(1000, summary.EventCount);
        Assert.Equal(1000, delivered);
        Assert.True(reader.Disposed);
    }

    [Theory]
    [InlineData("generic", false)]
    [InlineData("typed", false)]
    [InlineData("custom", false)]
    [InlineData("generic", true)]
    [InlineData("typed", true)]
    [InlineData("custom", true)]
    public async Task ConsumerCancellationOrFailureClosesSources(string shape, bool failure) {
        var reader = new CountingReader(1000);
        using var cancellation = new CancellationTokenSource();
        int delivered = 0;
        Task<EventReportSummary> execution = EventReportEngine.StreamRowsAsync(CreateRequest(shape, reader), (_, _, token) => {
            Assert.Equal(cancellation.Token, token);
            delivered++;
            if (failure) {
                throw new IOException("Consumer failed.");
            }
            cancellation.Cancel();
            return Task.CompletedTask;
        }, cancellation.Token);

        if (failure) {
            await Assert.ThrowsAsync<IOException>(() => execution);
        } else {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
        }
        await reader.Closed.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, delivered);
        Assert.True(reader.Disposed);
        Assert.True(reader.ReadCount < 1000);
    }

    [Theory]
    [InlineData(EventReadMode.Metadata, false, false)]
    [InlineData(EventReadMode.Message, false, true)]
    [InlineData(EventReadMode.StructuredData, true, false)]
    [InlineData(EventReadMode.StructuredDataAndMessage, true, true)]
    [InlineData(EventReadMode.Full, true, true)]
    public async Task GenericReadModesControlPayloadAndMessage(EventReadMode mode, bool payload, bool message) {
        var reader = new CountingReader(1);
        EventReportRequest request = CreateRequest("generic", reader);
        request.ReadMode = mode;
        EventReport report = await EventReportEngine.QueryAsync(request);
        EventReportRow row = Assert.Single(report.Rows);
        Assert.Equal(mode, reader.ReadMode);
        Assert.Equal(payload, row.Values.ContainsKey("TargetUserName"));
        Assert.Equal(message ? "Rendered logon message" : string.Empty, row.Message);
        Assert.Equal(4624, row.EventId);
        Assert.False(string.IsNullOrEmpty(row.ObservationIdentity));
    }

    [Theory]
    [InlineData("typed", EventReadMode.Metadata)]
    [InlineData("custom", EventReadMode.Message)]
    [InlineData("generic", EventReadMode.RawXml)]
    public async Task UnsupportedProjectionModesAreRejectedBeforeOpeningSources(string shape, EventReadMode mode) {
        var reader = new CountingReader(1);
        EventReportRequest request = CreateRequest(shape, reader);
        request.ReadMode = mode;
        await Assert.ThrowsAsync<InvalidOperationException>(() => EventReportEngine.QueryAsync(request));
        Assert.Equal(0, reader.ReadCount);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(1000, false)]
    public async Task CompletenessLossCannotDisappearOrRetainUnboundedDiagnosticDetails(int lossCount, bool empty) {
        var reader = new CountingReader(1, true, lossCount, empty, new string('x', 6000));
        EventReportRequest request = CreateRequest("generic", reader);
        int forwarded = 0;
        request.SavedEventDiagnosticHandler = _ => forwarded++;
        EventReportSummary summary = await EventReportEngine.StreamRowsAsync(request, (_, _, _) => Task.CompletedTask);
        Assert.False(summary.IsComplete);
        Assert.False(Assert.Single(summary.Coverage).Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(summary.CompletenessDiagnostic));
        Assert.Equal(lossCount, forwarded);
        Assert.True(summary.CompletenessDiagnostic!.Length < 66000);
        if (!empty) {
            Assert.Contains("Details are sampled", summary.CompletenessDiagnostic, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("typed")]
    [InlineData("custom")]
    public async Task StructuredOnlyReadModeReachesEachTypedReader(string shape) {
        var reader = new CountingReader(1);
        EventReportRequest request = CreateRequest(shape, reader);
        request.ReadMode = EventReadMode.StructuredData;
        EventReportSummary summary = await EventReportEngine.StreamRowsAsync(request, (row, _, _) => {
            Assert.Empty(row.Message);
            return Task.CompletedTask;
        });
        Assert.Equal(EventReadMode.StructuredData, reader.ReadMode);
        Assert.Equal(1, summary.EventCount);
        Assert.True(summary.IsComplete);
    }

    [Fact]
    public void GenericSnapshotColumnsRemainScopedToTheirOwnPayloads() {
        EventObject Source(string field, string value) => new SavedEventRecord {
            ProviderName = "Audit", EventId = 1, Channel = "Application", Computer = "server01",
            TimeCreatedUtc = new DateTime(2026, 10, 4, 10, 0, 0, DateTimeKind.Utc),
            Data = new Dictionary<string, string> { [field] = value }
        }.ToEventObject("fixture.evtx", EventReadMode.StructuredData);
        EventReport first = EventReportEngine.Create(new object[] { Source("FirstField", "first") });
        EventReport second = EventReportEngine.Create(new object[] { Source("SecondField", "second") });
        EventReportSection firstSection = Assert.Single(first.Sections);
        EventReportSection secondSection = Assert.Single(second.Sections);
        Assert.Contains(firstSection.Columns, column => column.Name == "FirstField" && column.ValueType == typeof(string));
        Assert.DoesNotContain(firstSection.Columns, column => column.Name == "SecondField");
        Assert.Contains(secondSection.Columns, column => column.Name == "SecondField" && column.ValueType == typeof(string));
        Assert.DoesNotContain(secondSection.Columns, column => column.Name == "FirstField");
    }

    private static EventReportRequest CreateRequest(string shape, CountingReader reader) {
        string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "Tests", "Logs", "NamedFilterExamples.evtx"));
        EventReportRequest request = shape switch {
            "typed" => EventReportRequest.ForTypes(EventType.ADUserLogon),
            "custom" => EventReportRequest.ForDefinition(new EventDefinition {
                Name = "PortableLogon",
                Sources = new[] {
                    new EventDefinitionSource { LogName = "Security", EventIds = new[] { 4624 },
                        ProviderNames = new[] { "Microsoft-Windows-Security-Auditing" } }
                },
                Fields = new[] {
                    new EventDefinitionField { Name = "EventId", Source = EventFieldSource.Constant,
                        SourceName = "domain-value", ValueKind = EventFieldValueKind.String }
                }
            }),
            _ => EventReportRequest.ForFiles(path)
        };
        request.Paths = new[] { path };
        request.SavedEventReader = reader;
        request.Oldest = true;
        return request;
    }

    private sealed class CountingReader : ISavedEventReader {
        private readonly int count;
        private readonly bool loss;
        private readonly int lossCount;
        private readonly bool emptyLoss;
        private readonly string? lossMessage;
        private TaskCompletionSource<bool> closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal CountingReader(int count, bool loss = false, int lossCount = 1, bool emptyLoss = false, string? lossMessage = null) {
            this.count = count;
            this.loss = loss;
            this.lossCount = lossCount;
            this.emptyLoss = emptyLoss;
            this.lossMessage = lossMessage;
        }
        internal int ReadCount { get; private set; }
        internal bool Disposed { get; private set; }
        internal Task Closed => closed.Task;
        internal EventReadMode ReadMode { get; private set; }
        public IEnumerable<SavedEventRecord> Read(EventLogFileQuery query,
            Action<SavedEventReadDiagnostic>? diagnosticHandler = null, CancellationToken cancellationToken = default) {
            ReadCount = 0;
            Disposed = false;
            closed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ReadMode = query.ReadMode;
            try {
                for (int index = 0; index < count; index++) {
                    cancellationToken.ThrowIfCancellationRequested();
                    ReadCount++;
                    yield return new SavedEventRecord {
                        ProviderName = "Microsoft-Windows-Security-Auditing", EventId = 4624, RecordId = index + 1,
                        Channel = "Security", Computer = "server01",
                        TimeCreatedUtc = new DateTime(2026, 10, 4, 10, 0, 0, DateTimeKind.Utc).AddSeconds(index),
                        RawXml = "<Event><EventData><Data Name=\"TargetUserName\">alice</Data></EventData></Event>",
                        Data = new Dictionary<string, string> { ["TargetUserName"] = "alice" },
                        Message = "Rendered logon message", MessageRenderStatus = EventMessageRenderStatus.Rendered
                    };
                }
                if (loss) {
                    for (int index = 0; index < lossCount; index++) {
                        diagnosticHandler?.Invoke(new SavedEventReadDiagnostic { Code = emptyLoss ? string.Empty : "EVXEVTX202",
                            Severity = SavedEventReadDiagnosticSeverity.Warning,
                            Message = emptyLoss ? string.Empty : index + (lossMessage ?? "One malformed record was skipped."),
                            AffectsCompleteness = true });
                    }
                }
            } finally {
                Disposed = true;
                closed.TrySetResult(true);
            }
        }
    }
}
