namespace EventViewerX.Reporting;

/// <summary>Identifies how a report section should present its rows.</summary>
public enum EventReportSectionKind {
    /// <summary>Raw Windows Event Log rows with technical event metadata.</summary>
    Generic,
    /// <summary>Rows projected by a built-in typed event definition.</summary>
    Typed,
    /// <summary>Rows projected by a reusable custom JSON definition.</summary>
    Custom
}
