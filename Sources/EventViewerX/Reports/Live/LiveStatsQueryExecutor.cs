using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Linq;
using System.Threading;
using EventViewerX.Reports.QueryHelpers;
using EventViewerX.Reports.Stats;

namespace EventViewerX.Reports.Live;

/// <summary>
/// Executes live event statistics queries using typed contracts.
/// </summary>
internal static class LiveStatsQueryExecutor {
    /// <summary>
    /// Reads events from a live channel and produces aggregate statistics.
    /// </summary>
    /// <param name="request">Live stats request.</param>
    /// <param name="result">Result payload on success.</param>
    /// <param name="failure">Failure payload on error.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> on success; otherwise <see langword="false"/>.</returns>
    public static bool TryBuild(
        LiveStatsQueryRequest request,
        out LiveStatsQueryResult result,
        out LiveStatsQueryFailure? failure,
        CancellationToken cancellationToken = default) {

        if (!TryValidateRequest(request, out result, out failure)) {
            return false;
        }

        string xpath = BuildEffectiveXPath(request.XPath, request.StartTimeUtc, request.EndTimeUtc);
        var builder = new EvtxStatsReportBuilder();
        long scanned = 0;
        long matched = 0;
        bool truncated = false;
        DateTime? minUtc = null;
        DateTime? maxUtc = null;
        long readLimit =
            request.MaxEventsScanned > 0 &&
            request.MaxEventsScanned < long.MaxValue
                ? request.MaxEventsScanned + 1L
                : request.MaxEventsScanned;

        try {
            EventLogChannelQuery query =
                LiveEventChannelQueryFactory.Create(
                    request.LogName,
                    request.MachineName,
                    xpath,
                    readLimit,
                    request.OldestFirst,
                    EventReadMode.Metadata,
                    request.SessionTimeoutMs);
            foreach (EventObject ev in
                     EventLogEngine.ReadChannel(
                         query,
                         cancellationToken)) {
                cancellationToken.ThrowIfCancellationRequested();

                if (request.MaxEventsScanned > 0 && scanned >= request.MaxEventsScanned) {
                    truncated = true;
                    break;
                }

                scanned++;
                bool hasCreatedTime =
                    TryNormalizeCreatedTimeUtc(
                        ev.TimeCreated,
                        out DateTime createdUtc);
                if (hasCreatedTime &&
                    !IsWithinRange(
                        createdUtc,
                        request.StartTimeUtc,
                        request.EndTimeUtc)) {
                    continue;
                }

                matched++;
                if (hasCreatedTime) {
                    if (!minUtc.HasValue ||
                        createdUtc < minUtc.Value) {
                        minUtc = createdUtc;
                    }
                    if (!maxUtc.HasValue ||
                        createdUtc > maxUtc.Value) {
                        maxUtc = createdUtc;
                    }
                }

                builder.Add(ev);

            }

            result = new LiveStatsQueryResult {
                MachineName = string.IsNullOrWhiteSpace(request.MachineName)
                    ? Environment.MachineName
                    : request.MachineName!.Trim(),
                LogName = request.LogName,
                XPath = xpath,
                OldestFirst = request.OldestFirst,
                MaxEventsScanned = request.MaxEventsScanned,
                ScannedEvents = scanned,
                MatchedEvents = matched,
                EventsWithoutLevel =
                    builder.EventsWithoutLevel,
                Truncated = truncated,
                TimeCreatedUtcMin = minUtc,
                TimeCreatedUtcMax = maxUtc,
                StartTimeUtc = request.StartTimeUtc,
                EndTimeUtc = request.EndTimeUtc,
                TopEventIds = builder.GetTopEventIds(request.TopEventIds)
                    .Select(static x => new TopEventIdRow { Id = x.Key, Count = x.Value })
                    .ToList(),
                TopProviders = builder.GetTopProviders(request.TopProviders)
                    .Select(static x => new TopProviderRow { ProviderName = x.Key, Count = x.Value })
                    .ToList(),
                TopComputers = builder.GetTopComputers(request.TopComputers)
                    .Select(static x => new TopComputerRow { ComputerName = x.Key, Count = x.Value })
                    .ToList(),
                TopLevels = builder.GetTopLevels(request.TopLevels)
            };
            failure = null;
            return true;
        } catch (OperationCanceledException) {
            throw;
        } catch (UnauthorizedAccessException ex) {
            result = new LiveStatsQueryResult();
            failure = new LiveStatsQueryFailure {
                Kind = LiveStatsQueryFailureKind.AccessDenied,
                Message = ex.Message
            };
            return false;
        } catch (TimeoutException ex) {
            result = new LiveStatsQueryResult();
            failure = new LiveStatsQueryFailure {
                Kind = LiveStatsQueryFailureKind.Timeout,
                Message = ex.Message
            };
            return false;
        } catch (EventLogSessionException ex) {
            result = new LiveStatsQueryResult();
            failure = new LiveStatsQueryFailure {
                Kind = LiveStatsQueryFailureKind.HostUnavailable,
                Message = ex.Message
            };
            return false;
        } catch (EventLogNotFoundException ex) {
            result = new LiveStatsQueryResult();
            failure = new LiveStatsQueryFailure {
                Kind = LiveStatsQueryFailureKind.LogNotFound,
                Message = ex.Message
            };
            return false;
        } catch (Win32Exception ex) {
            result = new LiveStatsQueryResult();
            failure = new LiveStatsQueryFailure {
                Kind = QueryFailureHelpers.Classify(ex) switch {
                    NativeQueryFailureKind.InvalidQuery =>
                        LiveStatsQueryFailureKind.InvalidQuery,
                    NativeQueryFailureKind.LogNotFound =>
                        LiveStatsQueryFailureKind.LogNotFound,
                    NativeQueryFailureKind.AccessDenied =>
                        LiveStatsQueryFailureKind.AccessDenied,
                    NativeQueryFailureKind.Timeout =>
                        LiveStatsQueryFailureKind.Timeout,
                    NativeQueryFailureKind.HostUnavailable =>
                        LiveStatsQueryFailureKind.HostUnavailable,
                    _ =>
                        LiveStatsQueryFailureKind.Exception
                },
                Message = ex.Message
            };
            return false;
        } catch (EventLogException ex) {
            result = new LiveStatsQueryResult();
            failure = new LiveStatsQueryFailure {
                Kind = QueryFailureHelpers.IsInvalidEventQuery(ex)
                    ? LiveStatsQueryFailureKind.InvalidQuery
                    : QueryFailureHelpers.IsTimeoutLike(ex.Message)
                        ? LiveStatsQueryFailureKind.Timeout
                        : LiveStatsQueryFailureKind.Exception,
                Message = ex.Message
            };
            return false;
        } catch (ArgumentException ex) {
            result = new LiveStatsQueryResult();
            failure = new LiveStatsQueryFailure {
                Kind = LiveStatsQueryFailureKind.InvalidArgument,
                Message = ex.Message
            };
            return false;
        } catch (Exception ex) {
            result = new LiveStatsQueryResult();
            failure = new LiveStatsQueryFailure {
                Kind = LiveStatsQueryFailureKind.Exception,
                Message = ex.Message
            };
            return false;
        }
    }

