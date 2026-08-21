using System;
using EventViewerX.Reports;
using EventViewerX.Reports.Evtx;

namespace EventViewerX.Reports.Security;

/// <summary>
/// Executes EVTX security report queries using typed contracts.
/// </summary>
internal static class SecurityEvtxQueryExecutor {
    private const string SecurityProviderName = "Microsoft-Windows-Security-Auditing";
    private const int EventIdFailedLogon = 4625;
    private const int EventIdAccountLockout = 4740;
    private static readonly int[] UserLogonEventIds = { 4624, 4625, 4634, 4647 };

    /// <summary>
    /// Builds a logon-related security report from an EVTX file.
    /// </summary>
    public static bool TryBuildUserLogons(
        SecurityEvtxQueryRequest request,
        out SecurityUserLogonsQueryResult result,
        out EvtxQueryFailure? failure,
        CancellationToken cancellationToken = default) {

        if (!TryValidateRequest(request, out failure)) {
            result = new SecurityUserLogonsQueryResult();
            return false;
        }

        var evtxRequest = CreateEvtxRequest(request, UserLogonEventIds);
        if (!SecurityUserLogonsReportBuilder.TryBuildFromFile(
                request: evtxRequest,
                includeSamples: request.IncludeSamples,
                sampleSize: request.SampleSize,
                report: out var report,
                failure: out failure,
                cancellationToken: cancellationToken)) {
            result = new SecurityUserLogonsQueryResult();
            return false;
        }

        result = new SecurityUserLogonsQueryResult {
            Path = request.FilePath,
            ProviderName = SecurityProviderName,
            EventIds = UserLogonEventIds,
            MaxEventsScanned = request.MaxEventsScanned,
            ScannedEvents = report.Scanned,
            MatchedEvents = report.Matched,
            Truncated = report.Truncated,
            TimeCreatedUtcMin = report.MinUtc,
            TimeCreatedUtcMax = report.MaxUtc,
            Top = request.Top,
            ByEventId = ReportAggregates.TopIntRows(report.ByEventId, request.Top, "id"),
            ByTargetUser = ReportAggregates.TopStringRows(report.ByTargetUser, request.Top, "user"),
            ByTargetDomain = ReportAggregates.TopStringRows(report.ByTargetDomain, request.Top, "domain"),
            ByLogonType = ReportAggregates.TopStringRows(report.ByLogonType, request.Top, "logon_type"),
            ByIpAddress = ReportAggregates.TopStringRows(report.ByIpAddress, request.Top, "ip"),
            ByWorkstationName = ReportAggregates.TopStringRows(report.ByWorkstationName, request.Top, "workstation"),
            ByComputerName = ReportAggregates.TopStringRows(report.ByComputerName, request.Top, "computer"),
            IncludeSamples = request.IncludeSamples,
            SampleSize = request.IncludeSamples ? request.SampleSize : null,
            Samples = request.IncludeSamples ? report.Samples : null,
            StartTimeUtc = request.StartTimeUtc,
            EndTimeUtc = request.EndTimeUtc
        };
        failure = null;
        return true;
    }

    /// <summary>
    /// Builds a failed-logons security report from an EVTX file.
    /// </summary>
    public static bool TryBuildFailedLogons(
        SecurityEvtxQueryRequest request,
        out SecurityFailedLogonsQueryResult result,
        out EvtxQueryFailure? failure,
        CancellationToken cancellationToken = default) {

        if (!TryValidateRequest(request, out failure)) {
            result = new SecurityFailedLogonsQueryResult();
            return false;
        }

        var evtxRequest = CreateEvtxRequest(request, new[] { EventIdFailedLogon });
        if (!SecurityFailedLogonsReportBuilder.TryBuildFromFile(
                request: evtxRequest,
                includeSamples: request.IncludeSamples,
                sampleSize: request.SampleSize,
                report: out var report,
                failure: out failure,
                cancellationToken: cancellationToken)) {
            result = new SecurityFailedLogonsQueryResult();
            return false;
        }

        result = new SecurityFailedLogonsQueryResult {
            Path = request.FilePath,
            ProviderName = SecurityProviderName,
            EventId = EventIdFailedLogon,
            MaxEventsScanned = request.MaxEventsScanned,
            ScannedEvents = report.Scanned,
            MatchedEvents = report.Matched,
            Truncated = report.Truncated,
            TimeCreatedUtcMin = report.MinUtc,
            TimeCreatedUtcMax = report.MaxUtc,
            Top = request.Top,
            ByTargetUser = ReportAggregates.TopStringRows(report.ByTargetUser, request.Top, "user"),
            ByTargetDomain = ReportAggregates.TopStringRows(report.ByTargetDomain, request.Top, "domain"),
            ByLogonType = ReportAggregates.TopStringRows(report.ByLogonType, request.Top, "logon_type"),
            ByIpAddress = ReportAggregates.TopStringRows(report.ByIpAddress, request.Top, "ip"),
            ByWorkstationName = ReportAggregates.TopStringRows(report.ByWorkstationName, request.Top, "workstation"),
            ByComputerName = ReportAggregates.TopStringRows(report.ByComputerName, request.Top, "computer"),
            ByStatus = ReportAggregates.TopStringRows(report.ByStatus, request.Top, "status"),
            ByStatusName = ReportAggregates.TopStringRows(report.ByStatusName, request.Top, "status"),
            BySubStatus = ReportAggregates.TopStringRows(report.BySubStatus, request.Top, "sub_status"),
            BySubStatusName = ReportAggregates.TopStringRows(report.BySubStatusName, request.Top, "sub_status"),
            ByFailureReason = ReportAggregates.TopStringRows(report.ByFailureReason, request.Top, "failure_reason"),
            IncludeSamples = request.IncludeSamples,
            SampleSize = request.IncludeSamples ? request.SampleSize : null,
            Samples = request.IncludeSamples ? report.Samples : null,
            StartTimeUtc = request.StartTimeUtc,
            EndTimeUtc = request.EndTimeUtc
        };
        failure = null;
        return true;
    }

