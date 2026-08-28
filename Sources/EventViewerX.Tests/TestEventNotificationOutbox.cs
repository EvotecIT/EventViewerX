using System.Text.Json;
using EventViewerX.Native;
using EventViewerX.Reporting;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventNotificationOutbox {
    [Fact]
    public async Task SavePublishesOneCompleteBatchDirectory() {
        string outbox = CreateTemporaryDirectory();
        try {
            EventReport report = CreateReport("Security digest");
            EventEmailPackage email = await EventReportEmailRenderer.RenderAsync(report);

            string completed = EventNotificationOutbox.Save(outbox, "batch-001", report, email, 1);

            Assert.Equal(Path.Combine(outbox, "batch-001"), completed);
            Assert.True(File.Exists(Path.Combine(completed, "report.html")));
            Assert.True(File.Exists(Path.Combine(completed, "email.html")));
            Assert.True(File.Exists(Path.Combine(completed, "email.txt")));
            string manifestPath = Path.Combine(completed, "batch.json");
            Assert.True(File.Exists(manifestPath));
            EventNotificationBatchManifest? manifest = JsonSerializer.Deserialize<EventNotificationBatchManifest>(
                File.ReadAllText(manifestPath));
            Assert.NotNull(manifest);
            Assert.Equal("batch-001", manifest.BatchId);
            Assert.Equal(1, manifest.EventCount);
            Assert.Equal("Security digest", manifest.Title);
            Assert.Equal(DateTimeKind.Utc, manifest.PersistedUtc.Kind);
            Assert.Empty(Directory.GetDirectories(outbox, "*.pending-*"));
        } finally {
            Directory.Delete(outbox, recursive: true);
        }
    }

    [Fact]
    public async Task PendingDeliverySurvivesRestartAndTracksFailureThenAcknowledgement() {
        string outbox = CreateTemporaryDirectory();
        try {
            EventReport report = CreateReport("Restart-safe digest");
            EventEmailPackage email = await EventReportEmailRenderer.RenderAsync(report);
            EventNotificationOutbox.Save(outbox, "retry-batch", report, email, 1);

            EventNotificationOutboxBatch pending = Assert.Single(EventNotificationOutbox.GetPending(outbox));
            Assert.Equal(email.Html, pending.Html);
            Assert.Equal(email.PlainText, pending.PlainText);
            Assert.Equal("Restart-safe digest", pending.Manifest.Title);
            var failure = new InvalidOperationException("temporary SMTP failure");
            EventNotificationOutbox.RecordFailure(pending, failure);

            EventNotificationOutboxBatch retried = Assert.Single(EventNotificationOutbox.GetPending(outbox));
            EventNotificationOutboxHealth health = EventNotificationOutbox.GetHealth(outbox);
            Assert.Equal(1, retried.Delivery.FailedAttempts);
            Assert.Contains("temporary SMTP failure", retried.Delivery.LastError);
            Assert.NotNull(retried.Delivery.LastAttemptUtc);
            Assert.Equal(1, health.PendingBatches);
            Assert.Equal(1, health.FailedAttempts);
            Assert.NotNull(health.OldestPendingUtc);
            Assert.True(health.OldestPendingAge >= TimeSpan.Zero);
            var retryPolicy = new EventNotificationRetryPolicy {
                InitialDelay = TimeSpan.FromSeconds(10),
                MaximumDelay = TimeSpan.FromSeconds(40)
            };
            DateTime lastAttempt = retried.Delivery.LastAttemptUtc!.Value;
            Assert.Empty(EventNotificationOutbox.GetReady(
                outbox,
                retryPolicy,
                lastAttempt.AddSeconds(9)));
            Assert.Single(EventNotificationOutbox.GetReady(
                outbox,
                retryPolicy,
                lastAttempt.AddSeconds(10)));
            Assert.Equal(TimeSpan.FromSeconds(40), retryPolicy.GetDelay(100));
            EventNotificationOutbox.MarkDelivered(retried);

            Assert.Empty(EventNotificationOutbox.GetPending(outbox));
            Assert.Equal(0, EventNotificationOutbox.GetHealth(outbox).PendingBatches);
            Assert.True(File.Exists(Path.Combine(outbox, "retry-batch", "delivery.json")));
        } finally {
            Directory.Delete(outbox, recursive: true);
        }
    }

    [Fact]
    public async Task PoisonBatchMovesToScopedDeadLetterDirectory() {
        string outbox = CreateTemporaryDirectory();
        try {
            EventReport report = CreateReport();
            EventEmailPackage email = await EventReportEmailRenderer.RenderAsync(report);
            EventNotificationOutbox.Save(outbox, "poison-batch", report, email, 1);
            EventNotificationOutboxBatch pending = Assert.Single(EventNotificationOutbox.GetPending(outbox));

            string deadLetter = EventNotificationOutbox.MoveToDeadLetter(pending);

            Assert.Equal(Path.Combine(outbox, "dead-letter", "poison-batch"), deadLetter);
            Assert.True(Directory.Exists(deadLetter));
            Assert.Empty(EventNotificationOutbox.GetPending(outbox));
        } finally {
            Directory.Delete(outbox, recursive: true);
        }
    }

    [Fact]
    public async Task SaveIsIdempotentForAnExistingBatchIdentifier() {
        string outbox = CreateTemporaryDirectory();
        try {
            EventReport report = CreateReport();
            EventEmailPackage email = await EventReportEmailRenderer.RenderAsync(report);
            string first = EventNotificationOutbox.Save(outbox, "same-batch", report, email, 1);
            DateTime persistedUtc = File.GetLastWriteTimeUtc(Path.Combine(first, "batch.json"));

            string second = EventNotificationOutbox.Save(outbox, "same-batch", report, email, 99);

            Assert.Equal(first, second);
            Assert.Equal(persistedUtc, File.GetLastWriteTimeUtc(Path.Combine(second, "batch.json")));
            EventNotificationBatchManifest? manifest = JsonSerializer.Deserialize<EventNotificationBatchManifest>(
                File.ReadAllText(Path.Combine(second, "batch.json")));
            Assert.NotNull(manifest);
            Assert.Equal(1, manifest.EventCount);
            Assert.Single(Directory.GetDirectories(outbox));
        } finally {
            Directory.Delete(outbox, recursive: true);
        }
    }

    [Fact]
    public async Task CanceledSaveLeavesNoCompletedOrPendingBatch() {
        string outbox = CreateTemporaryDirectory();
        try {
            EventReport report = CreateReport();
            EventEmailPackage email = await EventReportEmailRenderer.RenderAsync(report);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
                EventNotificationOutbox.Save(outbox, "canceled-batch", report, email, 1, cancellation.Token));

            Assert.Empty(Directory.GetDirectories(outbox));
        } finally {
            Directory.Delete(outbox, recursive: true);
        }
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("..\\outside")]
    [InlineData(".")]
    [InlineData("..")]
    public async Task SaveRejectsBatchIdentifiersContainingPathSegments(string batchId) {
        string outbox = CreateTemporaryDirectory();
        try {
            EventReport report = CreateReport();
            EventEmailPackage email = await EventReportEmailRenderer.RenderAsync(report);

            Assert.Throws<ArgumentException>(() =>
                EventNotificationOutbox.Save(outbox, batchId, report, email, 1));

            Assert.Empty(Directory.GetDirectories(outbox));
        } finally {
            Directory.Delete(outbox, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory() {
        string path = Path.Combine(Path.GetTempPath(), "EventViewerX.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static EventReport CreateReport(string? title = null) {
        var metadata = new NativeEventMetadata(
            "Microsoft-Windows-Security-Auditing",
            Guid.Parse("54849625-5478-4994-A5BA-3E3B0328C30D"),
            id: 4624,
            qualifiers: null,
            level: 0,
            task: 12544,
            opcode: 0,
            keywords: unchecked((long)0x8020000000000000),
            timeCreated: new DateTime(2026, 8, 28, 12, 0, 0, DateTimeKind.Utc),
            recordId: 42,
            activityId: null,
            relatedActivityId: null,
            processId: 100,
            threadId: 200,
            logName: "Security",
            machineName: "server01",
            userId: null,
            version: 2);
        var source = new EventObject(metadata, queriedMachine: "server01", containerLog: "Security");
        return EventReportEngine.Create(new object[] { source }, title);
    }
}
