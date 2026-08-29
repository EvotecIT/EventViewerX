using System.Text.Json;

namespace EventViewerX.Reporting;

/// <summary>Publishes a rendered notification batch as one atomic, retry-safe outbox directory.</summary>
public static partial class EventNotificationOutbox {
    /// <summary>
    /// Writes the report, email bodies, and batch manifest to a temporary directory before atomically
    /// publishing the completed directory. Reusing an existing batch identifier is idempotent.
    /// </summary>
    /// <param name="outboxPath">Root directory that owns completed notification batches.</param>
    /// <param name="batchId">File-name-safe identifier for this delivery attempt.</param>
    /// <param name="report">Rendered report model for the batch.</param>
    /// <param name="email">Rendered transport-neutral email payload.</param>
    /// <param name="eventCount">Number of events included in the batch.</param>
    /// <param name="cancellationToken">Cancellation requested before publication.</param>
    /// <returns>The full path of the completed batch directory.</returns>
    public static string Save(
        string outboxPath,
        string batchId,
        EventReport report,
        EventEmailPackage email,
        int eventCount,
        CancellationToken cancellationToken = default) => Save(
            outboxPath,
            batchId,
            report,
            email,
            eventCount,
            Array.Empty<EventNotificationCheckpointBoundary>(),
            cancellationToken);

    /// <summary>Persists a batch together with the checkpoint boundaries acknowledged by its delivery.</summary>
    public static string Save(
        string outboxPath,
        string batchId,
        EventReport report,
        EventEmailPackage email,
        int eventCount,
        IEnumerable<EventNotificationCheckpointBoundary> checkpoints,
        CancellationToken cancellationToken = default) => Save(
            outboxPath,
            batchId,
            report,
            email,
            eventCount,
            checkpoints,
            requiresExternalTransport: false,
            cancellationToken);

    /// <summary>Persists a batch together with its transport requirement and checkpoint boundaries.</summary>
    public static string Save(
        string outboxPath,
        string batchId,
        EventReport report,
        EventEmailPackage email,
        int eventCount,
        IEnumerable<EventNotificationCheckpointBoundary> checkpoints,
        bool requiresExternalTransport,
        CancellationToken cancellationToken = default) => Save(
            outboxPath,
            batchId,
            report,
            email,
            eventCount,
            checkpoints,
            requiresExternalTransport,
            new EventNotificationOutboxLimits(),
            cancellationToken);

