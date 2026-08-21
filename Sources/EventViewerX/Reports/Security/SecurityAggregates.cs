using System;
using System.Collections.Generic;
using System.Linq;

namespace EventViewerX.Reports.Security;

/// <summary>
/// Small aggregation helpers for security report builders.
/// </summary>
internal static class SecurityAggregates {
    internal static DateTime? NormalizeUtc(
        DateTime value) {

        return value == DateTime.MinValue
            ? null
            : value.ToUniversalTime();
    }

    /// <summary>
    /// Increments a count for <paramref name="key"/> in <paramref name="dict"/>.
    /// </summary>
    /// <param name="dict">Target dictionary.</param>
    /// <param name="key">Key to increment.</param>
    /// <param name="useUnknownPlaceholder">When true, empty keys are counted as <c>(unknown)</c>.</param>
    public static void AddCount(Dictionary<string, long> dict, string? key, bool useUnknownPlaceholder = false) {
        ReportAggregates.AddCount(dict, key, useUnknownPlaceholder);
    }

    /// <summary>
    /// Increments a count for <paramref name="key"/> in <paramref name="dict"/>.
    /// </summary>
    /// <param name="dict">Target dictionary.</param>
    /// <param name="key">Key to increment.</param>
    public static void AddCount(Dictionary<int, long> dict, int key) {
        ReportAggregates.AddCount(dict, key);
    }

    /// <summary>
    /// Returns the top <paramref name="top"/> pairs ordered by count desc then key asc (case-insensitive for strings).
    /// </summary>
    /// <param name="dict">Source counts.</param>
    /// <param name="top">Maximum items to return.</param>
    public static IReadOnlyList<KeyValuePair<string, long>> TopStringPairs(Dictionary<string, long> dict, int top) {
        return ReportAggregates.TopStringPairs(dict, top);
    }

    /// <summary>
    /// Returns the top <paramref name="top"/> pairs ordered by count desc then key asc.
    /// </summary>
    /// <param name="dict">Source counts.</param>
    /// <param name="top">Maximum items to return.</param>
    public static IReadOnlyList<KeyValuePair<int, long>> TopIntPairs(Dictionary<int, long> dict, int top) {
        return ReportAggregates.TopIntPairs(dict, top);
    }
}