    /// <summary>
    /// Builds an account-lockouts security report from an EVTX file.
    /// </summary>
    public static bool TryBuildAccountLockouts(
        SecurityEvtxQueryRequest request,
        out SecurityAccountLockoutsQueryResult result,
        out EvtxQueryFailure? failure,
        CancellationToken cancellationToken = default) {

        if (!TryValidateRequest(request, out failure)) {
            result = new SecurityAccountLockoutsQueryResult();
            return false;
        }

        var evtxRequest = CreateEvtxRequest(request, new[] { EventIdAccountLockout });
        if (!SecurityAccountLockoutsReportBuilder.TryBuildFromFile(
                request: evtxRequest,
                includeSamples: request.IncludeSamples,
                sampleSize: request.SampleSize,
                report: out var report,
                failure: out failure,
                cancellationToken: cancellationToken)) {
            result = new SecurityAccountLockoutsQueryResult();
            return false;
        }

        result = new SecurityAccountLockoutsQueryResult {
            Path = request.FilePath,
            ProviderName = SecurityProviderName,
            EventId = EventIdAccountLockout,
            MaxEventsScanned = request.MaxEventsScanned,
            ScannedEvents = report.Scanned,
            MatchedEvents = report.Matched,
            Truncated = report.Truncated,
            TimeCreatedUtcMin = report.MinUtc,
            TimeCreatedUtcMax = report.MaxUtc,
            Top = request.Top,
            ByTargetUser = ReportAggregates.TopStringRows(report.ByTargetUser, request.Top, "user"),
            ByTargetDomain = ReportAggregates.TopStringRows(report.ByTargetDomain, request.Top, "domain"),
            ByCallerComputerName = ReportAggregates.TopStringRows(report.ByCallerComputerName, request.Top, "computer"),
            BySubjectUser = ReportAggregates.TopStringRows(report.BySubjectUser, request.Top, "user"),
            ByComputerName = ReportAggregates.TopStringRows(report.ByComputerName, request.Top, "computer"),
            IncludeSamples = request.IncludeSamples,
            SampleSize = request.IncludeSamples ? request.SampleSize : null,
            Samples = request.IncludeSamples ? report.Samples : null,
            StartTimeUtc = request.StartTimeUtc,
            EndTimeUtc = request.EndTimeUtc
        };
        failure = null;
        return true;
    }

    private static EvtxQueryRequest CreateEvtxRequest(SecurityEvtxQueryRequest request, IReadOnlyList<int> eventIds) {
        return new EvtxQueryRequest {
            FilePath = request.FilePath,
            EventIds = eventIds,
            ProviderName = SecurityProviderName,
            StartTimeUtc = request.StartTimeUtc,
            EndTimeUtc = request.EndTimeUtc,
            MaxEvents = request.MaxEventsScanned,
            OldestFirst = true,
            ReadMode = EventReadMode.StructuredData
        };
    }

    private static bool TryValidateRequest(SecurityEvtxQueryRequest request, out EvtxQueryFailure? failure) {
        if (request is null) {
            failure = new EvtxQueryFailure {
                Kind = EvtxQueryFailureKind.InvalidArgument,
                Message = "request is required."
            };
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.FilePath)) {
            failure = new EvtxQueryFailure {
                Kind = EvtxQueryFailureKind.InvalidArgument,
                Message = "filePath is required."
            };
            return false;
        }

        if (request.MaxEventsScanned < 0) {
            failure = new EvtxQueryFailure {
                Kind = EvtxQueryFailureKind.InvalidArgument,
                Message = "maxEventsScanned must be greater than or equal to 0."
            };
            return false;
        }

        if (request.Top < 0) {
            failure = new EvtxQueryFailure {
                Kind = EvtxQueryFailureKind.InvalidArgument,
                Message = "top must be greater than or equal to 0."
            };
            return false;
        }

        if (request.SampleSize < 0) {
            failure = new EvtxQueryFailure {
                Kind = EvtxQueryFailureKind.InvalidArgument,
                Message = "sampleSize must be greater than or equal to 0."
            };
            return false;
        }

        if (request.StartTimeUtc.HasValue &&
            request.EndTimeUtc.HasValue &&
            request.StartTimeUtc.Value > request.EndTimeUtc.Value) {
            failure = new EvtxQueryFailure {
                Kind = EvtxQueryFailureKind.InvalidArgument,
                Message = "startTimeUtc must be less than or equal to endTimeUtc."
            };
            return false;
        }

        failure = null;
        return true;
    }
}
