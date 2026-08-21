namespace EventViewerX.Reports.Evtx;

/// <summary>
/// Describes how an EVTX streaming query completed.
/// </summary>
internal sealed class EvtxQueryExecutionInfo {
    /// <summary>Number of events delivered to the callback.</summary>
    public int EventsDelivered { get; set; }

    /// <summary>True when at least one additional matching event existed beyond the requested cap.</summary>
    public bool Truncated { get; set; }

    /// <summary>True when the callback requested an early stop.</summary>
    public bool StoppedByHandler { get; set; }
}
