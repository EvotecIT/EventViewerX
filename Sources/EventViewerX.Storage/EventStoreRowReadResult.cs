namespace EventViewerX.Storage;

/// <summary>Summarizes a streamed history read after the consumer has processed each row.</summary>
public sealed class EventStoreRowReadResult {
    internal EventStoreRowReadResult(long rowsRead, long eventsScanned, bool scanLimitReached, string? completenessDiagnostic) {
        RowsRead = rowsRead;
        EventsScanned = eventsScanned;
        ScanLimitReached = scanLimitReached;
        CompletenessDiagnostic = completenessDiagnostic;
    }

    /// <summary>Number of rows delivered to the consumer.</summary>
    public long RowsRead { get; }

    /// <summary>Number of candidate rows evaluated after SQL filtering.</summary>
    public long EventsScanned { get; }

    /// <summary>Whether a candidate or result limit prevented an exhaustive read.</summary>
    public bool ScanLimitReached { get; }

    /// <summary>Explanation of a truncated read, when applicable.</summary>
    public string? CompletenessDiagnostic { get; }

    /// <summary>Whether the selected history was read exhaustively.</summary>
    public bool IsComplete => !ScanLimitReached;
}
