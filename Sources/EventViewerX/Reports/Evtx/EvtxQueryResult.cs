using System.Collections.Generic;

namespace EventViewerX.Reports.Evtx;

/// <summary>
/// Query result for EVTX reads.
/// </summary>
internal sealed class EvtxQueryResult {
    /// <summary>
    /// Gets or sets queried events.
    /// </summary>
    public IReadOnlyList<EventObject> Events { get; set; } = new List<EventObject>();

    /// <summary>
    /// Gets or sets a value indicating whether additional matching events existed beyond the requested cap.
    /// </summary>
    public bool Truncated { get; set; }
}
