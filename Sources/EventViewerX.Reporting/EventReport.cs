namespace EventViewerX.Reporting;

/// <summary>Immutable, reusable result of one event query.</summary>
public sealed class EventReport {
    internal EventReport(string title, DateTime generatedAt, TimeSpan queryDuration,
        IReadOnlyList<EventReportRow> rows, IReadOnlyList<EventReportSection> sections,
        IReadOnlyList<EventReportCoverage> coverage, long scanned, bool scanLimitReached,
        string? completenessDiagnostic = null) {
        Title = title;
        GeneratedAt = generatedAt;
        QueryDuration = queryDuration;
        Rows = rows;
        Sections = sections;
        Coverage = coverage;
        EventsScanned = scanned;
        ScanLimitReached = scanLimitReached;
        CompletenessDiagnostic = completenessDiagnostic;
    }

    /// <summary>Report title.</summary>
    public string Title { get; }
    /// <summary>UTC generation timestamp.</summary>
    public DateTime GeneratedAt { get; }
    /// <summary>Time spent reading and projecting source events.</summary>
    public TimeSpan QueryDuration { get; }
    /// <summary>Normalized event rows.</summary>
    public IReadOnlyList<EventReportRow> Rows { get; }
    /// <summary>Homogeneous generic or typed tables ready for presentation.</summary>
    public IReadOnlyList<EventReportSection> Sections { get; }
    /// <summary>Source coverage and failures.</summary>
    public IReadOnlyList<EventReportCoverage> Coverage { get; }
    /// <summary>Raw typed candidates evaluated.</summary>
    public long EventsScanned { get; }
    /// <summary>Whether the typed candidate cap was reached.</summary>
    public bool ScanLimitReached { get; }
    /// <summary>Specific reason the report is not exhaustive, when one is available.</summary>
    public string? CompletenessDiagnostic { get; }
}
