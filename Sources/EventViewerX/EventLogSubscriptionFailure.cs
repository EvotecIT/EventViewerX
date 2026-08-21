namespace EventViewerX;

/// <summary>Describes an asynchronous native subscription failure.</summary>
public sealed class EventLogSubscriptionFailure {
    internal EventLogSubscriptionFailure(
        string logName,
        string? machineName,
        Exception exception,
        bool terminal) {

        LogName = logName;
        MachineName = machineName;
        Exception = exception;
        Terminal = terminal;
    }

    /// <summary>Subscribed channel.</summary>
    public string LogName { get; }

    /// <summary>Remote computer, or null for the local computer.</summary>
    public string? MachineName { get; }

    /// <summary>Failure raised by native delivery, projection, or the consumer callback.</summary>
    public Exception Exception { get; }

    /// <summary>Whether delivery was stopped to preserve correctness.</summary>
    public bool Terminal { get; }
}
