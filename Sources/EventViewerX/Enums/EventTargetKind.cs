namespace EventViewerX;

/// <summary>Kind of machine returned by target discovery.</summary>
public enum EventTargetKind {
    /// <summary>The computer running EventViewerX.</summary>
    LocalMachine,
    /// <summary>An explicitly named event-log computer.</summary>
    EventLogMachine,
    /// <summary>A Windows Event Collector.</summary>
    Collector,
    /// <summary>An Active Directory domain controller.</summary>
    DomainController
}
