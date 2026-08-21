using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using EventViewerX.Reports.QueryHelpers;

namespace EventViewerX.Reports.Evtx;

/// <summary>
/// Executes EVTX queries using a stable request/response contract.
/// </summary>
internal static class EvtxQueryExecutor {
    /// <summary>
    /// Streams EVTX events to a callback and returns typed failures on errors.
    /// </summary>
    /// <param name="request">EVTX query request.</param>
    /// <param name="eventHandler">Callback invoked for each event. Return <see langword="false"/> to stop early.</param>
    /// <param name="failure">Failure details when query fails.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when query succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryForEachEvent(
        EvtxQueryRequest? request,
        Func<EventObject, bool> eventHandler,
        out EvtxQueryFailure? failure,
        CancellationToken cancellationToken = default) {

        return TryForEachEventWithInfo(request, eventHandler, out _, out failure, cancellationToken);
    }

    /// <summary>
    /// Streams EVTX events and reports whether the query was capped or stopped by the callback.
    /// </summary>
    public static bool TryForEachEventWithInfo(
        EvtxQueryRequest? request,
        Func<EventObject, bool> eventHandler,
        out EvtxQueryExecutionInfo executionInfo,
        out EvtxQueryFailure? failure,
        CancellationToken cancellationToken = default,
        EventReadMode? readModeOverride = null) {

        executionInfo = new EvtxQueryExecutionInfo();

        if (eventHandler is null) {
            failure = new EvtxQueryFailure {
                Kind = EvtxQueryFailureKind.InvalidArgument,
                Message = "eventHandler is required."
            };
            return false;
        }

        if (!TryValidateRequest(request, out failure)) {
            return false;
        }
        if (readModeOverride.HasValue &&
            !Enum.IsDefined(
                typeof(EventReadMode),
                readModeOverride.Value)) {
            failure = new EvtxQueryFailure {
                Kind = EvtxQueryFailureKind.InvalidArgument,
                Message = "readModeOverride is not supported."
            };
            return false;
        }
        EvtxQueryRequest validatedRequest = request!;
        int maxEvents = validatedRequest.MaxEvents;

        try {
            long readLimit =
                maxEvents > 0 &&
                maxEvents < int.MaxValue
                    ? maxEvents + 1L
                    : maxEvents;
            var query = new EventLogFileQuery(
                validatedRequest.FilePath) {
                XPath = EventFilterCompiler.BuildXPath(
                    new EventFilter {
                        EventIds = validatedRequest.EventIds,
                        ProviderNames =
                            string.IsNullOrWhiteSpace(
                                validatedRequest.ProviderName)
                                ? null
                                : new[] {
                                    validatedRequest.ProviderName!
                                },
                        StartTime =
                            validatedRequest.StartTimeUtc,
                        EndTime =
                            validatedRequest.EndTimeUtc
                    }),
                MaxEvents = readLimit,
                Oldest = validatedRequest.OldestFirst,
                ReadMode =
                    readModeOverride ??
                    validatedRequest.ReadMode
            };
            foreach (EventObject ev in
                     EventLogEngine.ReadFile(
                         query,
                         cancellationToken)) {
                cancellationToken.ThrowIfCancellationRequested();

                if (maxEvents > 0 &&
                    executionInfo.EventsDelivered >= maxEvents) {
                    executionInfo.Truncated = true;
                    break;
                }

                bool continueReading;
                try {
                    continueReading =
                        eventHandler(ev);
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception ex) {
                    failure = new EvtxQueryFailure {
                        Kind =
                            EvtxQueryFailureKind.Exception,
                        Message = ex.Message
                    };
                    return false;
                }
                if (!continueReading) {
                    executionInfo.EventsDelivered++;
                    executionInfo.StoppedByHandler = true;
                    break;
                }
                executionInfo.EventsDelivered++;
            }

            failure = null;
            return true;
        } catch (OperationCanceledException) {
            throw;
        } catch (ArgumentException ex) {
            failure = new EvtxQueryFailure {
                Kind = EvtxQueryFailureKind.InvalidArgument,
                Message = ex.Message
            };
            return false;
        } catch (FileNotFoundException ex) {
            failure = new EvtxQueryFailure {
                Kind = EvtxQueryFailureKind.NotFound,
                Message = ex.Message
            };
            return false;
        } catch (UnauthorizedAccessException ex) {
            failure = new EvtxQueryFailure {
                Kind = EvtxQueryFailureKind.AccessDenied,
                Message = ex.Message
            };
            return false;
        } catch (IOException ex) {
            failure = new EvtxQueryFailure {
                Kind = EvtxQueryFailureKind.IoError,
                Message = ex.Message
            };
            return false;
        } catch (Exception ex) {
            failure = new EvtxQueryFailure {
                Kind = EvtxQueryFailureKind.Exception,
                Message = ex.Message
            };
            return false;
        }
    }

    /// <summary>
    /// Queries an EVTX file and returns either events or a typed failure.
    /// </summary>
    /// <param name="request">EVTX query request.</param>
    /// <param name="result">Result object with queried events.</param>
    /// <param name="failure">Failure details when query fails.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when query succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryRead(
        EvtxQueryRequest? request,
        out EvtxQueryResult result,
        out EvtxQueryFailure? failure,
        CancellationToken cancellationToken = default) {
        var list = new List<EventObject>();
        if (!TryForEachEventWithInfo(
                request,
                ev => {
                    list.Add(ev);
                    return true;
                },
                out EvtxQueryExecutionInfo executionInfo,
                out failure,
                cancellationToken)) {
            result = new EvtxQueryResult();
            return false;
        }

        result = new EvtxQueryResult {
            Events = list,
            Truncated = executionInfo.Truncated
        };
        return true;
    }

    private static bool TryValidateRequest(EvtxQueryRequest? request, out EvtxQueryFailure? failure) {
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

        if (QueryValidationHelpers.HasInvalidUtcRange(request.StartTimeUtc, request.EndTimeUtc)) {
            failure = new EvtxQueryFailure {
                Kind = EvtxQueryFailureKind.InvalidArgument,
                Message = "startTimeUtc must be less than or equal to endTimeUtc."
            };
            return false;
        }

        if (QueryValidationHelpers.IsNegative(request.MaxEvents)) {
            failure = new EvtxQueryFailure {
                Kind = EvtxQueryFailureKind.InvalidArgument,
                Message = "maxEvents must be greater than or equal to 0."
            };
            return false;
        }

        if (request.EventIds != null &&
            request.EventIds.Any(static eventId =>
                eventId < EventIdValidation.Minimum ||
                eventId > EventIdValidation.Maximum)) {
            failure = new EvtxQueryFailure {
                Kind = EvtxQueryFailureKind.InvalidArgument,
                Message = $"eventIds must contain values from {EventIdValidation.Minimum} through {EventIdValidation.Maximum}."
            };
            return false;
        }
        if (!Enum.IsDefined(
                typeof(EventReadMode),
                request.ReadMode)) {
            failure = new EvtxQueryFailure {
                Kind = EvtxQueryFailureKind.InvalidArgument,
                Message = "readMode is not supported."
            };
            return false;
        }

        failure = null;
        return true;
    }
}
