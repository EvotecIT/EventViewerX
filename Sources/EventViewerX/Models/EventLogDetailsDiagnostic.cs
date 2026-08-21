namespace EventViewerX;

/// <summary>Stage that produced a partial event-log details diagnostic.</summary>
public enum EventLogDetailsReadStage {
    /// <summary>An EventLogConfiguration property could not be projected.</summary>
    Configuration,
    /// <summary>An EventLogInformation property could not be projected.</summary>
    RuntimeInformation
}

/// <summary>Describes one property that could not be projected into an otherwise usable event-log details snapshot.</summary>
public sealed class EventLogDetailsDiagnostic {
    /// <summary>Read stage that produced the diagnostic.</summary>
    public EventLogDetailsReadStage Stage { get; set; }

    /// <summary>Name of the native property that could not be read.</summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>Typed status associated with the failure.</summary>
    public EventLogDetailsStatus Status { get; set; }

    /// <summary>Underlying exception type.</summary>
    public string ErrorType { get; set; } = string.Empty;

    /// <summary>Human-readable failure message.</summary>
    public string Message { get; set; } = string.Empty;
}
