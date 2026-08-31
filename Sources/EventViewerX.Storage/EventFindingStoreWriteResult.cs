namespace EventViewerX.Storage;

/// <summary>Outcome of one transactional detection-finding write.</summary>
public sealed class EventFindingStoreWriteResult {
    internal EventFindingStoreWriteResult(int attempted, int inserted) {
        Attempted = attempted;
        Inserted = inserted;
    }

    /// <summary>Findings submitted to the store.</summary>
    public int Attempted { get; }
    /// <summary>New findings inserted after idempotent deduplication.</summary>
    public int Inserted { get; }
    /// <summary>Findings already present in the store.</summary>
    public int Duplicates => Attempted - Inserted;
}
