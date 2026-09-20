namespace EventViewerX.Sigma;

/// <summary>Maps one Sigma category to one explicitly chosen Windows telemetry contract.</summary>
public sealed class SigmaLogSourceMapping {
    /// <summary>Creates a validated category mapping.</summary>
    public SigmaLogSourceMapping(
        string category,
        IEnumerable<string> channels,
        IEnumerable<string> providers,
        IEnumerable<int> eventIds) {

        if (string.IsNullOrWhiteSpace(category)) {
            throw new ArgumentException("Sigma category cannot be empty.", nameof(category));
        }
        Category = category.Trim();
        Channels = Array.AsReadOnly(Normalize(channels, nameof(channels)));
        Providers = Array.AsReadOnly(Normalize(providers, nameof(providers)));
        EventIds = Array.AsReadOnly((eventIds ?? throw new ArgumentNullException(nameof(eventIds)))
            .Distinct()
            .OrderBy(static value => value)
            .ToArray());
        if (Channels.Count == 0 || EventIds.Count == 0 || EventIds.Any(static value => value < 0)) {
            throw new ArgumentException(
                "A Sigma log-source mapping requires at least one channel and one non-negative event ID.");
        }
    }

    /// <summary>Gets the Sigma logsource category.</summary>
    public string Category { get; }

    /// <summary>Gets the exact Windows event channels selected by the profile.</summary>
    public IReadOnlyList<string> Channels { get; }

    /// <summary>Gets the exact Windows event providers selected by the profile.</summary>
    public IReadOnlyList<string> Providers { get; }

    /// <summary>Gets the event IDs selected by the profile.</summary>
    public IReadOnlyList<int> EventIds { get; }

    private static string[] Normalize(
        IEnumerable<string> values,
        string parameterName) => (values ?? throw new ArgumentNullException(parameterName))
        .Where(static value => !string.IsNullOrWhiteSpace(value))
        .Select(static value => value.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
