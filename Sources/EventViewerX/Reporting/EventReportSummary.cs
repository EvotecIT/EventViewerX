namespace EventViewerX.Reporting;

/// <summary>Portable query completion evidence shared by row output and report exports.</summary>
public sealed class EventReportSummary {
    /// <summary>Captures completion evidence without retaining event rows.</summary>
    public EventReportSummary(long eventCount, long eventsScanned, bool scanLimitReached,
        string? completenessDiagnostic, IEnumerable<EventReportCoverage>? coverage = null) {
        if (eventCount < 0) {
            throw new ArgumentOutOfRangeException(nameof(eventCount));
        }
        if (eventsScanned < 0) {
            throw new ArgumentOutOfRangeException(nameof(eventsScanned));
        }
        EventCount = eventCount;
        EventsScanned = eventsScanned;
        ScanLimitReached = scanLimitReached;
        CompletenessDiagnostic = completenessDiagnostic;
        Coverage = Array.AsReadOnly((coverage ?? Array.Empty<EventReportCoverage>())
            .Select(static source => new EventReportCoverage {
                MachineName = source.MachineName,
                LogName = source.LogName,
                Succeeded = source.Succeeded,
                Status = source.Status,
                Detail = source.Detail
            }).ToArray());
    }

    /// <summary>Captures a report's completion evidence.</summary>
    public static EventReportSummary Create(EventReport report) {
        if (report == null) {
            throw new ArgumentNullException(nameof(report));
        }
        return new EventReportSummary(report.Rows.Count, report.EventsScanned,
            report.ScanLimitReached, report.CompletenessDiagnostic, report.Coverage);
    }

    /// <summary>Version of this serialized completion contract.</summary>
    public int SchemaVersion => 1;
    /// <summary>Number of emitted event rows.</summary>
    public long EventCount { get; }
    /// <summary>Number of candidates evaluated.</summary>
    public long EventsScanned { get; }
    /// <summary>Whether a candidate or result bound prevented exhaustion.</summary>
    public bool ScanLimitReached { get; }
    /// <summary>Reason the selected input was not exhaustive, if known.</summary>
    public string? CompletenessDiagnostic { get; }
    /// <summary>Declared source completion and failures; empty means source coverage was not declared.</summary>
    public IReadOnlyList<EventReportCoverage> Coverage { get; }
    /// <summary>Whether source coverage was supplied for this query.</summary>
    public bool HasDeclaredCoverage => Coverage.Count > 0;
    /// <summary>Whether the query completed without reported bounds, diagnostics, or source failures.</summary>
    public bool IsComplete => !ScanLimitReached && string.IsNullOrWhiteSpace(CompletenessDiagnostic) &&
        Coverage.All(static source => source.Succeeded);
}
