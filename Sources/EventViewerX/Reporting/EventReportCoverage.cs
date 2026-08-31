namespace EventViewerX.Reporting;

/// <summary>Describes one queried or failed report source.</summary>
public sealed class EventReportCoverage {
    /// <summary>Target machine.</summary>
    public string MachineName { get; set; } = string.Empty;
    /// <summary>Source channel.</summary>
    public string LogName { get; set; } = string.Empty;
    /// <summary>Whether the source completed successfully.</summary>
    public bool Succeeded { get; set; }
    /// <summary>Failure classification or empty on success.</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>Failure detail or empty on success.</summary>
    public string Detail { get; set; } = string.Empty;
}
