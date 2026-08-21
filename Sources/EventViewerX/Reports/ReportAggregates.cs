using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace EventViewerX.Reports;

/// <summary>
/// Small aggregation helpers for report builders.
/// </summary>
internal static class ReportAggregates {
    /// <summary>
    /// Increments a count for <paramref name="key"/> in <paramref name="dict"/>.
    /// </summary>
    /// <param name="dict">Target dictionary.</param>
    /// <param name="key">Key to increment.</param>
    /// <param name="useUnknownPlaceholder">When true, empty keys are counted as <c>(unknown)</c>.</param>
    public static void AddCount(Dictionary<string, long> dict, string? key, bool useUnknownPlaceholder = false) {
        if (dict is null) throw new ArgumentNullException(nameof(dict));

        string? k;
        if (string.IsNullOrWhiteSpace(key)) {
            if (!useUnknownPlaceholder) return;
            k = "(unknown)";
        } else {
            k = key!.Trim();
        }

        dict.TryGetValue(k, out var cur);
        dict[k] = cur + 1;
    }

    /// <summary>
    /// Increments a count for <paramref name="key"/> in <paramref name="dict"/>.
    /// </summary>
    /// <param name="dict">Target dictionary.</param>
    /// <param name="key">Key to increment.</param>
    public static void AddCount(Dictionary<int, long> dict, int key) {
        if (dict is null) throw new ArgumentNullException(nameof(dict));

        dict.TryGetValue(key, out var cur);
        dict[key] = cur + 1;
    }

    /// <summary>
    /// Returns the top <paramref name="top"/> pairs ordered by count desc then key asc (case-insensitive for strings).
    /// </summary>
    /// <param name="dict">Source counts.</param>
    /// <param name="top">Maximum items to return.</param>
    public static IReadOnlyList<KeyValuePair<string, long>> TopStringPairs(Dictionary<string, long> dict, int top) {
        return TopStringPairs((IReadOnlyDictionary<string, long>)dict, top);
    }

    /// <summary>
    /// Returns the top <paramref name="top"/> pairs ordered by count desc then key asc (case-insensitive for strings).
    /// </summary>
    /// <param name="dict">Source counts.</param>
    /// <param name="top">Maximum items to return.</param>
    public static IReadOnlyList<KeyValuePair<string, long>> TopStringPairs(IReadOnlyDictionary<string, long> dict, int top) {
        if (dict is null) throw new ArgumentNullException(nameof(dict));
        if (top <= 0) return Array.Empty<KeyValuePair<string, long>>();

        return dict
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Take(top)
            .ToList();
    }

    /// <summary>
    /// Returns the top <paramref name="top"/> pairs ordered by count desc then key asc.
    /// </summary>
    /// <param name="dict">Source counts.</param>
    /// <param name="top">Maximum items to return.</param>
    public static IReadOnlyList<KeyValuePair<int, long>> TopIntPairs(Dictionary<int, long> dict, int top) {
        return TopIntPairs((IReadOnlyDictionary<int, long>)dict, top);
    }

    /// <summary>
    /// Returns the top <paramref name="top"/> pairs ordered by count desc then key asc.
    /// </summary>
    /// <param name="dict">Source counts.</param>
    /// <param name="top">Maximum items to return.</param>
    public static IReadOnlyList<KeyValuePair<int, long>> TopIntPairs(IReadOnlyDictionary<int, long> dict, int top) {
        if (dict is null) throw new ArgumentNullException(nameof(dict));
        if (top <= 0) return Array.Empty<KeyValuePair<int, long>>();

        return dict
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key)
            .Take(top)
            .ToList();
    }

    /// <summary>
    /// Returns top rows shaped as key/count objects for string-key aggregates.
    /// </summary>
    /// <param name="dict">Source counts.</param>
    /// <param name="top">Maximum rows to return.</param>
    /// <param name="keyName">Field name used under <see cref="ReportTopRow.Key"/>.</param>
    public static IReadOnlyList<ReportTopRow> TopStringRows(IReadOnlyDictionary<string, long> dict, int top, string keyName) {
        if (dict is null) throw new ArgumentNullException(nameof(dict));
        if (string.IsNullOrWhiteSpace(keyName)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(keyName));

        var list = new List<ReportTopRow>();
        foreach (var kvp in TopStringPairs(dict, top)) {
            list.Add(new ReportTopRow {
                Key = new Dictionary<string, object?>(StringComparer.Ordinal) { { keyName, kvp.Key } },
                Count = kvp.Value
            });
        }

        return list;
    }

    /// <summary>
    /// Returns top rows shaped as key/count objects for integer-key aggregates.
    /// </summary>
    /// <param name="dict">Source counts.</param>
    /// <param name="top">Maximum rows to return.</param>
    /// <param name="keyName">Field name used under <see cref="ReportTopRow.Key"/>.</param>
    public static IReadOnlyList<ReportTopRow> TopIntRows(IReadOnlyDictionary<int, long> dict, int top, string keyName) {
        if (dict is null) throw new ArgumentNullException(nameof(dict));
        if (string.IsNullOrWhiteSpace(keyName)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(keyName));

        var list = new List<ReportTopRow>();
        foreach (var kvp in TopIntPairs(dict, top)) {
            list.Add(new ReportTopRow {
                Key = new Dictionary<string, object?>(StringComparer.Ordinal) { { keyName, kvp.Key } },
                Count = kvp.Value
            });
        }

        return list;
    }

    /// <summary>
    /// Returns top rows shaped for preview tables as <c>[key, count]</c> cells for string-key aggregates.
    /// </summary>
    /// <param name="dict">Source counts.</param>
    /// <param name="top">Maximum rows to return.</param>
    public static IReadOnlyList<IReadOnlyList<string>> TopStringPreviewRows(IReadOnlyDictionary<string, long> dict, int top) {
        if (dict is null) throw new ArgumentNullException(nameof(dict));

        var rows = new List<IReadOnlyList<string>>();
        foreach (var kvp in TopStringPairs(dict, top)) {
            rows.Add(new[] { kvp.Key, kvp.Value.ToString(CultureInfo.InvariantCulture) });
        }

        return rows;
    }

    /// <summary>
    /// Returns top rows shaped for preview tables as <c>[key, count]</c> cells for integer-key aggregates.
    /// </summary>
    /// <param name="dict">Source counts.</param>
    /// <param name="top">Maximum rows to return.</param>
    public static IReadOnlyList<IReadOnlyList<string>> TopIntPreviewRows(IReadOnlyDictionary<int, long> dict, int top) {
        if (dict is null) throw new ArgumentNullException(nameof(dict));

        var rows = new List<IReadOnlyList<string>>();
        foreach (var kvp in TopIntPairs(dict, top)) {
            rows.Add(new[] {
                kvp.Key.ToString(CultureInfo.InvariantCulture),
                kvp.Value.ToString(CultureInfo.InvariantCulture)
            });
        }

        return rows;
    }
}
