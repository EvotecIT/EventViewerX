namespace EventViewerX.Storage;

/// <summary>Successful completion of a bounded streaming history write.</summary>
public sealed class EventStoreIngestionResult {
    internal EventStoreIngestionResult(long attempted, long inserted, long committedBatches,
        EventStoreCheckpoint? checkpoint) {
        Attempted = attempted;
        Inserted = inserted;
        CommittedBatches = committedBatches;
        Checkpoint = checkpoint;
    }
    /// <summary>Total source rows committed, including duplicates.</summary>
    public long Attempted { get; }
    /// <summary>New history rows inserted.</summary>
    public long Inserted { get; }
    /// <summary>Idempotent duplicate rows.</summary>
    public long Duplicates => Attempted - Inserted;
    /// <summary>Number of successfully committed transactions.</summary>
    public long CommittedBatches { get; }
    /// <summary>Detached final checkpoint when a checkpoint factory was supplied.</summary>
    public EventStoreCheckpoint? Checkpoint { get; }
}