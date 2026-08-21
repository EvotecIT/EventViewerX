using System;
using System.Collections.Generic;

namespace EventViewerX.Reports.QueryHelpers;

internal static class EventProjectionHelpers {
    internal static string SafeGetUserSid(EventObject ev) {
        try {
            return ev.UserId?.Value ?? string.Empty;
        } catch {
            return string.Empty;
        }
    }

    internal static string SafeGetMessage(EventObject ev) {
        try {
            return ev.Message ?? string.Empty;
        } catch {
            return string.Empty;
        }
    }

    internal static string TruncateSafe(string value, int maxChars) {
        if (maxChars <= 0 || string.IsNullOrEmpty(value)) {
            return string.Empty;
        }

        if (value.Length <= maxChars) {
            return value;
        }

        return value.Substring(0, maxChars);
    }

    internal static IReadOnlyDictionary<string, string> NormalizeDict(IReadOnlyDictionary<string, string>? dict) {
        if (dict is null || dict.Count == 0) {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var result = new Dictionary<string, string>(dict.Count, StringComparer.Ordinal);
        foreach (var kvp in dict) {
            result[kvp.Key] = kvp.Value ?? string.Empty;
        }

        return result;
    }
}
