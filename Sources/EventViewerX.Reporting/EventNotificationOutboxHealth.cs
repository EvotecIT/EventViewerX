namespace EventViewerX.Reporting;

/// <summary>Point-in-time health for pending durable notification batches.</summary>
public sealed class EventNotificationOutboxHealth {
    internal EventNotificationOutboxHealth(
        int pendingBatches,
        int failedAttempts,
        DateTime? oldestPendingUtc) {

        PendingBatches = pendingBatches;
        FailedAttempts = failedAttempts;
        OldestPendingUtc = oldestPendingUtc;
        OldestPendingAge = oldestPendingUtc.HasValue
            ? DateTime.UtcNow - oldestPendingUtc.Value
            : TimeSpan.Zero;
    }

    /// <summary>Complete batches awaiting acknowledged transport delivery.</summary>
    public int PendingBatches { get; }
    /// <summary>Total persisted failed delivery attempts across pending batches.</summary>
    public int FailedAttempts { get; }
    /// <summary>UTC persistence time of the oldest pending batch.</summary>
    public DateTime? OldestPendingUtc { get; }
    /// <summary>Age of the oldest pending batch when this snapshot was captured.</summary>
    public TimeSpan OldestPendingAge { get; }
}
