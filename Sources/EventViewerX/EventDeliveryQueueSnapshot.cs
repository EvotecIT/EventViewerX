namespace EventViewerX;

/// <summary>Point-in-time health and throughput counters for a bounded delivery queue.</summary>
public sealed class EventDeliveryQueueSnapshot {
    internal EventDeliveryQueueSnapshot(
        int capacity,
        long accepted,
        long completed,
        int depth,
        int highWatermark,
        DateTime? oldestPendingUtc,
        Exception? failure) {

        Capacity = capacity;
        Accepted = accepted;
        Completed = completed;
        Depth = depth;
        HighWatermark = highWatermark;
        OldestPendingUtc = oldestPendingUtc;
        OldestPendingAge = oldestPendingUtc.HasValue
            ? DateTime.UtcNow - oldestPendingUtc.Value
            : TimeSpan.Zero;
        Failure = failure;
    }

    /// <summary>Maximum queued item count.</summary>
    public int Capacity { get; }
    /// <summary>Items accepted by the queue.</summary>
    public long Accepted { get; }
    /// <summary>Items successfully processed.</summary>
    public long Completed { get; }
    /// <summary>Current queued or processing item count.</summary>
    public int Depth { get; }
    /// <summary>Highest observed queue depth.</summary>
    public int HighWatermark { get; }
    /// <summary>UTC acceptance time of the oldest queued or processing item.</summary>
    public DateTime? OldestPendingUtc { get; }
    /// <summary>Age of the oldest queued or processing item when this snapshot was captured.</summary>
    public TimeSpan OldestPendingAge { get; }
    /// <summary>First processing failure, when present.</summary>
    public Exception? Failure { get; }
    /// <summary>Items accepted but not yet successfully processed.</summary>
    public long Pending => Accepted - Completed;
}
