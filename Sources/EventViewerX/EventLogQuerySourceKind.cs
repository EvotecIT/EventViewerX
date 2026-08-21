namespace EventViewerX;

/// <summary>Identifies the source kind referenced by a structured Windows Event Log query.</summary>
public enum EventLogQuerySourceKind {
    /// <summary>Detect file paths from the QueryList XML.</summary>
    Auto = 0,

    /// <summary>The QueryList selects live Windows event channels.</summary>
    Channel = 1,

    /// <summary>The QueryList selects offline event-log files.</summary>
    File = 2
}
