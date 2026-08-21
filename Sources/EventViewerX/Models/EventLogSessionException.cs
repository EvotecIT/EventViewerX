namespace EventViewerX;

/// <summary>
/// Exception thrown when an Event Log session cannot be opened for a target host.
/// </summary>
public sealed class EventLogSessionException : InvalidOperationException {
    /// <summary>Creates a session failure exception from the typed open result.</summary>
    public EventLogSessionException(EventLogSessionOpenResult result, string message)
        : base(message) {
        Status = result.Status;
        TargetHost = result.TargetHost;
    }

    /// <summary>Session-open status that caused the failure.</summary>
    public EventLogSessionOpenStatus Status { get; }

    /// <summary>Host targeted by the session request.</summary>
    public string TargetHost { get; }
}
