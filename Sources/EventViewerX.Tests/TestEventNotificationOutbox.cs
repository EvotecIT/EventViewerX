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
            Assert.Equal(EventNotificationBatchManifest.CurrentSchemaVersion, manifest.SchemaVersion);
            Assert.Equal("batch-001", manifest.BatchId);
            Assert.Equal(1, manifest.EventCount);
            Assert.Equal("Security digest", manifest.Title);
            Assert.Equal(DateTimeKind.Utc, manifest.PersistedUtc.Kind);
            Assert.False(manifest.RequiresExternalTransport);
            Assert.Empty(Directory.GetDirectories(outbox, "*.pending-*"));
        } finally {
            Directory.Delete(outbox, recursive: true);
        }
    }

    [Fact]
    public async Task BatchPersistsExternalTransportRequirement() {
        string outbox = CreateTemporaryDirectory();
        try {
            EventReport report = CreateReport("SMTP digest");
            EventEmailPackage email = await EventReportEmailRenderer.RenderAsync(report);

            EventNotificationOutbox.Save(
                outbox,
                "smtp-batch",
                report,
                email,
                1,
                Array.Empty<EventNotificationCheckpointBoundary>(),
                requiresExternalTransport: true);

            Assert.True(Assert.Single(EventNotificationOutbox.GetPending(outbox))
                .Manifest.RequiresExternalTransport);
        } finally {
            Directory.Delete(outbox, recursive: true);
        }
    }

    [Fact]
    public async Task BatchRejectsAmbiguousCheckpointBoundaries() {
        string outbox = CreateTemporaryDirectory();
        try {
            EventReport report = CreateReport("Invalid checkpoint digest");
            EventEmailPackage email = await EventReportEmailRenderer.RenderAsync(report);
            var missingExpectedUpdate = new EventNotificationCheckpointBoundary {
                Consumer = "watcher",
                Computer = "server01",
                Container = "Security|query",
                ExpectedExists = true
            };

            Assert.Throws<InvalidDataException>(() => EventNotificationOutbox.Save(
                outbox,
                "invalid-checkpoint",
                report,
                email,
                1,
                new[] { missingExpectedUpdate }));

            DateTime expectedUpdated = new(2026, 8, 28, 11, 0, 0, DateTimeKind.Utc);
            var valid = new EventNotificationCheckpointBoundary {
                Consumer = "watcher",
                Computer = "server01",
                Container = "Security|query",
                ExpectedExists = true,
                ExpectedUpdatedAtUtc = expectedUpdated
            };
            Assert.Throws<InvalidDataException>(() => EventNotificationOutbox.Save(
                outbox,
                "duplicate-checkpoint",
                report,
                email,
                1,
                new[] {
                    valid,
                    new EventNotificationCheckpointBoundary {
                        Consumer = "WATCHER",
                        Computer = "SERVER01",
                        Container = "security|QUERY",
                        ExpectedExists = true,
                        ExpectedUpdatedAtUtc = expectedUpdated
                    }
                }));
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
            Assert.Equal(lastAttempt.AddSeconds(10), retryPolicy.GetNextAttemptUtc(retried.Delivery));
            Assert.Equal(
                TimeSpan.FromSeconds(1),
                retryPolicy.GetRemainingDelay(retried.Delivery, lastAttempt.AddSeconds(9)));
            Assert.Equal(
                TimeSpan.Zero,
                retryPolicy.GetRemainingDelay(retried.Delivery, lastAttempt.AddSeconds(10)));
            Assert.Empty(EventNotificationOutbox.GetReady(
                outbox,
                retryPolicy,
                lastAttempt.AddSeconds(9)));
            Assert.Single(EventNotificationOutbox.GetReady(
                outbox,
                retryPolicy,
                lastAttempt.AddSeconds(10)));
            Assert.Equal(TimeSpan.FromSeconds(40), retryPolicy.GetDelay(100));
            EventNotificationOutbox.MarkTransportAcknowledged(retried);

            EventNotificationOutboxBatch acknowledged = Assert.Single(EventNotificationOutbox.GetPending(outbox));
            Assert.NotNull(acknowledged.Delivery.TransportAcknowledgedUtc);
            Assert.Null(acknowledged.Delivery.DeliveredUtc);
            EventNotificationOutbox.MarkDelivered(acknowledged);

            Assert.Empty(EventNotificationOutbox.GetPending(outbox));
            Assert.Equal(0, EventNotificationOutbox.GetHealth(outbox).PendingBatches);
            Assert.True(File.Exists(Path.Combine(outbox, "retry-batch", "delivery.json")));
        } finally {
            Directory.Delete(outbox, recursive: true);
        }
    }

    [Fact]
    public async Task BatchRetainsCheckpointCompareAndSwapBoundaryAcrossRestart() {
        string outbox = CreateTemporaryDirectory();
        try {
            EventReport report = CreateReport("Checkpoint-aware digest");
            EventEmailPackage email = await EventReportEmailRenderer.RenderAsync(report);
            DateTime expectedUpdated = new(2026, 8, 28, 11, 0, 0, DateTimeKind.Utc);
            EventNotificationOutbox.Save(
                outbox,
                "checkpoint-batch",
                report,
                email,
                1,
                new[] {
                    new EventNotificationCheckpointBoundary {
                        Consumer = "watcher",
                        Computer = "server01",
                        Container = "Security|query",
                        RecordId = 42,
                        BookmarkXml = "<Bookmark Target='42' />",
                        ExpectedExists = true,
                        ExpectedRecordId = 41,
                        ExpectedBookmarkXml = "<Bookmark Target='41' />",
                        ExpectedUpdatedAtUtc = expectedUpdated
                    }
                });

            EventNotificationCheckpointBoundary boundary = Assert.Single(
                Assert.Single(EventNotificationOutbox.GetPending(outbox)).Manifest.Checkpoints);

            Assert.Equal("watcher", boundary.Consumer);
            Assert.Equal(42, boundary.RecordId);
            Assert.Equal(41, boundary.ExpectedRecordId);
            Assert.Equal(expectedUpdated, boundary.ExpectedUpdatedAtUtc);
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

    [Fact]
    public async Task SaveRejectsBatchBeforePublicationWhenBatchByteLimitIsExceeded() {
        string outbox = CreateTemporaryDirectory();
        try {
            EventReport report = CreateReport("Oversized digest");
            EventEmailPackage email = await EventReportEmailRenderer.RenderAsync(report);
            var limits = new EventNotificationOutboxLimits(
                maximumBatchBytes: 1,
                maximumOutboxBytes: 1,
                maximumPendingBatches: 1);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                EventNotificationOutbox.Save(
                    outbox,
                    "oversized-batch",
                    report,
                    email,
                    1,
                    Array.Empty<EventNotificationCheckpointBoundary>(),
                    requiresExternalTransport: false,
                    limits));

            Assert.Contains("batch limit", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(Directory.GetDirectories(outbox));
        } finally {
            Directory.Delete(outbox, recursive: true);
        }
    }

    [Fact]
    public async Task SaveFailsClosedAtPendingBatchAndTotalByteCapacity() {
        string outbox = CreateTemporaryDirectory();
        try {
            EventReport report = CreateReport("Bounded digest");
            EventEmailPackage email = await EventReportEmailRenderer.RenderAsync(report);
            var onePending = new EventNotificationOutboxLimits(
                maximumBatchBytes: 16L * 1024 * 1024,
                maximumOutboxBytes: 32L * 1024 * 1024,
                maximumPendingBatches: 1);
            EventNotificationOutbox.Save(
                outbox,
                "bounded-001",
                report,
                email,
                1,
                Array.Empty<EventNotificationCheckpointBoundary>(),
                requiresExternalTransport: false,
                onePending);

            InvalidOperationException pendingException = Assert.Throws<InvalidOperationException>(() =>
                EventNotificationOutbox.Save(
                    outbox,
                    "bounded-002",
                    report,
                    email,
                    1,
                    Array.Empty<EventNotificationCheckpointBoundary>(),
                    requiresExternalTransport: false,
                    onePending));
            Assert.Contains("pending batches", pendingException.Message, StringComparison.OrdinalIgnoreCase);

            EventNotificationOutboxBatch first = Assert.Single(EventNotificationOutbox.GetPending(outbox));
            EventNotificationOutbox.MarkDelivered(first);
            long retainedBytes = Directory.GetFiles(outbox, "*", SearchOption.AllDirectories)
                .Sum(static path => new FileInfo(path).Length);
            var byteBound = new EventNotificationOutboxLimits(
                maximumBatchBytes: retainedBytes,
                maximumOutboxBytes: retainedBytes + 1,
                maximumPendingBatches: 2);

            InvalidOperationException byteException = Assert.Throws<InvalidOperationException>(() =>
                EventNotificationOutbox.Save(
                    outbox,
                    "bounded-002",
                    report,
                    email,
                    1,
                    Array.Empty<EventNotificationCheckpointBoundary>(),
                    requiresExternalTransport: false,
                    byteBound));
            Assert.Contains("outbox", byteException.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("byte limit", byteException.Message, StringComparison.OrdinalIgnoreCase);

            EventNotificationOutboxHealth health = EventNotificationOutbox.GetHealth(outbox);
            Assert.Equal(retainedBytes, health.TotalBytes);
            Assert.Equal(retainedBytes, health.DeliveredBytes);
            Assert.Equal(0, health.PendingBytes);
            Assert.Equal(0, health.DeadLetterBytes);
            Assert.Equal(0, health.StagingBytes);
        } finally {
            Directory.Delete(outbox, recursive: true);
        }
    }

    [Fact]
    public async Task NewerManifestSchemaFailsClosedWithoutMovingOrAcknowledgingBatch() {
        string outbox = CreateTemporaryDirectory();
        try {
            EventReport report = CreateReport("Future outbox schema");
            EventEmailPackage email = await EventReportEmailRenderer.RenderAsync(report);
            string directory = EventNotificationOutbox.Save(
                outbox,
                "future-schema",
                report,
                email,
                1);
            string manifestPath = Path.Combine(directory, "batch.json");
            EventNotificationBatchManifest manifest = JsonSerializer.Deserialize<EventNotificationBatchManifest>(
                File.ReadAllText(manifestPath))!;
            manifest.SchemaVersion = EventNotificationBatchManifest.CurrentSchemaVersion + 1;
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                EventNotificationOutbox.GetPending(outbox));

            Assert.Contains("unsupported manifest schema", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True(Directory.Exists(directory));
            Assert.False(File.Exists(Path.Combine(directory, "delivery.json")));
        } finally {
            Directory.Delete(outbox, recursive: true);
        }
    }

    [Fact]
    public async Task MissingManifestSchemaFailsClosedWithoutAcknowledgingLegacyBatch() {
        string outbox = CreateTemporaryDirectory();
        try {
            EventReport report = CreateReport("Legacy outbox schema");
            EventEmailPackage email = await EventReportEmailRenderer.RenderAsync(report);
            string directory = EventNotificationOutbox.Save(
                outbox,
                "legacy-schema",
                report,
                email,
                1,
                Array.Empty<EventNotificationCheckpointBoundary>(),
                requiresExternalTransport: true);
            string manifestPath = Path.Combine(directory, "batch.json");
            Dictionary<string, JsonElement> manifest = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                File.ReadAllText(manifestPath))!;
            Assert.True(manifest.Remove(nameof(EventNotificationBatchManifest.SchemaVersion)));
            Assert.True(manifest.Remove(nameof(EventNotificationBatchManifest.RequiresExternalTransport)));
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                EventNotificationOutbox.GetPending(outbox));

            Assert.Contains("no explicit manifest schema version", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True(Directory.Exists(directory));
            Assert.False(File.Exists(Path.Combine(directory, "delivery.json")));
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
