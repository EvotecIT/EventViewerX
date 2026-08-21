using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;
using System.Threading;
using EventViewerX.Reports.QueryHelpers;

namespace EventViewerX.Reports.Live;

/// <summary>
/// Executes live event channel queries using typed contracts.
/// </summary>
internal static class LiveEventQueryExecutor {
    /// <summary>
    /// Reads events from a live event log channel.
    /// </summary>
    /// <param name="request">Live query request.</param>
    /// <param name="result">Result payload when successful.</param>
    /// <param name="failure">Failure payload when query fails.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> on success; otherwise <see langword="false"/>.</returns>
    public static bool TryRead(
        LiveEventQueryRequest request,
        out LiveEventQueryResult result,
        out LiveEventQueryFailure? failure,
        CancellationToken cancellationToken = default) {

        if (request is null) {
            result = new LiveEventQueryResult();
            failure = new LiveEventQueryFailure {
                Kind = LiveEventQueryFailureKind.InvalidArgument,
                Message = "request is required."
            };
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.LogName)) {
            result = new LiveEventQueryResult();
            failure = new LiveEventQueryFailure {
                Kind = LiveEventQueryFailureKind.InvalidArgument,
                Message = "logName is required."
            };
            return false;
        }

        if (QueryValidationHelpers.IsNegative(request.MaxEvents)) {
            result = new LiveEventQueryResult();
            failure = new LiveEventQueryFailure {
                Kind = LiveEventQueryFailureKind.InvalidArgument,
                Message = "maxEvents must be greater than or equal to 0."
            };
            return false;
        }

        if (QueryValidationHelpers.IsNegative(request.MaxMessageChars)) {
            result = new LiveEventQueryResult();
            failure = new LiveEventQueryFailure {
                Kind = LiveEventQueryFailureKind.InvalidArgument,
                Message = "maxMessageChars must be greater than or equal to 0."
            };
            return false;
        }

        if (QueryValidationHelpers.IsNonPositiveWhenProvided(request.SessionTimeoutMs)) {
            result = new LiveEventQueryResult();
            failure = new LiveEventQueryFailure {
                Kind = LiveEventQueryFailureKind.InvalidArgument,
                Message = "sessionTimeoutMs must be positive when provided."
            };
            return false;
        }

        var xpath = string.IsNullOrWhiteSpace(request.XPath) ? "*" : request.XPath!;

