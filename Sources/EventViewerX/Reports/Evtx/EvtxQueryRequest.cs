using System;
using System.Collections.Generic;

namespace EventViewerX.Reports.Evtx;

/// <summary>
/// Query contract for reading events from an EVTX file.
/// </summary>
internal sealed class EvtxQueryRequest {
    /// <summary>
    /// Gets or sets the EVTX file path (absolute or relative).
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional event IDs to include.
    /// </summary>
    public IReadOnlyList<int>? EventIds { get; set; }

    /// <summary>
    /// Gets or sets an optional provider name filter.
    /// </summary>
    public string? ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the optional UTC lower bound.
    /// </summary>
    public DateTime? StartTimeUtc { get; set; }

    /// <summary>
    /// Gets or sets the optional UTC upper bound.
    /// </summary>
    public DateTime? EndTimeUtc { get; set; }

    /// <summary>
    /// Gets or sets the maximum events to return (0 means unlimited).
    /// </summary>
    public int MaxEvents { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether events should be read oldest-first.
    /// </summary>
    public bool OldestFirst { get; set; }

    /// <summary>
    /// Gets or sets the amount of provider data materialized for each event.
    /// </summary>
    public EventReadMode ReadMode { get; set; } = EventReadMode.Full;

    /// <summary>
    /// Creates a copy of this request with the specified event projection.
    /// </summary>
    public EvtxQueryRequest WithReadMode(EventReadMode readMode) {
        return new EvtxQueryRequest {
            FilePath = FilePath,
            EventIds = EventIds,
            ProviderName = ProviderName,
            StartTimeUtc = StartTimeUtc,
            EndTimeUtc = EndTimeUtc,
            MaxEvents = MaxEvents,
            OldestFirst = OldestFirst,
            ReadMode = readMode
        };
    }
}
