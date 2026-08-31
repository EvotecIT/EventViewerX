namespace EventViewerX.Reporting;

/// <summary>Supported shared event aggregation operation.</summary>
public enum EventAggregationOperation {
    /// <summary>Number of input rows.</summary>
    Count,
    /// <summary>Number of distinct non-null or explicitly included values.</summary>
    DistinctCount,
    /// <summary>Earliest non-null date-time value.</summary>
    FirstSeen,
    /// <summary>Latest non-null date-time value.</summary>
    LastSeen,
    /// <summary>Count divided by a declared bucket or query-window unit.</summary>
    Rate
}