    /// <summary>Persists a bounded batch while enforcing cross-process outbox capacity.</summary>
    public static string Save(
        string outboxPath,
        string batchId,
        EventReport report,
        EventEmailPackage email,
        int eventCount,
        IEnumerable<EventNotificationCheckpointBoundary> checkpoints,
        bool requiresExternalTransport,
        EventNotificationOutboxLimits limits,
        CancellationToken cancellationToken = default) {

        if (string.IsNullOrWhiteSpace(outboxPath)) {
            throw new ArgumentException("Outbox path cannot be empty.", nameof(outboxPath));
        }
        ValidateBatchId(batchId);
        if (report == null) {
            throw new ArgumentNullException(nameof(report));
        }
        if (email == null) {
            throw new ArgumentNullException(nameof(email));
        }
        if (eventCount < 0) {
            throw new ArgumentOutOfRangeException(nameof(eventCount));
        }
        if (limits == null) {
            throw new ArgumentNullException(nameof(limits));
        }
        EventNotificationCheckpointBoundary[] checkpointSnapshot = SnapshotCheckpoints(checkpoints);

        string outboxDirectory = Path.GetFullPath(outboxPath);
        Directory.CreateDirectory(outboxDirectory);
        string completedDirectory = Path.Combine(outboxDirectory, batchId);
        using FileStream writeLock = AcquireWriteLock(
            outboxDirectory,
            limits.WriteLockTimeout,
            cancellationToken);
        if (Directory.Exists(completedDirectory)) {
            return completedDirectory;
        }

        DateTime persistedUtc = DateTime.UtcNow;
        var manifest = new EventNotificationBatchManifest {
            BatchId = batchId,
            EventCount = eventCount,
            Title = report.Title,
            PersistedUtc = persistedUtc,
            RequiresExternalTransport = requiresExternalTransport,
            Checkpoints = checkpointSnapshot
        };
        string reportHtml = EventReportHtmlRenderer.Render(report);
        string manifestJson = JsonSerializer.Serialize(manifest);
        long batchBytes = GetUtf8Bytes(reportHtml) +
                          GetUtf8Bytes(email.Html) +
                          GetUtf8Bytes(email.PlainText) +
                          GetUtf8Bytes(manifestJson);
        if (batchBytes > limits.MaximumBatchBytes) {
            throw new InvalidOperationException(
                $"Notification batch '{batchId}' requires {batchBytes} bytes, exceeding the configured " +
                $"{limits.MaximumBatchBytes}-byte batch limit.");
        }
        EventNotificationOutboxUsage usage = GetUsage(outboxDirectory);
        if (usage.PendingBatches >= limits.MaximumPendingBatches) {
            throw new InvalidOperationException(
                $"Notification outbox already contains {usage.PendingBatches} pending batches, reaching the configured limit.");
        }
        if (usage.TotalBytes > limits.MaximumOutboxBytes - batchBytes) {
            throw new InvalidOperationException(
                $"Notification outbox requires {usage.TotalBytes + batchBytes} bytes after publication, exceeding the configured " +
                $"{limits.MaximumOutboxBytes}-byte limit.");
        }

        string pendingDirectory = completedDirectory + ".pending-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(pendingDirectory);
        try {
            cancellationToken.ThrowIfCancellationRequested();
            File.WriteAllText(
                Path.Combine(pendingDirectory, "report.html"),
                reportHtml,
                new UTF8Encoding(false));
            cancellationToken.ThrowIfCancellationRequested();
            File.WriteAllText(
                Path.Combine(pendingDirectory, "email.html"),
                email.Html,
                new UTF8Encoding(false));
            File.WriteAllText(
                Path.Combine(pendingDirectory, "email.txt"),
                email.PlainText,
                new UTF8Encoding(false));
            File.WriteAllText(
                Path.Combine(pendingDirectory, "batch.json"),
                manifestJson,
                new UTF8Encoding(false));
            cancellationToken.ThrowIfCancellationRequested();

            try {
                Directory.Move(pendingDirectory, completedDirectory);
            } catch (IOException) when (Directory.Exists(completedDirectory)) {
                // Another retry published the same immutable batch first.
            }
            return completedDirectory;
        } finally {
            if (Directory.Exists(pendingDirectory)) {
                Directory.Delete(pendingDirectory, recursive: true);
            }
        }
    }

