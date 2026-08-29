namespace EventViewerX.Reporting;

/// <summary>Hard capacity limits applied before a notification batch is published.</summary>
public sealed class EventNotificationOutboxLimits {
    /// <summary>Creates bounded outbox limits.</summary>
    /// <param name="maximumBatchBytes">Maximum UTF-8 payload bytes for one immutable batch.</param>
    /// <param name="maximumOutboxBytes">Maximum bytes retained anywhere under the outbox root.</param>
    /// <param name="maximumPendingBatches">Maximum completed batches awaiting delivery.</param>
    /// <param name="writeLockTimeout">Maximum wait for another process publishing to the same outbox.</param>
    public EventNotificationOutboxLimits(
        long maximumBatchBytes = 64L * 1024 * 1024,
        long maximumOutboxBytes = 1024L * 1024 * 1024,
        int maximumPendingBatches = 10000,
        TimeSpan? writeLockTimeout = null) {

        if (maximumBatchBytes <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maximumBatchBytes));
        }
        if (maximumOutboxBytes < maximumBatchBytes) {
            throw new ArgumentOutOfRangeException(
                nameof(maximumOutboxBytes),
                "Maximum outbox bytes cannot be smaller than the maximum batch size.");
        }
        if (maximumPendingBatches <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maximumPendingBatches));
        }
        TimeSpan lockTimeout = writeLockTimeout ?? TimeSpan.FromSeconds(30);
        if (lockTimeout <= TimeSpan.Zero || lockTimeout > TimeSpan.FromMinutes(5)) {
            throw new ArgumentOutOfRangeException(nameof(writeLockTimeout));
        }

        MaximumBatchBytes = maximumBatchBytes;
        MaximumOutboxBytes = maximumOutboxBytes;
        MaximumPendingBatches = maximumPendingBatches;
        WriteLockTimeout = lockTimeout;
    }

    /// <summary>Maximum UTF-8 payload bytes for one batch before filesystem overhead.</summary>
    public long MaximumBatchBytes { get; }

    /// <summary>Maximum retained file bytes across pending, delivered, dead-letter, and staging data.</summary>
    public long MaximumOutboxBytes { get; }

    /// <summary>Maximum number of batches awaiting acknowledged delivery.</summary>
    public int MaximumPendingBatches { get; }

    /// <summary>Maximum wait for another process publishing to the same outbox.</summary>
    public TimeSpan WriteLockTimeout { get; }
}
