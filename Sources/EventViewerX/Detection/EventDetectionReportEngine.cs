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
        EventReport presentation = EventReportEngine.Create(
            presentationInput,
            title,
            CreatePresentationCoverage(executionCoverage),
            executionCoverage.IsComplete
                ? null
                : "Detection coverage is incomplete. Review the coverage rows for missing scope and failures.");
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

    private static IReadOnlyList<EventReportCoverage> CreatePresentationCoverage(
        EventDetectionCoverage coverage) {

        var result = new List<EventReportCoverage>();
        AddObserved(result, coverage.ObservedTargets, "Observed target");
        AddObserved(result, coverage.ObservedChannels, "Observed channel");
        AddObserved(result, coverage.ObservedProviders, "Observed provider");
        AddObserved(result, coverage.ObservedEventIds.Select(static value =>
            value.ToString(System.Globalization.CultureInfo.InvariantCulture)), "Observed event ID");
        AddObserved(result, coverage.ObservedEventTypes.Select(static value => value.ToString()), "Observed event type");
        AddMissing(result, coverage.MissingTargets, "Missing target");
        AddMissing(result, coverage.MissingChannels, "Missing channel");
        AddMissing(result, coverage.MissingProviders, "Missing provider");
        AddMissing(result, coverage.MissingEventIds.Select(static value =>
            value.ToString(System.Globalization.CultureInfo.InvariantCulture)), "Missing event ID");
        AddMissing(result, coverage.MissingEventTypes.Select(static value => value.ToString()), "Missing event type");
        foreach (string failure in coverage.Failures) {
            result.Add(new EventReportCoverage {
                MachineName = "Detection",
                LogName = "Coverage",
                Succeeded = false,
                Status = "Detection coverage failure",
                Detail = failure
            });
        }
        return result;
    }

    private static void AddObserved(
        ICollection<EventReportCoverage> result,
        IEnumerable<string> values,
        string status) {

        foreach (string value in values) {
            result.Add(new EventReportCoverage {
                MachineName = string.Equals(status, "Observed target", StringComparison.Ordinal)
                    ? value
                    : "Detection",
                LogName = string.Equals(status, "Observed channel", StringComparison.Ordinal)
                    ? value
                    : status,
                Succeeded = true,
                Status = status,
                Detail = value
            });
        }
    }

    private static void AddMissing(
        ICollection<EventReportCoverage> result,
        IEnumerable<string> values,
        string status) {

        foreach (string value in values) {
            result.Add(new EventReportCoverage {
                MachineName = string.Equals(status, "Missing target", StringComparison.Ordinal)
                    ? value
                    : "Detection",
                LogName = string.Equals(status, "Missing channel", StringComparison.Ordinal)
                    ? value
                    : status,
                Succeeded = false,
                Status = status,
                Detail = value
            });
        }
    }
}