    /// <summary>Loads complete, not-yet-delivered batches in persistence order.</summary>
    public static IReadOnlyList<EventNotificationOutboxBatch> GetPending(string outboxPath) {
        if (string.IsNullOrWhiteSpace(outboxPath)) {
            throw new ArgumentException("Outbox path cannot be empty.", nameof(outboxPath));
        }
        string root = Path.GetFullPath(outboxPath);
        if (!Directory.Exists(root)) {
            return Array.Empty<EventNotificationOutboxBatch>();
        }
        var result = new List<EventNotificationOutboxBatch>();
        foreach (string directory in Directory.GetDirectories(root)) {
            string name = Path.GetFileName(directory);
            if (name.Contains(".pending-", StringComparison.Ordinal) ||
                string.Equals(name, "dead-letter", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }
            string manifestPath = Path.Combine(directory, "batch.json");
            string htmlPath = Path.Combine(directory, "email.html");
            string textPath = Path.Combine(directory, "email.txt");
            if (!File.Exists(manifestPath) || !File.Exists(htmlPath) || !File.Exists(textPath)) {
                throw new InvalidDataException($"Outbox batch '{directory}' is incomplete.");
            }
            EventNotificationBatchManifest? manifest = JsonSerializer.Deserialize<EventNotificationBatchManifest>(
                File.ReadAllText(manifestPath));
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.BatchId) ||
                !string.Equals(manifest.BatchId, name, StringComparison.Ordinal)) {
                throw new InvalidDataException($"Outbox batch '{directory}' has invalid identity metadata.");
            }
            if (manifest.SchemaVersion < 0 ||
                manifest.SchemaVersion > EventNotificationBatchManifest.CurrentSchemaVersion) {
                throw new InvalidDataException(
                    $"Outbox batch '{directory}' uses unsupported manifest schema {manifest.SchemaVersion}. " +
                    $"This build supports schema {EventNotificationBatchManifest.CurrentSchemaVersion}; " +
                    "upgrade the reader or restore a compatible binary before delivery or checkpoint advancement.");
            }
            if (manifest.EventCount < 0 || manifest.PersistedUtc == default) {
                throw new InvalidDataException($"Outbox batch '{directory}' has invalid persistence metadata.");
            }
            manifest.Checkpoints = SnapshotCheckpoints(
                manifest.Checkpoints ?? Array.Empty<EventNotificationCheckpointBoundary>());
            EventNotificationDeliveryState delivery = ReadDeliveryState(directory);
            if (delivery.DeliveredUtc.HasValue) {
                continue;
            }
            result.Add(new EventNotificationOutboxBatch(
                directory,
                manifest,
                delivery,
                File.ReadAllText(htmlPath),
                File.ReadAllText(textPath)));
        }
        return result.OrderBy(static batch => batch.Manifest.PersistedUtc)
            .ThenBy(static batch => batch.Manifest.BatchId, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>Returns pending-batch count, retry count, and oldest age for health reporting.</summary>
    public static EventNotificationOutboxHealth GetHealth(string outboxPath) {
        IReadOnlyList<EventNotificationOutboxBatch> pending = GetPending(outboxPath);
        EventNotificationOutboxUsage usage = GetUsage(Path.GetFullPath(outboxPath));
        return new EventNotificationOutboxHealth(
            pending.Count,
            pending.Sum(static batch => batch.Delivery.FailedAttempts),
            pending.Count == 0 ? null : pending.Min(static batch => batch.Manifest.PersistedUtc),
            usage.TotalBytes,
            usage.PendingBytes,
            usage.DeliveredBytes,
            usage.DeadLetterBytes,
            usage.StagingBytes);
    }

    /// <summary>Loads pending batches whose persisted retry backoff has elapsed.</summary>
    public static IReadOnlyList<EventNotificationOutboxBatch> GetReady(
        string outboxPath,
        EventNotificationRetryPolicy? retryPolicy = null,
        DateTime? nowUtc = null) {

        retryPolicy ??= new EventNotificationRetryPolicy();
        retryPolicy.Validate();
        DateTime now = (nowUtc ?? DateTime.UtcNow).ToUniversalTime();
        return GetPending(outboxPath)
            .Where(batch => retryPolicy.IsReady(batch.Delivery, now))
            .ToArray();
    }

    /// <summary>Records one failed delivery attempt atomically without changing the batch payload.</summary>
    public static void RecordFailure(EventNotificationOutboxBatch batch, Exception exception) {
        if (batch == null) {
            throw new ArgumentNullException(nameof(batch));
        }
        if (exception == null) {
            throw new ArgumentNullException(nameof(exception));
        }
        EventNotificationDeliveryState current = ReadDeliveryState(batch.DirectoryPath);
        current.FailedAttempts++;
        current.LastAttemptUtc = DateTime.UtcNow;
        string error = exception.GetType().Name + ": " + exception.Message.Trim();
        current.LastError = error.Length <= 2048 ? error : error.Substring(0, 2048);
        WriteDeliveryState(batch.DirectoryPath, current);
    }

    /// <summary>Records an acknowledged delivery atomically.</summary>
    public static void MarkDelivered(EventNotificationOutboxBatch batch) {
        if (batch == null) {
            throw new ArgumentNullException(nameof(batch));
        }
        EventNotificationDeliveryState current = ReadDeliveryState(batch.DirectoryPath);
        current.LastAttemptUtc = DateTime.UtcNow;
        current.LastError = null;
        current.TransportAcknowledgedUtc ??= current.LastAttemptUtc;
        current.DeliveredUtc = current.LastAttemptUtc;
        WriteDeliveryState(batch.DirectoryPath, current);
    }

    /// <summary>Persists downstream transport acknowledgement before checkpoint completion.</summary>
    public static void MarkTransportAcknowledged(EventNotificationOutboxBatch batch) {
        if (batch == null) {
            throw new ArgumentNullException(nameof(batch));
        }
        EventNotificationDeliveryState current = ReadDeliveryState(batch.DirectoryPath);
        current.LastAttemptUtc = DateTime.UtcNow;
        current.LastError = null;
        current.TransportAcknowledgedUtc = current.LastAttemptUtc;
        WriteDeliveryState(batch.DirectoryPath, current);
    }

    /// <summary>Moves a repeatedly failing batch into the outbox dead-letter directory.</summary>
    public static string MoveToDeadLetter(EventNotificationOutboxBatch batch) {
        if (batch == null) {
            throw new ArgumentNullException(nameof(batch));
        }
        string root = Path.GetDirectoryName(batch.DirectoryPath) ??
            throw new InvalidDataException("Outbox batch has no parent directory.");
        string deadLetterRoot = Path.Combine(root, "dead-letter");
        Directory.CreateDirectory(deadLetterRoot);
        string batchName = Path.GetFileName(batch.DirectoryPath);
        ValidateBatchId(batchName);
        string destination = Path.Combine(deadLetterRoot, batchName);
        if (Directory.Exists(destination)) {
            throw new IOException($"Dead-letter batch '{destination}' already exists.");
        }
        Directory.Move(batch.DirectoryPath, destination);
        return destination;
    }

    private static EventNotificationDeliveryState ReadDeliveryState(string directory) {
        string path = Path.Combine(directory, "delivery.json");
        if (!File.Exists(path)) {
            return new EventNotificationDeliveryState();
        }
        return JsonSerializer.Deserialize<EventNotificationDeliveryState>(File.ReadAllText(path)) ??
            throw new InvalidDataException($"Outbox delivery state '{path}' is empty.");
    }

    private static void WriteDeliveryState(string directory, EventNotificationDeliveryState state) {
        string path = Path.Combine(directory, "delivery.json");
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            File.WriteAllText(
                temporary,
                JsonSerializer.Serialize(state),
                new UTF8Encoding(false));
            if (File.Exists(path)) {
                File.Replace(temporary, path, destinationBackupFileName: null);
            } else {
                File.Move(temporary, path);
            }
        } finally {
            if (File.Exists(temporary)) {
                File.Delete(temporary);
            }
        }
    }

