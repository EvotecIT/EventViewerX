using System.Reflection;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestQueryReliability {
    [Fact]
    public void PartitionDeduplicationAcceptsReusedRecordIdsWithDifferentMetadata() {
        var deduplicator = new EventDeliveryDeduplicator();
        EventObject first = CreateEvent(7, new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc));
        EventObject nextGeneration = CreateEvent(7, first.TimeCreated.AddDays(1));

        Assert.True(deduplicator.TryAccept(first));
        Assert.False(deduplicator.TryAccept(CreateEvent(7, first.TimeCreated)));
        Assert.True(deduplicator.TryAccept(nextGeneration));
        Assert.False(deduplicator.TryAccept(nextGeneration));
    }

    [Theory]
    [InlineData("first\nsecond", "*", true)]
    [InlineData("first\r\nsecond", "first*second", true)]
    [InlineData("x\ny", "x?y", true)]
    [InlineData("foo\n", "foo", false)]
    [InlineData("foo\n", "foo*", true)]
    public void WildcardsMatchEntireMultilineValues(string text, string pattern, bool expected) {
        var fields = new Dictionary<string, object?> { ["Message"] = text };
        EventPredicate predicate = EventPredicate.Compare(
            "Message", EventPredicateOperator.MatchesWildcard, pattern);

        Assert.Equal(expected, EventPredicateEvaluator.Matches(predicate, fields));
        Assert.Equal(expected, EventPredicateEvaluator.CompileFields(predicate)(fields));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    public void StringOrderingUsesTheSameCasePolicyAsEquality(
        bool ignoreCase, bool greater, bool equal) {

        var fields = new Dictionary<string, object?> { ["Name"] = "B" };
        EventPredicate predicate = EventPredicate.Compare("Name", EventPredicateOperator.GreaterThan, "b");
        predicate.IgnoreCase = ignoreCase;
        Assert.Equal(greater, EventPredicateEvaluator.Matches(predicate, fields));
        Assert.Equal(greater, EventPredicateEvaluator.CompileFields(predicate)(fields));
        predicate.Operator = EventPredicateOperator.Equal;
        Assert.Equal(equal, EventPredicateEvaluator.Matches(predicate, fields));
    }

    [Fact]
    public async Task AsyncBatchKeepsCancellationRegistrationsBoundedDuringLongReads() {
        using var cancellation = new CancellationTokenSource();
        string path = Path.GetTempFileName();
        try {
            var query = EventLogBatchQuery.ForFiles(new[] {
                new EventLogFileQuery(path) { SavedEventReader = new SlowSavedReader(), Oldest = true }
            });
            await using IAsyncEnumerator<EventObject> events = EventLogBatchEngine.ReadAsync(
                query, cancellation.Token).GetAsyncEnumerator();
            for (int index = 0; index < 1000; index++) {
                Assert.True(await events.MoveNextAsync());
                Assert.Equal(index + 1, events.Current.RecordId);
            }

            // Inspect the supported runtimes before completing the query or disposing the token
            // source can hide retained callbacks. One query must not retain one callback per row.
            object? registrations = typeof(CancellationTokenSource)
                .GetField("_registrations", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(cancellation);
            object? callback = registrations?.GetType()
                .GetField("Callbacks", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                .GetValue(registrations);
            int count = 0;
            while (callback != null && count <= 4) {
                count++;
                callback = callback.GetType().GetField("Next",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(callback);
            }
            Assert.InRange(count, 0, 4);
        } finally {
            File.Delete(path);
        }
    }

    private sealed class SlowSavedReader : ISavedEventReader {
        public IEnumerable<SavedEventRecord> Read(EventLogFileQuery query,
            Action<SavedEventReadDiagnostic>? diagnosticHandler = null,
            CancellationToken cancellationToken = default) {
            for (int index = 1; index <= 2000; index++) {
                cancellationToken.ThrowIfCancellationRequested();
                Thread.Sleep(1);
                yield return new SavedEventRecord {
                    RecordId = index, EventId = 1, Channel = "Application", Computer = "HostA",
                    ProviderName = "Audit", TimeCreatedUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(index)
                };
            }
        }
    }

    private static EventObject CreateEvent(long recordId, DateTime time) => new SavedEventRecord {
        EventId = 1,
        RecordId = recordId,
        TimeCreatedUtc = time,
        ProviderName = "Audit",
        Channel = "Application",
        Computer = "HostA"
    }.ToEventObject("fixture.evtx", EventReadMode.Metadata);
}