using System.Text.Json;

namespace EventViewerX.Reports.Correlation;

/// <summary>
/// Reusable correlation profile presets for named-events timeline queries.
/// </summary>
internal static class NamedEventsTimelineCorrelationProfiles {
    private sealed class CorrelationProfile {
        public CorrelationProfile(string name, IReadOnlyList<string> keys) {
            Name = name;
            Keys = keys;
        }

        public string Name { get; }
        public IReadOnlyList<string> Keys { get; }
    }

    private static readonly CorrelationProfile[] ProfilesValue = {
        new("identity", new[] { "who", "object_affected", "computer" }),
        new("actor_activity", new[] { "who", "action", "computer" }),
        new("object_activity", new[] { "object_affected", "action", "who" }),
        new("host_activity", new[] { "computer", "action", "who" }),
        new("rule_activity", new[] { "named_event", "who", "object_affected" })
    };

    private static readonly IReadOnlyDictionary<string, CorrelationProfile> ProfilesByName =
        ProfilesValue.ToDictionary(static profile => profile.Name, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Supported correlation profile names.
    /// </summary>
    public static IReadOnlyList<string> Names { get; } = ProfilesValue
        .Select(static profile => profile.Name)
        .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    /// <summary>
    /// Resolves an optional profile name into normalized profile metadata.
    /// </summary>
    /// <param name="rawProfile">Raw profile name.</param>
    /// <param name="normalizedProfile">Normalized profile name when a profile was supplied.</param>
    /// <param name="keys">Correlation keys for the resolved profile.</param>
    /// <param name="error">Validation error when resolution fails.</param>
    /// <returns><see langword="true"/> when the input is blank or resolves successfully; otherwise <see langword="false"/>.</returns>
    public static bool TryResolve(
        string? rawProfile,
        out string? normalizedProfile,
        out IReadOnlyList<string>? keys,
        out string? error) {
        normalizedProfile = null;
        keys = null;
        error = null;

        var profile = NormalizeProfileName(rawProfile);
        if (!string.IsNullOrWhiteSpace(rawProfile) && string.IsNullOrWhiteSpace(profile)) {
            error = $"correlation_profile ('{rawProfile}') is not recognized. Allowed values: {string.Join(", ", Names)}.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(profile)) {
            return true;
        }

        if (!ProfilesByName.TryGetValue(profile, out var resolved)) {
            error = $"correlation_profile ('{rawProfile}') is not recognized. Allowed values: {string.Join(", ", Names)}.";
            return false;
        }

        normalizedProfile = resolved.Name;
        keys = resolved.Keys;
        return true;
    }

    private static string NormalizeProfileName(string? rawProfile) {
        if (string.IsNullOrWhiteSpace(rawProfile)) {
            return string.Empty;
        }

        var replaced = (rawProfile ?? string.Empty).Trim()
            .Replace('-', '_')
            .Replace(' ', '_');
        var normalized = (JsonNamingPolicy.SnakeCaseLower.ConvertName(replaced) ?? string.Empty).Trim(UnderscoreTrimChars);
        while (normalized.Contains("__", StringComparison.Ordinal)) {
            normalized = normalized.Replace("__", "_");
        }

        return normalized;
    }

    private static readonly char[] UnderscoreTrimChars = { '_' };
}
