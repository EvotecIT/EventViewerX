using System;
using System.Collections.Generic;

namespace EventViewerX.Reports.Stats;

/// <summary>
/// Shared aggregation helpers for EVTX statistics reports.
/// </summary>
internal static class EvtxStatsAggregates {
    /// <summary>
    /// Adds/increments a computer-name count when <paramref name="computerName"/> is not empty.
    /// </summary>
    /// <param name="dict">Target dictionary.</param>
    /// <param name="computerName">Computer name value.</param>
    public static void AddComputerCount(Dictionary<string, long> dict, string? computerName) {
        if (dict is null) throw new ArgumentNullException(nameof(dict));
        if (string.IsNullOrWhiteSpace(computerName)) {
            return;
        }

        var key = computerName!.Trim();
        dict.TryGetValue(key, out var cur);
        dict[key] = cur + 1;
    }

    /// <summary>
    /// Adds/increments a level count and preserves display name on first observation.
    /// </summary>
    /// <param name="dict">Target dictionary.</param>
    /// <param name="level">Numeric event level.</param>
    /// <param name="displayName">Optional level display name.</param>
    public static void AddLevelCount(Dictionary<int, EvtxLevelStats> dict, int level, string? displayName) {
        if (dict is null) throw new ArgumentNullException(nameof(dict));

        dict.TryGetValue(level, out var cur);
        cur ??= new EvtxLevelStats(level, displayName ?? string.Empty);
        cur.Count++;
        dict[level] = cur;
    }
}
