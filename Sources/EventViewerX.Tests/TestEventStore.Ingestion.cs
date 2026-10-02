using EventViewerX.Reporting;
using EventViewerX.Storage;
using Xunit;

namespace EventViewerX.Tests;

public sealed partial class TestEventStore {
    [Fact]
    public async Task StreamingIngestionCommitsBoundedBatchesAndAtomicCheckpoints() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            EventReport report = CreateReport(Enumerable.Range(1, 7)
                .Select(index => (new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(index),
                    (long)index, "alice")).ToArray());
            EventReportSectionSchema[] schemas = report.Sections.Select(EventReportSectionSchema.FromSection).ToArray();
            var batchCounts = new List<int>();
            EventStoreIngestionResult result = await store.WriteStreamAsync(AsStream(report.Rows), schemas,
                batchSize: 3, checkpointFactory: batch => {
                    batchCounts.Add(batch.Count);
                    return IngestionCheckpoint(batch[batch.Count - 1].RecordId!.Value);
                });

            Assert.Equal(new[] { 3, 3, 1 }, batchCounts);
            Assert.Equal(7, result.Attempted);
            Assert.Equal(7, result.Inserted);
            Assert.Equal(3, result.CommittedBatches);
            Assert.Equal(7, result.Checkpoint!.RecordId);
            Assert.Equal(7, (await store.GetCheckpointAsync("ingestion", "HostA", "Application"))!.RecordId);
            Assert.Equal(Enumerable.Range(1, 7).Select(static value => (long?)value),
                (await store.ReadReportAsync(new EventStoreQuery { Oldest = true })).Rows.Select(static row => row.RecordId));

            EventStoreIngestionResult replay = await store.WriteStreamAsync(AsStream(report.Rows), schemas, batchSize: 3);
            Assert.Equal(7, replay.Duplicates);
            Assert.Equal(0, replay.Inserted);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task FailedStreamingSourceLeavesOnlyCommittedPrefixAndCheckpoint() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            EventReport report = CreateReport(Enumerable.Range(1, 4)
                .Select(index => (new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(index),
                    (long)index, "alice")).ToArray());
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.WriteStreamAsync(FailingSource(),
                report.Sections.Select(EventReportSectionSchema.FromSection).ToArray(), batchSize: 3,
                checkpointFactory: batch => IngestionCheckpoint(batch[batch.Count - 1].RecordId!.Value)));

            Assert.Equal(3, (await store.ReadReportAsync(new EventStoreQuery())).Rows.Count);
            Assert.Equal(3, (await store.GetCheckpointAsync("ingestion", "HostA", "Application"))!.RecordId);

            async IAsyncEnumerable<EventReportRow> FailingSource() {
                foreach (EventReportRow row in report.Rows) {
                    await Task.Yield();
                    yield return row;
                }
                throw new InvalidOperationException("source failed after an uncommitted tail");
            }
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task StreamingCheckpointConflictRollsBackItsWholeBatch() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            DateTime start = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            EventReport report = CreateReport(Enumerable.Range(1, 6)
                .Select(index => (start.AddMinutes(index), (long)index, "alice")).ToArray());
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.WriteStreamAsync(ConflictingSource(),
                report.Sections.Select(EventReportSectionSchema.FromSection).ToArray(), batchSize: 3,
                checkpointFactory: batch => IngestionCheckpoint(batch[batch.Count - 1].RecordId!.Value)));
            Assert.Equal(new long?[] { 1, 2, 3, 99 },
                (await store.ReadReportAsync(new EventStoreQuery { Oldest = true })).Rows.Select(static row => row.RecordId));
            Assert.Equal(99, (await store.GetCheckpointAsync("ingestion", "HostA", "Application"))!.RecordId);

            async IAsyncEnumerable<EventReportRow> ConflictingSource() {
                for (int index = 0; index < report.Rows.Count; index++) {
                    if (index == 3) {
                        await store.WriteAsync(CreateReport((start.AddMinutes(99), 99, "bob")), IngestionCheckpoint(99));
                    }
                    yield return report.Rows[index];
                }
            }
        } finally {
            DeleteStore(path);
        }
    }

    private static EventStoreCheckpoint IngestionCheckpoint(long recordId) => new() {
        Consumer = "ingestion", Computer = "HostA", Container = "Application", RecordId = recordId
    };

    private static async IAsyncEnumerable<EventReportRow> AsStream(IEnumerable<EventReportRow> rows) {
        foreach (EventReportRow row in rows) {
            await Task.Yield();
            yield return row;
        }
    }
}