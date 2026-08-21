namespace EventViewerX.Storage;

/// <summary>Calendar bucket used for local historical summaries.</summary>
public enum EventStoreSummaryPeriod {
    /// <summary>One UTC hour.</summary>
    Hour,
    /// <summary>One UTC calendar day.</summary>
    Day,
    /// <summary>One UTC week beginning Monday.</summary>
    Week,
    /// <summary>One UTC calendar month.</summary>
    Month
}

/// <summary>One grouped event-history summary row.</summary>
public sealed class EventStoreSummaryRow {
    /// <summary>Inclusive UTC bucket start.</summary>
    public DateTime PeriodStartUtc { get; set; }
    /// <summary>Stable built-in or custom definition name.</summary>
    public string DefinitionName { get; set; } = string.Empty;
    /// <summary>Number of stored events in the bucket.</summary>
    public long Count { get; set; }
    /// <summary>First matching event timestamp.</summary>
    public DateTime FirstEventUtc { get; set; }
    /// <summary>Last matching event timestamp.</summary>
    public DateTime LastEventUtc { get; set; }
}

/// <summary>Summary rows with candidate-scan completeness information.</summary>
public sealed class EventStoreSummaryResult {
    internal EventStoreSummaryResult(
        IReadOnlyList<EventStoreSummaryRow> rows,
        long eventsScanned,
        bool scanLimitReached) {

        Rows = rows;
        EventsScanned = eventsScanned;
        ScanLimitReached = scanLimitReached;
    }

    /// <summary>Calendar-bucket summary rows.</summary>
    public IReadOnlyList<EventStoreSummaryRow> Rows { get; }
    /// <summary>Stored candidate events represented or evaluated.</summary>
    public long EventsScanned { get; }
    /// <summary>Whether a managed-predicate candidate cap prevented an exhaustive summary.</summary>
    public bool ScanLimitReached { get; }
}
