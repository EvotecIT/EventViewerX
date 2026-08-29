using EventViewerX.Reporting;

namespace EventViewerX;

/// <summary>Builds reusable detection health and incident reports without rerunning source queries.</summary>
public static class EventDetectionReportEngine {
    /// <summary>Creates a renderer-ready report over existing observations, findings, and pack manifests.</summary>
    public static EventDetectionReportSnapshot Create(
        IEnumerable<EventObservation>? observations,
        IEnumerable<EventDetectionFinding>? findings,
        IEnumerable<EventDetectionPack>? packs = null,
        EventDetectionReportOptions? options = null,
        IEnumerable<EventDecisionMetric>? metrics = null) {

        options ??= new EventDetectionReportOptions();
        string title = string.IsNullOrWhiteSpace(options.Title)
            ? "EventViewerX detection report"
            : options.Title.Trim();
        string queryOwner = string.IsNullOrWhiteSpace(options.QueryOwner)
            ? "Caller-supplied observations"
            : options.QueryOwner.Trim();
        EventDetectionFinding[] findingSnapshot = (findings ?? Array.Empty<EventDetectionFinding>()).ToArray();
        if (findingSnapshot.Any(static finding => finding == null)) {
            throw new ArgumentException("Findings cannot contain null values.", nameof(findings));
        }
        EventObservation[] suppliedObservations = (observations ?? Array.Empty<EventObservation>()).ToArray();
        if (suppliedObservations.Any(static observation => observation == null)) {
            throw new ArgumentException("Observations cannot contain null values.", nameof(observations));
        }
        EventObservation[] observationSnapshot = suppliedObservations
            .Concat(findingSnapshot.SelectMany(static finding => finding.Evidence))
            .GroupBy(static observation => observation.Identity, StringComparer.Ordinal)
            .Select(static group => group.First())
            .OrderBy(static observation => observation.EventTimeUtc)
            .ToArray();
        EventDetectionCoverage executionCoverage = ResolveCoverage(options.Coverage, findingSnapshot);
        EventDetectionPackHealth[] packHealth = (packs ?? Array.Empty<EventDetectionPack>())
            .Select(pack => {
                if (pack == null) {
                    throw new ArgumentException("Packs cannot contain null values.", nameof(packs));
                }
                return new EventDetectionPackHealth(
                    pack,
                    pack.Validate(),
                    pack.GetCoverage(),
                    executionCoverage);
            })
            .OrderBy(static pack => pack.PackId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        EventTimeline timeline = EventTimelineEngine.Create(observationSnapshot, findingSnapshot);
        object[] presentationInput = (metrics ?? Array.Empty<EventDecisionMetric>()).Cast<object>()
            .Concat(findingSnapshot.Cast<object>())
            .Concat(timeline.Entries.Cast<object>())
            .ToArray();
        EventReport presentation = EventReportEngine.Create(presentationInput, title);
        string[] limits = Normalize(options.Limits);
        string[] failures = Normalize(options.Failures)
            .Concat(findingSnapshot
                .Where(static finding => finding.Status != EventDetectionFindingStatus.Matched)
                .Select(static finding => finding.CompletenessDiagnostic ?? finding.Explanation))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new EventDetectionReportSnapshot(
            title,
            DateTime.UtcNow,
            timeline.StartTimeUtc,
            timeline.EndTimeUtc,
            queryOwner,
            options.UsedStorageHistory,
            observationSnapshot.Select(static observation => observation.SourceComputer)
                .Concat(observationSnapshot.Select(static observation => observation.CollectorComputer))
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            observationSnapshot.Select(static observation => observation.SourceLog)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            limits,
            failures,
            executionCoverage,
            observationSnapshot,
            findingSnapshot,
            packHealth,
            timeline,
            presentation);
    }

    private static EventDetectionCoverage ResolveCoverage(
        EventDetectionCoverage? supplied,
        IReadOnlyList<EventDetectionFinding> findings) {

        if (supplied != null) {
            return supplied.Snapshot();
        }
        if (findings.Count == 0 || findings.Any(static finding => !finding.Coverage.IsDeclared)) {
            return EventDetectionCoverage.Unknown();
        }
        string first = findings[0].Coverage.ToJson();
        return findings.All(finding => string.Equals(first, finding.Coverage.ToJson(), StringComparison.Ordinal))
            ? findings[0].Coverage.Snapshot()
            : EventDetectionCoverage.Unknown();
    }

    private static string[] Normalize(IEnumerable<string>? values) =>
        (values ?? Array.Empty<string>())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
