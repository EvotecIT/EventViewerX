using Xunit;

namespace EventViewerX.Tests;

public sealed class TestQueryDiagnostics {
    [Fact]
    public void DeclarativeExplanationResolvesProjectionAndLimitsWithoutReadingEvents() {
        var definition = new EventDefinition {
            Name = "ExplainCustom", Sources = new[] {
                new EventDefinitionSource { LogName = "Application", EventIds = new[] { 1 } }
            }
        };
        EventQueryExplanation plan = EventDefinitionEngine.Explain(new EventDefinitionQuery(definition) {
            MaxEvents = 3, MaxCandidates = 10, Oldest = true
        });
        Assert.NotEmpty(plan.Sources);
        Assert.Contains("Declarative projection: ExplainCustom", plan.ManagedStages);
        Assert.Equal(3, plan.ResultLimit);
        Assert.Equal(10, plan.CandidateLimit);
        Assert.True(plan.Oldest);
    }

    [Fact]
    public void TypedExplanationKeepsSourceAndPredicatePlanningTogether() {
        EventQueryExplanation plan = EventTypeEngine.Explain(new EventTypeQuery(new[] { EventType.OSStartup }) {
            MaxEvents = 3, MaxCandidates = 10,
            Predicate = EventPredicate.Compare("EventId", EventPredicateOperator.Equal, 12)
        });
        Assert.NotEmpty(plan.Sources);
        Assert.NotNull(plan.PredicatePlan);
        Assert.True(plan.HasNativeFilter);
        Assert.Equal(3, plan.ResultLimit);
        Assert.Equal(10, plan.CandidateLimit);
        Assert.All(plan.Sources, static source => Assert.Contains("EventID", source.NativeFilter, StringComparison.Ordinal));
    }

    [Fact]
    public void ExplainResolvesOfflinePartitionsWithoutReadingInvalidEventContent() {
        string path = Path.GetTempFileName();
        try {
            EventQueryExplanation plan = EventQueryPlanner.Explain(new EventQueryDefinition {
                Paths = new[] { path },
                Filter = new EventFilter { EventIds = Enumerable.Range(1, 40).ToArray() },
                Options = new EventLogQueryOptions { MaxEvents = 10, ReadMode = EventReadMode.Metadata, Oldest = true }
            });
            Assert.NotEmpty(plan.Sources);
            Assert.All(plan.Sources, source => {
                Assert.Equal(EventLogQuerySourceKind.File, source.SourceKind);
                Assert.Equal(EventReadMode.Metadata, source.ReadMode);
                Assert.Contains("EventID", source.NativeFilter, StringComparison.Ordinal);
            });
            Assert.True(plan.Oldest);
            Assert.Equal(10, plan.MaxEvents);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task AsyncDiagnosticsCountCandidatesDeliveryAndSourceFailures() {
        string path = Path.GetTempFileName();
        try {
            var query = EventLogBatchQuery.ForFiles(new[] {
                new EventLogFileQuery(path) { SavedEventReader = new DiagnosticReader(), Oldest = true },
                new EventLogFileQuery(path) { SavedEventReader = new DiagnosticReader(fail: true), Oldest = true }
            });
            query.ContinueOnError = true;
            var info = new EventQueryExecutionInfo();
            var events = new List<EventObject>();
            await foreach (EventObject item in EventLogBatchEngine.ReadAsync(query, info)) {
                events.Add(item);
            }
            Assert.Equal(3, events.Count);
            Assert.Equal(3, info.NativeEventsRead);
            Assert.Equal(3, info.EventsEmitted);
            Assert.Equal(1, info.SourceFailures);
            Assert.Equal(0, info.BufferedEvents);
            Assert.InRange(info.PeakBufferedEvents, 1, 65);
            Assert.True(info.Completed);
            Assert.False(info.Canceled);
            Assert.True(info.Duration >= info.PrimingDuration);
        } finally {
            File.Delete(path);
        }
    }

    private sealed class DiagnosticReader : ISavedEventReader {
        private readonly bool _fail;
        internal DiagnosticReader(bool fail = false) => _fail = fail;
        public IEnumerable<SavedEventRecord> Read(EventLogFileQuery query,
            Action<SavedEventReadDiagnostic>? diagnosticHandler = null,
            CancellationToken cancellationToken = default) {
            if (_fail) {
                throw new InvalidDataException("source failed");
            }
            for (int index = 1; index <= 3; index++) {
                yield return new SavedEventRecord {
                    ProviderName = "Audit", EventId = 1, RecordId = index, Channel = "Application", Computer = "HostA",
                    TimeCreatedUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(index)
                };
            }
        }
    }
}
