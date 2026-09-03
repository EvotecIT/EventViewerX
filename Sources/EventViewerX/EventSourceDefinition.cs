namespace EventViewerX;

/// <summary>Native Windows Event Log source used by a built-in or custom event definition.</summary>
public sealed class EventSourceDefinition {
    internal EventSourceDefinition(
        string logName,
        IEnumerable<int> eventIds,
        IEnumerable<string>? providerNames = null) {

        LogName = logName;
        EventIds = eventIds
            .Distinct()
            .OrderBy(static value => value)
            .ToArray();
        ProviderNames = (providerNames ?? Array.Empty<string>())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>Original Windows event channel.</summary>
    public string LogName { get; }

    /// <summary>Event identifiers selected from the channel.</summary>
    public IReadOnlyList<int> EventIds { get; }

    /// <summary>Event providers selected from the channel. An empty list means the source is not provider-scoped.</summary>
    public IReadOnlyList<string> ProviderNames { get; }
}
