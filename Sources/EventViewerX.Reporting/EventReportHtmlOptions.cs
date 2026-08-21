using HtmlForgeX;

namespace EventViewerX.Reporting;

/// <summary>Controls interactive HTML presentation without changing the normalized report snapshot.</summary>
public sealed class EventReportHtmlOptions {
    /// <summary>Gets or sets where selected-record details should be presented.</summary>
    public MonitoringRecordDrawerPlacement RecordDrawerPlacement { get; set; } = MonitoringRecordDrawerPlacement.Auto;
}
