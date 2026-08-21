using System;
using System.Linq;
using System.Threading;
using EventViewerX.Reports;
using EventViewerX.Reports.Evtx;

namespace EventViewerX.Reports.Stats;

/// <summary>
/// Executes EVTX statistics queries using typed contracts.
/// </summary>
internal static class EvtxStatsQueryExecutor {
    /// <summary>
    /// Reads an EVTX file and produces aggregate statistics.
    /// </summary>
    /// <param name="request">Stats query request.</param>
    /// <param name="result">Result payload on success.</param>
    /// <param name="failure">Failure payload on error.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> on success; otherwise <see langword="false"/>.</returns>
    public static bool TryBuild(
        EvtxStatsQueryRequest request,
        out EvtxStatsQueryResult result,
        out EvtxQueryFailure? failure,
        CancellationToken cancellationToken = default) {

        if (!TryValidateRequest(request, out result, out failure)) {
            return false;
        }

        var evtxRequest = new EvtxQueryRequest {
            FilePath = request.FilePath,
            EventIds = request.EventIds,
            ProviderName = request.ProviderName,
            StartTimeUtc = request.StartTimeUtc,
            EndTimeUtc = request.EndTimeUtc,
            MaxEvents = request.MaxEventsScanned,
            OldestFirst = request.OldestFirst,
            ReadMode = EventReadMode.Metadata
        };

        if (!EvtxStatsReportBuilder.TryBuildFromFile(evtxRequest, out var report, out failure, cancellationToken)) {
            result = new EvtxStatsQueryResult();
            return false;
        }

        result = new EvtxStatsQueryResult {
            Path = request.FilePath,
            ProviderName = request.ProviderName ?? string.Empty,
            OldestFirst = request.OldestFirst,
            MaxEventsScanned = request.MaxEventsScanned,
            ScannedEvents = report.Scanned,
            EventsWithoutLevel = report.EventsWithoutLevel,
            Truncated = report.Truncated,
            TimeCreatedUtcMin = report.MinUtc,
            TimeCreatedUtcMax = report.MaxUtc,
            StartTimeUtc = request.StartTimeUtc,
            EndTimeUtc = request.EndTimeUtc,
            EventIds = request.EventIds,
            TopEventIds = ReportAggregates.TopIntPairs(report.ByEventId, request.TopEventIds)
                .Select(static x => new EvtxStatsTopEventIdRow { Id = x.Key, Count = x.Value })
                .ToList(),
            TopProviders = ReportAggregates.TopStringPairs(report.ByProviderName, request.TopProviders)
                .Select(static x => new EvtxStatsTopProviderRow { ProviderName = x.Key, Count = x.Value })
                .ToList(),
            TopComputers = ReportAggregates.TopStringPairs(report.ByComputerName, request.TopComputers)
                .Select(static x => new EvtxStatsTopComputerRow { ComputerName = x.Key, Count = x.Value })
                .ToList(),
            TopLevels = report.ByLevel.Values
                .OrderByDescending(static x => x.Count)
                .ThenBy(static x => x.Level)
                .Take(request.TopLevels)
                .ToList()
        };
        failure = null;
        return true;
    }

    private static bool TryValidateRequest(
        EvtxStatsQueryRequest request,
        out EvtxStatsQueryResult result,
        out EvtxQueryFailure? failure) {
        if (request is null) {
            result = new EvtxStatsQueryResult();
            failure = new EvtxQueryFailure {
                Kind = EvtxQueryFailureKind.InvalidArgument,
                Message = "request is required."
            };
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.FilePath)) {
            result = new EvtxStatsQueryResult();
            failure = new EvtxQueryFailure {
                Kind = EvtxQueryFailureKind.InvalidArgument,
                Message = "filePath is required."
            };
            return false;
        }

        if (request.MaxEventsScanned < 0) {
            result = new EvtxStatsQueryResult();
            failure = new EvtxQueryFailure {
                Kind = EvtxQueryFailureKind.InvalidArgument,
                Message = "maxEventsScanned must be greater than or equal to 0."
            };
            return false;
        }

        if (request.StartTimeUtc.HasValue &&
            request.EndTimeUtc.HasValue &&
            request.StartTimeUtc.Value > request.EndTimeUtc.Value) {
            result = new EvtxStatsQueryResult();
            failure = new EvtxQueryFailure {
                Kind = EvtxQueryFailureKind.InvalidArgument,
                Message = "startTimeUtc must be less than or equal to endTimeUtc."
            };
            return false;
        }

        if (request.EventIds is not null &&
            request.EventIds.Any(static id =>
                id < EventIdValidation.Minimum ||
                id > EventIdValidation.Maximum)) {
            result = new EvtxStatsQueryResult();
            failure = new EvtxQueryFailure {
                Kind = EvtxQueryFailureKind.InvalidArgument,
                Message =
                    $"eventIds must contain only values from {EventIdValidation.Minimum} through {EventIdValidation.Maximum}."
            };
            return false;
        }

        if (request.TopEventIds < 0 || request.TopProviders < 0 || request.TopComputers < 0 || request.TopLevels < 0) {
            result = new EvtxStatsQueryResult();
            failure = new EvtxQueryFailure {
                Kind = EvtxQueryFailureKind.InvalidArgument,
                Message = "Top values must be greater than or equal to 0."
            };
            return false;
        }

        result = new EvtxStatsQueryResult();
        failure = null;
        return true;
    }
}
