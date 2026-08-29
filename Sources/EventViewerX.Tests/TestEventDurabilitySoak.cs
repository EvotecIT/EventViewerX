using EventViewerX.Native;
using EventViewerX.Reporting;
using System.Globalization;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventDurabilitySoak {
    [Fact]
    [Trait("Category", "Soak")]
    public async Task BoundedDeliveryQueueDrainsSustainedBurstWithoutLossOrReordering() {
        const int eventCount = 100_000;
        const int capacity = 64;
        int nextExpected = 0;
        await using var queue = new EventDeliveryQueue<int>(capacity, (value, _) => {
            Assert.Equal(nextExpected, value);
            nextExpected++;
            return ValueTask.CompletedTask;
        });

        for (int index = 0; index < eventCount; index++) {
            await queue.WriteAsync(index);
        }
        queue.Complete();
        await queue.Completion;

        EventDeliveryQueueSnapshot snapshot = queue.GetSnapshot();
        Assert.Equal(eventCount, nextExpected);
        Assert.Equal(eventCount, snapshot.Accepted);
        Assert.Equal(eventCount, snapshot.Completed);
        Assert.Equal(0, snapshot.Depth);
        Assert.InRange(snapshot.HighWatermark, 1, capacity + 1);
        Assert.Null(snapshot.Failure);
    }

    [Fact]
    [Trait("Category", "Soak")]
    public async Task DurableOutboxSurvivesRepeatedRestartDeliveryAndRetryCycles() {
        const int batchCount = 100;
        string outbox = CreateTemporaryDirectory();
        try {
            EventReport report = CreateReport();
            EventEmailPackage email = await EventReportEmailRenderer.RenderAsync(report);
            for (int index = 0; index < batchCount; index++) {
                EventNotificationOutbox.Save(
                    outbox,
                    $"soak-{index:D4}",
                    report,
                    email,
                    eventCount: 1);
            }

            for (int cycle = 0; cycle < 4; cycle++) {
                EventNotificationOutboxBatch[] pending = EventNotificationOutbox.GetPending(outbox).ToArray();
                foreach (EventNotificationOutboxBatch batch in pending) {
                    int index = int.Parse(
                        batch.Manifest.BatchId.Substring("soak-".Length),
                        CultureInfo.InvariantCulture);
                    if (index % 4 == cycle) {
                        EventNotificationOutboxBatch current = batch;
                        if (index % 10 == 0) {
                            EventNotificationOutbox.RecordFailure(current, new IOException("transient soak failure"));
                            current = EventNotificationOutbox.GetPending(outbox)
                                .Single(candidate => candidate.Manifest.BatchId == batch.Manifest.BatchId);
                        }
                        EventNotificationOutbox.MarkTransportAcknowledged(current);
                        current = EventNotificationOutbox.GetPending(outbox)
                            .Single(candidate => candidate.Manifest.BatchId == batch.Manifest.BatchId);
                        EventNotificationOutbox.MarkDelivered(current);
                    }
                }
            }

            EventNotificationOutboxHealth health = EventNotificationOutbox.GetHealth(outbox);
            Assert.Equal(0, health.PendingBatches);
            Assert.Equal(0, health.FailedAttempts);
            Assert.Null(health.OldestPendingUtc);
            Assert.True(health.DeliveredBytes > 0);
            Assert.Equal(0, health.PendingBytes);
            Assert.Equal(0, health.DeadLetterBytes);
            Assert.Equal(0, health.StagingBytes);
            Assert.Equal(batchCount, Directory.GetDirectories(outbox)
                .Count(static directory => !directory.EndsWith("dead-letter", StringComparison.OrdinalIgnoreCase)));
        } finally {
            Directory.Delete(outbox, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory() {
        string path = Path.Combine(Path.GetTempPath(), "EventViewerX.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static EventReport CreateReport() {
        var metadata = new NativeEventMetadata(
            "Microsoft-Windows-Security-Auditing",
            providerId: null,
            id: 4624,
            qualifiers: null,
            level: 0,
            task: 12544,
            opcode: 0,
            keywords: 0,
            timeCreated: new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc),
            recordId: 1,
            activityId: null,
            relatedActivityId: null,
            processId: 1,
            threadId: 1,
            logName: "Security",
            machineName: "soak-host",
            userId: null,
            version: 1);
        var source = new EventObject(metadata, queriedMachine: "soak-host", containerLog: "Security");
        return EventReportEngine.Create(new object[] { source }, "Durability soak report");
    }
}
