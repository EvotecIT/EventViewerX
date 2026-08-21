namespace EventViewerX;

/// <summary>Native Windows Event Log source used by a built-in or custom event definition.</summary>
public sealed class EventSourceDefinition {
    internal EventSourceDefinition(
        string logName,
        IEnumerable<int> eventIds) {

        LogName = logName;
        EventIds = eventIds
            .Distinct()
            .OrderBy(static value => value)
            .ToArray();
    }

    /// <summary>Original Windows event channel.</summary>
    public string LogName { get; }

    /// <summary>Event identifiers selected from the channel.</summary>
    public IReadOnlyList<int> EventIds { get; }
}