        try {
            var rows = new List<LiveEventRow>();
            bool truncated = false;
            long readLimit =
                request.MaxEvents > 0 &&
                request.MaxEvents < int.MaxValue
                    ? request.MaxEvents + 1L
                    : request.MaxEvents;
            EventLogChannelQuery query =
                LiveEventChannelQueryFactory.Create(
                    request.LogName,
                    request.MachineName,
                    xpath,
                    readLimit,
                    request.OldestFirst,
                    request.IncludeMessage
                    ? EventReadMode.Message
                    : EventReadMode.Metadata,
                    request.SessionTimeoutMs);

            foreach (EventObject ev in
                     EventLogEngine.ReadChannel(
                         query,
                         cancellationToken)) {
                cancellationToken.ThrowIfCancellationRequested();

                if (request.MaxEvents > 0 && rows.Count >= request.MaxEvents) {
                    truncated = true;
                    break;
                }

                rows.Add(
                    ProjectRow(
                        ev,
                        request.IncludeMessage,
                        request.MaxMessageChars));
            }

            result = new LiveEventQueryResult {
                MachineName = string.IsNullOrWhiteSpace(request.MachineName)
                    ? Environment.MachineName
                    : request.MachineName!.Trim(),
                LogName = request.LogName,
                XPath = xpath,
                Count = rows.Count,
                Truncated = truncated,
                Events = rows
            };
            failure = null;
            return true;
        } catch (OperationCanceledException) {
            throw;
        } catch (UnauthorizedAccessException ex) {
            result = new LiveEventQueryResult();
            failure = new LiveEventQueryFailure {
                Kind = LiveEventQueryFailureKind.AccessDenied,
                Message = ex.Message
            };
            return false;
        } catch (TimeoutException ex) {
            result = new LiveEventQueryResult();
            failure = new LiveEventQueryFailure {
                Kind = LiveEventQueryFailureKind.Timeout,
                Message = ex.Message
            };
            return false;
        } catch (EventLogSessionException ex) {
            result = new LiveEventQueryResult();
            failure = new LiveEventQueryFailure {
                Kind = LiveEventQueryFailureKind.HostUnavailable,
                Message = ex.Message
            };
            return false;
        } catch (EventLogNotFoundException ex) {
            result = new LiveEventQueryResult();
            failure = new LiveEventQueryFailure {
                Kind = LiveEventQueryFailureKind.LogNotFound,
                Message = ex.Message
            };
            return false;
        } catch (Win32Exception ex) {
            result = new LiveEventQueryResult();
            failure = new LiveEventQueryFailure {
                Kind = QueryFailureHelpers.Classify(ex) switch {
                    NativeQueryFailureKind.InvalidQuery =>
                        LiveEventQueryFailureKind.InvalidQuery,
                    NativeQueryFailureKind.LogNotFound =>
                        LiveEventQueryFailureKind.LogNotFound,
                    NativeQueryFailureKind.AccessDenied =>
                        LiveEventQueryFailureKind.AccessDenied,
                    NativeQueryFailureKind.Timeout =>
                        LiveEventQueryFailureKind.Timeout,
                    NativeQueryFailureKind.HostUnavailable =>
                        LiveEventQueryFailureKind.HostUnavailable,
                    _ =>
                        LiveEventQueryFailureKind.Exception
                },
                Message = ex.Message
            };
            return false;
        } catch (EventLogException ex) {
            result = new LiveEventQueryResult();
            failure = new LiveEventQueryFailure {
                Kind = QueryFailureHelpers.IsInvalidEventQuery(ex)
                    ? LiveEventQueryFailureKind.InvalidQuery
                    : QueryFailureHelpers.IsTimeoutLike(ex.Message)
                        ? LiveEventQueryFailureKind.Timeout
                        : LiveEventQueryFailureKind.Exception,
                Message = ex.Message
            };
            return false;
        } catch (ArgumentException ex) {
            result = new LiveEventQueryResult();
            failure = new LiveEventQueryFailure {
                Kind = LiveEventQueryFailureKind.InvalidArgument,
                Message = ex.Message
            };
            return false;
        } catch (Exception ex) {
            result = new LiveEventQueryResult();
            failure = new LiveEventQueryFailure {
                Kind = LiveEventQueryFailureKind.Exception,
                Message = ex.Message
            };
            return false;
        }
    }

    internal static string? FormatTimeCreatedUtc(
        DateTime timeCreated) {

        return timeCreated == DateTime.MinValue
            ? null
            : timeCreated
                .ToUniversalTime()
                .ToString("O");
    }

    internal static LiveEventRow ProjectRow(
        EventObject eventObject,
        bool includeMessage,
        int maxMessageChars) {

        return new LiveEventRow {
            TimeCreatedUtc = FormatTimeCreatedUtc(
                eventObject.TimeCreated),
            Id = eventObject.Id,
            RecordId = eventObject.RecordId,
            LogName = eventObject.LogName ??
                      string.Empty,
            ProviderName = eventObject.ProviderName ??
                           string.Empty,
            Level = eventObject.Level,
            LevelDisplayName =
                eventObject.LevelDisplayName ??
                string.Empty,
            Task = eventObject.Task,
            Opcode = eventObject.Opcode,
            Keywords = eventObject.Keywords,
            MachineName = eventObject.MachineName ??
                          string.Empty,
            UserSid =
                EventProjectionHelpers.SafeGetUserSid(
                    eventObject),
            Message = includeMessage
                ? EventProjectionHelpers.TruncateSafe(
                    EventProjectionHelpers.SafeGetMessage(
                        eventObject),
                    maxMessageChars)
                : null
        };
    }
}