    internal static bool TryNormalizeCreatedTimeUtc(
        DateTime created,
        out DateTime createdUtc) {

        if (created == DateTime.MinValue) {
            createdUtc = default;
            return false;
        }
        createdUtc = created.Kind == DateTimeKind.Utc
            ? created
            : created.ToUniversalTime();
        return true;
    }

    private static bool TryValidateRequest(
        LiveStatsQueryRequest request,
        out LiveStatsQueryResult result,
        out LiveStatsQueryFailure? failure) {
        if (request is null) {
            result = new LiveStatsQueryResult();
            failure = new LiveStatsQueryFailure {
                Kind = LiveStatsQueryFailureKind.InvalidArgument,
                Message = "request is required."
            };
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.LogName)) {
            result = new LiveStatsQueryResult();
            failure = new LiveStatsQueryFailure {
                Kind = LiveStatsQueryFailureKind.InvalidArgument,
                Message = "logName is required."
            };
            return false;
        }

        if (QueryValidationHelpers.IsNegative(request.MaxEventsScanned)) {
            result = new LiveStatsQueryResult();
            failure = new LiveStatsQueryFailure {
                Kind = LiveStatsQueryFailureKind.InvalidArgument,
                Message = "maxEventsScanned must be greater than or equal to 0."
            };
            return false;
        }

        if (QueryValidationHelpers.HasInvalidUtcRange(request.StartTimeUtc, request.EndTimeUtc)) {
            result = new LiveStatsQueryResult();
            failure = new LiveStatsQueryFailure {
                Kind = LiveStatsQueryFailureKind.InvalidArgument,
                Message = "startTimeUtc must be less than or equal to endTimeUtc."
            };
            return false;
        }

        if (request.TopEventIds < 0 || request.TopProviders < 0 || request.TopComputers < 0 || request.TopLevels < 0) {
            result = new LiveStatsQueryResult();
            failure = new LiveStatsQueryFailure {
                Kind = LiveStatsQueryFailureKind.InvalidArgument,
                Message = "Top values must be greater than or equal to 0."
            };
            return false;
        }

        if (QueryValidationHelpers.IsNonPositiveWhenProvided(request.SessionTimeoutMs)) {
            result = new LiveStatsQueryResult();
            failure = new LiveStatsQueryFailure {
                Kind = LiveStatsQueryFailureKind.InvalidArgument,
                Message = "sessionTimeoutMs must be positive when provided."
            };
            return false;
        }

        result = new LiveStatsQueryResult();
        failure = null;
        return true;
    }

    private static bool IsWithinRange(DateTime createdUtc, DateTime? startUtc, DateTime? endUtc) {
        if (startUtc.HasValue && createdUtc < startUtc.Value) {
            return false;
        }
        if (endUtc.HasValue && createdUtc > endUtc.Value) {
            return false;
        }
        return true;
    }

    internal static string BuildEffectiveXPath(string? xpath, DateTime? startUtc, DateTime? endUtc) {
        string baseXPath = string.IsNullOrWhiteSpace(xpath) ? "*" : xpath!.Trim();
        if (!startUtc.HasValue && !endUtc.HasValue) {
            return baseXPath;
        }

        string timeCondition;
        if (startUtc.HasValue && endUtc.HasValue) {
            timeCondition = $"TimeCreated[@SystemTime >= '{FormatUtc(startUtc.Value)}' and @SystemTime <= '{FormatUtc(endUtc.Value)}']";
        } else if (startUtc.HasValue) {
            timeCondition = $"TimeCreated[@SystemTime >= '{FormatUtc(startUtc.Value)}']";
        } else {
            timeCondition = $"TimeCreated[@SystemTime <= '{FormatUtc(endUtc!.Value)}']";
        }

        string timePredicate = $"System[{timeCondition}]";
        return baseXPath == "*"
            ? $"*[{timePredicate}]"
            : $"({baseXPath})[{timePredicate}]";
    }

    private static string FormatUtc(DateTime value) {
        return value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);
    }

}
