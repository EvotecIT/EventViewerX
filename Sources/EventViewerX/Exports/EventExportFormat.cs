namespace EventViewerX;

/// <summary>Streaming output format used by <see cref="EventLogExporter"/>.</summary>
public enum EventExportFormat {
    /// <summary>Stable, quoted CSV schema suitable for spreadsheets and bulk ingestion.</summary>
    Csv,

    /// <summary>One complete JSON object per UTF-8 line.</summary>
    JsonLines,

    /// <summary>Raw event XML fragments wrapped in a single <c>Events</c> document.</summary>
    Xml,

    /// <summary>Native Windows EVTX archive suitable for Event Viewer and indexed re-query.</summary>
    Evtx
}