    private static void ValidateBatchId(string batchId) {
        if (string.IsNullOrWhiteSpace(batchId)) {
            throw new ArgumentException("Batch identifier cannot be empty.", nameof(batchId));
        }
        if (!string.Equals(batchId, Path.GetFileName(batchId), StringComparison.Ordinal) ||
            batchId == "." || batchId == ".." ||
            batchId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) {
            throw new ArgumentException("Batch identifier must be a file-name-safe value without path segments.", nameof(batchId));
        }
    }

    private static EventNotificationCheckpointBoundary[] SnapshotCheckpoints(
        IEnumerable<EventNotificationCheckpointBoundary> checkpoints) {

        if (checkpoints == null) {
            throw new ArgumentNullException(nameof(checkpoints));
        }
        var snapshot = new List<EventNotificationCheckpointBoundary>();
        foreach (EventNotificationCheckpointBoundary? checkpoint in checkpoints) {
            if (checkpoint == null ||
                string.IsNullOrWhiteSpace(checkpoint.Consumer) ||
                string.IsNullOrWhiteSpace(checkpoint.Computer) ||
                string.IsNullOrWhiteSpace(checkpoint.Container)) {
                throw new InvalidDataException(
                    "Notification checkpoint boundaries require consumer, computer, and container identities.");
            }
            if (checkpoint.ExpectedExists && !checkpoint.ExpectedUpdatedAtUtc.HasValue) {
                throw new InvalidDataException(
                    "An existing notification checkpoint boundary requires its expected update time.");
            }
            var candidate = new EventNotificationCheckpointBoundary {
                Consumer = checkpoint.Consumer.Trim(),
                Computer = checkpoint.Computer.Trim(),
                Container = checkpoint.Container.Trim(),
                RecordId = checkpoint.RecordId,
                BookmarkXml = checkpoint.BookmarkXml,
                ExpectedExists = checkpoint.ExpectedExists,
                ExpectedRecordId = checkpoint.ExpectedRecordId,
                ExpectedBookmarkXml = checkpoint.ExpectedBookmarkXml,
                ExpectedUpdatedAtUtc = checkpoint.ExpectedUpdatedAtUtc?.ToUniversalTime()
            };
            if (snapshot.Any(existing =>
                    string.Equals(existing.Consumer, candidate.Consumer, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(existing.Computer, candidate.Computer, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(existing.Container, candidate.Container, StringComparison.OrdinalIgnoreCase))) {
                throw new InvalidDataException(
                    $"Notification batch contains duplicate checkpoint boundary '{candidate.Consumer}' for {candidate.Computer}/{candidate.Container}.");
            }
            snapshot.Add(candidate);
        }
        return snapshot.ToArray();
    }

    private static int GetUtf8Bytes(string value) => Encoding.UTF8.GetByteCount(value ?? string.Empty);
}
