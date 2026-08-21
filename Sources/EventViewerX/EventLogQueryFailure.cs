namespace EventViewerX;

/// <summary>
/// Describes a failure isolated to one source in a multi-source event query.
/// </summary>
public sealed class EventLogQueryFailure {
    internal EventLogQueryFailure(
        string source,
        string? machineName,
        Exception exception) {

        Source = source;
        MachineName = machineName;
        Exception = exception;
    }

    /// <summary>Channel name or offline event-log path that failed.</summary>
    public string Source { get; }

    /// <summary>Remote machine associated with the source, when applicable.</summary>
    public string? MachineName { get; }

    /// <summary>Exception raised while opening or reading the source.</summary>
    public Exception Exception { get; }
}
