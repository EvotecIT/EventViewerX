namespace EventViewerX;

/// <summary>Operational layer assessed by a readiness check.</summary>
public enum EventReadinessLayer {
    /// <summary>Runtime and platform support.</summary>
    Runtime,
    /// <summary>Local or Active Directory target discovery.</summary>
    TargetDiscovery,
    /// <summary>Event Log channel and native query transport.</summary>
    EventLogTransport,
    /// <summary>Windows Event Collector host, subscription, and expected-source coverage.</summary>
    WindowsEventCollector,
    /// <summary>Effective or observed audit-policy evidence.</summary>
    AuditPolicy,
    /// <summary>Provider-specific prerequisite configuration.</summary>
    Configuration
}
