using System;

namespace EventViewerX.Reports.Stats;

/// <summary>
/// Aggregated event counts for a single event level.
/// </summary>
public sealed class EvtxLevelStats {
    /// <summary>
    /// Creates a new level stats row.
    /// </summary>
    public EvtxLevelStats(int level, string levelDisplayName) {
        Level = level;
        LevelDisplayName = levelDisplayName ?? string.Empty;
    }

    /// <summary>
    /// Numeric event level (0 when unknown).
    /// </summary>
    public int Level { get; }

    /// <summary>
    /// Display-friendly level name resolved from provider metadata when available.
    /// </summary>
    public string LevelDisplayName { get; }

    /// <summary>
    /// Count of events at this level.
    /// </summary>
    public long Count { get; set; }
}
