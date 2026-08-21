using System;
using System.Collections.Generic;
using System.Threading;
using EventViewerX.Reports.QueryHelpers;

namespace EventViewerX.Reports.Evtx;

/// <summary>
/// Builds typed EVTX event reports from <see cref="EvtxQueryRequest"/>.
/// </summary>
internal static class EvtxEventReportBuilder {
    /// <summary>
    /// Reads an EVTX file and projects typed event rows for tool/report consumption.
    /// </summary>
    /// <param name="request">EVTX query request.</param>
    /// <param name="includeMessage">Whether to include formatted message text.</param>
    /// <param name="maxMessageChars">Maximum message length when included.</param>
    /// <param name="report">Projected event report on success.</param>
    /// <param name="failure">Failure payload on error.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> on success; otherwise <see langword="false"/>.</returns>
    public static bool TryBuild(
        EvtxQueryRequest? request,
        bool includeMessage,
        int maxMessageChars,
        out EvtxEventReportResult report,
        out EvtxQueryFailure? failure,
        CancellationToken cancellationToken = default) {

        if (QueryValidationHelpers.IsNegative(maxMessageChars)) {
            report = new EvtxEventReportResult();
            failure = new EvtxQueryFailure {
                Kind = EvtxQueryFailureKind.InvalidArgument,
                Message = "maxMessageChars must be greater than or equal to 0."
            };
            return false;
        }

        int requestedMaxEvents = request?.MaxEvents ?? 0;
        int estimatedCapacity = requestedMaxEvents > 0 ? Math.Min(requestedMaxEvents, 256) : 0;
        var rows = estimatedCapacity > 0
            ? new List<EvtxEventReportRow>(estimatedCapacity)
            : new List<EvtxEventReportRow>();

        if (!EvtxQueryExecutor.TryForEachEventWithInfo(
                request,
                ev => {
                    rows.Add(
                        ProjectRow(
                            ev,
                            includeMessage,
                            maxMessageChars));
                    return true;
                },
                out EvtxQueryExecutionInfo executionInfo,
                out failure,
                cancellationToken,
                readModeOverride: includeMessage ? EventReadMode.Full : EventReadMode.StructuredData)) {
            report = new EvtxEventReportResult();
            return false;
        }

        var effectivePath = request!.FilePath ?? string.Empty;
        report = new EvtxEventReportResult {
            Path = effectivePath,
            Count = rows.Count,
            Truncated = executionInfo.Truncated,
            Events = rows
        };
        failure = null;
        return true;
    }

    internal static string? FormatTimeCreatedUtc(
        DateTime timeCreated) =>
        timeCreated == DateTime.MinValue
            ? null
            : timeCreated
                .ToUniversalTime()
                .ToString("O");

    internal static EvtxEventReportRow ProjectRow(
        EventObject eventObject,
        bool includeMessage,
        int maxMessageChars) {

        return new EvtxEventReportRow {
            TimeCreatedUtc =
                FormatTimeCreatedUtc(
                    eventObject.TimeCreated),
            Id = eventObject.Id,
            RecordId = eventObject.RecordId,
            LogName = eventObject.LogName ?? string.Empty,
            ProviderName = eventObject.ProviderName ?? string.Empty,
            Level = eventObject.Level,
            LevelDisplayName =
                eventObject.LevelDisplayName ?? string.Empty,
            ComputerName =
                eventObject.ComputerName ?? string.Empty,
            QueriedMachine =
                eventObject.QueriedMachine ?? string.Empty,
            GatheredFrom =
                eventObject.GatheredFrom ?? string.Empty,
            MessageSubject =
                eventObject.MessageSubject ?? string.Empty,
            UserSid =
                EventProjectionHelpers.SafeGetUserSid(
                    eventObject),
            Data =
                EventProjectionHelpers.NormalizeDict(
                    eventObject.Data),
            MessageData =
                EventProjectionHelpers.NormalizeDict(
                    eventObject.MessageData),
            Message = includeMessage
                ? EventProjectionHelpers.TruncateSafe(
                    EventProjectionHelpers.SafeGetMessage(
                        eventObject),
                    maxMessageChars)
                : null
        };
    }
}
