namespace EventViewerX;

/// <summary>
/// Identifies one channel or offline file referenced by a structured
/// Windows Event Log QueryList.
/// </summary>
public sealed class EventLogStructuredQuerySource {
    internal EventLogStructuredQuerySource(
        EventLogQuerySourceKind kind,
        string source) {

        Kind = kind;
        Source = source;
    }

    /// <summary>Whether the source is a live channel or an offline event-log file.</summary>
    public EventLogQuerySourceKind Kind { get; }

    /// <summary>
    /// Channel name for live sources, or a normalized full path for offline files.
    /// </summary>
    public string Source { get; }
}
