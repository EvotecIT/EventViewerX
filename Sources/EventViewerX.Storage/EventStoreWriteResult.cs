namespace EventViewerX.Storage;

/// <summary>Outcome of one transactional event-store write.</summary>
public sealed class EventStoreWriteResult {
    internal EventStoreWriteResult(int attempted, int inserted, bool checkpointCommitted,
        EventStoreCheckpoint? checkpoint = null) {
        Attempted = attempted;
        Inserted = inserted;
        CheckpointCommitted = checkpointCommitted;
        Checkpoint = checkpoint;
    }

    /// <summary>Rows submitted to the store.</summary>
    public int Attempted { get; }
    /// <summary>New rows inserted after idempotent deduplication.</summary>
    public int Inserted { get; }
    /// <summary>Whether the supplied checkpoint committed in the same transaction.</summary>
    public bool CheckpointCommitted { get; }
    /// <summary>Detached exact committed checkpoint, including its durable update time.</summary>
    public EventStoreCheckpoint? Checkpoint { get; }
    /// <summary>Rows already present in the store.</summary>
    public int Duplicates => Attempted - Inserted;
}
