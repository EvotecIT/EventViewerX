namespace EventViewerX;

/// <summary>
/// Describes the DACL in an event-log channel security descriptor without claiming effective access.
/// </summary>
public enum EventLogDaclState {
    /// <summary>The descriptor was unavailable or could not be parsed.</summary>
    Unavailable,
    /// <summary>The descriptor does not contain a DACL.</summary>
    NotPresent,
    /// <summary>The descriptor marks a DACL as present but its value is null.</summary>
    Null,
    /// <summary>The DACL exists but contains no access-control entries.</summary>
    Empty,
    /// <summary>The DACL contains one or more access-control entries.</summary>
    Entries
}
