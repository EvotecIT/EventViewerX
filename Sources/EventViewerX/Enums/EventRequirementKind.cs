namespace EventViewerX;

/// <summary>Kind of prerequisite associated with a typed event definition.</summary>
public enum EventRequirementKind {
    /// <summary>A Windows Event Log channel must exist and be readable.</summary>
    EventChannel,
    /// <summary>An advanced audit policy subcategory must produce the requested outcomes.</summary>
    AuditPolicy,
    /// <summary>The event source must have a specific Windows computer role.</summary>
    TargetRole,
    /// <summary>A provider-specific configuration setting must be enabled.</summary>
    Configuration,
    /// <summary>An operational warning about expected event volume or retention.</summary>
    Volume
}
