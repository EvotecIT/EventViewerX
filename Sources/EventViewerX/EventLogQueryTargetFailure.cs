namespace EventViewerX;

/// <summary>Describes an expected failure isolated to one remote event-log target.</summary>
public sealed class EventLogQueryTargetFailure {
    internal EventLogQueryTargetFailure(
        string machineName,
        string logName,
        EventLogRemoteQueryFailureKind kind,
        string message) {
        if (string.IsNullOrWhiteSpace(machineName)) {
            throw new ArgumentException("Remote target cannot be null or empty.", nameof(machineName));
        }
        if (kind == EventLogRemoteQueryFailureKind.None) {
            throw new ArgumentOutOfRangeException(nameof(kind), "A target failure must have a classified failure kind.");
        }
        if (string.IsNullOrWhiteSpace(logName)) {
            throw new ArgumentException("Event-log source cannot be null or empty.", nameof(logName));
        }

        MachineName = machineName.Trim();
        LogName = logName.Trim();
        Kind = kind;
        Message = message ?? string.Empty;
    }

    /// <summary>Normalized remote machine name.</summary>
    public string MachineName { get; }

    /// <summary>Event-log source that failed on the remote machine.</summary>
    public string LogName { get; }

    /// <summary>Typed remote-target failure kind.</summary>
    public EventLogRemoteQueryFailureKind Kind { get; }

    /// <summary>Failure message returned by the event-log boundary.</summary>
    public string Message { get; }
}
