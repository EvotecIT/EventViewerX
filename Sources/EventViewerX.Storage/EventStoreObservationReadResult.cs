namespace EventViewerX.Storage;

/// <summary>Bounded stored-observation read with explicit completeness evidence.</summary>
public sealed class EventStoreObservationReadResult {
    internal EventStoreObservationReadResult(
        IReadOnlyList<EventObservation> observations,
        long eventsScanned,
        bool scanLimitReached,
        string? completenessDiagnostic) {

        Observations = Array.AsReadOnly(observations.ToArray());
        EventsScanned = eventsScanned;
        ScanLimitReached = scanLimitReached;
        CompletenessDiagnostic = completenessDiagnostic;
    }

    /// <summary>Restored observations in requested deterministic order.</summary>
    public IReadOnlyList<EventObservation> Observations { get; }
    /// <summary>Stored candidate rows inspected.</summary>
    public long EventsScanned { get; }
    /// <summary>Whether a candidate or result bound prevented an exhaustive read.</summary>
    public bool ScanLimitReached { get; }
    /// <summary>Reason the historical read is incomplete.</summary>
    public string? CompletenessDiagnostic { get; }
    /// <summary>Whether the requested stored window was read exhaustively.</summary>
    public bool IsComplete => !ScanLimitReached && string.IsNullOrWhiteSpace(CompletenessDiagnostic);
}
