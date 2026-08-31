namespace EventViewerX.Reporting;

/// <summary>Point-in-time health for pending durable notification batches.</summary>
public sealed class EventNotificationOutboxHealth {
    internal EventNotificationOutboxHealth(
        int pendingBatches,
        int failedAttempts,
        DateTime? oldestPendingUtc,
        long totalBytes,
        long pendingBytes,
        long deliveredBytes,
        long deadLetterBytes,
        long stagingBytes) {

        PendingBatches = pendingBatches;
        FailedAttempts = failedAttempts;
        OldestPendingUtc = oldestPendingUtc;
        OldestPendingAge = oldestPendingUtc.HasValue
            ? DateTime.UtcNow - oldestPendingUtc.Value
            : TimeSpan.Zero;
        TotalBytes = totalBytes;
        PendingBytes = pendingBytes;
        DeliveredBytes = deliveredBytes;
        DeadLetterBytes = deadLetterBytes;
        StagingBytes = stagingBytes;
    }

    /// <summary>Complete batches awaiting acknowledged transport delivery.</summary>
    public int PendingBatches { get; }
    /// <summary>Total persisted failed delivery attempts across pending batches.</summary>
    public int FailedAttempts { get; }
    /// <summary>UTC persistence time of the oldest pending batch.</summary>
    public DateTime? OldestPendingUtc { get; }
    /// <summary>Age of the oldest pending batch when this snapshot was captured.</summary>
    public TimeSpan OldestPendingAge { get; }
    /// <summary>Total file bytes retained under the outbox root.</summary>
    public long TotalBytes { get; }
    /// <summary>File bytes retained by batches awaiting delivery.</summary>
    public long PendingBytes { get; }
    /// <summary>File bytes retained by acknowledged batches.</summary>
    public long DeliveredBytes { get; }
    /// <summary>File bytes retained in the dead-letter area.</summary>
    public long DeadLetterBytes { get; }
    /// <summary>File bytes retained by incomplete staging directories.</summary>
    public long StagingBytes { get; }
}
