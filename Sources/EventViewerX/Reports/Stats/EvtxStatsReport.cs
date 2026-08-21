using System;
using System.Collections.Generic;

namespace EventViewerX.Reports.Stats;

/// <summary>
/// Snapshot report of basic EVTX statistics.
/// </summary>
internal sealed class EvtxStatsReport {
    /// <summary>
    /// Number of scanned events passed into the builder.
    /// </summary>
    public int Scanned { get; set; }

    /// <summary>
    /// Number of scanned events whose source record did not expose a level.
    /// </summary>
    public long EventsWithoutLevel { get; set; }

    /// <summary>True when at least one additional matching event existed beyond the requested cap.</summary>
    public bool Truncated { get; set; }

    /// <summary>
    /// Minimum event time (UTC) among scanned events.
    /// </summary>
    public DateTime? MinUtc { get; set; }

    /// <summary>
    /// Maximum event time (UTC) among scanned events.
    /// </summary>
    public DateTime? MaxUtc { get; set; }

    /// <summary>
    /// Counts grouped by Event ID.
    /// </summary>
    public IReadOnlyDictionary<int, long> ByEventId { get; set; } = new Dictionary<int, long>();

    /// <summary>
    /// Counts grouped by provider name.
    /// </summary>
    public IReadOnlyDictionary<string, long> ByProviderName { get; set; } = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Counts grouped by computer/machine name.
    /// </summary>
    public IReadOnlyDictionary<string, long> ByComputerName { get; set; } = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Counts grouped by event level.
    /// </summary>
    public IReadOnlyDictionary<int, EvtxLevelStats> ByLevel { get; set; } = new Dictionary<int, EvtxLevelStats>();
}
