using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Net;
using EventViewerX.Native;

namespace EventViewerX;

/// <summary>
/// Fast, budgeted health probe over the same owned native reader used by
/// <see cref="EventLogEngine"/>.
/// </summary>
public static class EventLogProbe {
    private const int QuickProbeReadTimeoutMs = 750;
    private const int ErrorEvtInvalidChannelPath = 15000;
    private const int ErrorEvtInvalidQuery = 15001;
    private const int ErrorEvtChannelNotFound = 15007;
    private const int ErrorEvtMalformedXmlText = 15008;

    /// <summary>
    /// Reads the newest matching event from a local or remote channel within a
    /// bounded time budget using the owned native engine.
    /// </summary>
    public static EventLogProbeResult ProbeLatestEvent(
        string logName,
        string? xpathFilter = null,
        string? machineName = null,
        TimeSpan? timeout = null,
        int maxEventsToScan = 4096,
        NetworkCredential? credential = null,
        EventLogAuthentication authentication =
            EventLogAuthentication.Default,
        CancellationToken cancellationToken = default) {

        ValidateArguments(
            logName,
            timeout,
            maxEventsToScan);
        cancellationToken.ThrowIfCancellationRequested();
        if (EventLogTarget.IsLocalMachine(
                machineName) &&
            credential != null) {
            throw new ArgumentException(
                "Credentials can only be used with a remote event-log probe.",
                nameof(credential));
        }

        TimeSpan effectiveTimeout =
            timeout ??
            TimeSpan.FromSeconds(15);
        int timeoutMilliseconds =
            checked((int)Math.Min(
                int.MaxValue,
                Math.Max(
                    1,
                    effectiveTimeout.TotalMilliseconds)));
        var stopwatch = Stopwatch.StartNew();
        string target =
            ResolveProbeTarget(machineName);
        int scanned = 0;
        bool scanLimitReached = false;
        long? recordCount = null;

        using var probeCancellation =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken);
        probeCancellation.CancelAfter(
            effectiveTimeout);
        try {
            var query =
                new EventLogChannelQuery(logName) {
                    MachineName = machineName,
                    Credential = credential,
                    Authentication =
                        authentication,
                    XPath =
                        string.IsNullOrWhiteSpace(
                            xpathFilter)
                            ? "*"
                            : xpathFilter!,
                    Oldest = false,
                    ReadMode =
                        EventReadMode.Metadata,
                    MaxEvents =
                        checked(
                            (long)maxEventsToScan +
                            1),
                    RemoteConnectionTimeoutMilliseconds =
                        timeoutMilliseconds,
                    RemoteReadTimeoutMilliseconds =
                        Math.Min(
                            timeoutMilliseconds,
                            QuickProbeReadTimeoutMs),
                    BufferCapacity = 1,
                    RpcEndpointPort =
                        Settings.RpcProbePort
                };

            DateTime? eventTimeUtc =
                FindFirstUsableTimestampUtc(
                    EventLogEngine.ReadChannel(
                        query,
                        probeCancellation.Token),
                    maxEventsToScan,
                    out scanned,
                    out scanLimitReached);

            recordCount = TryRunOptionalRecordCountStage(
                () => TryReadRecordCount(
                    logName,
                    machineName,
                    credential,
                    authentication,
                    effectiveTimeout -
                    stopwatch.Elapsed,
                    probeCancellation.Token),
                effectiveTimeout -
                stopwatch.Elapsed,
                probeCancellation.Token,
                cancellationToken);
            if (eventTimeUtc.HasValue) {
                return new EventLogProbeResult(
                    logName,
                    target,
                    eventTimeUtc.Value,
                    EventLogProbeStatus.Ok,
                    null,
                    scanned,
                    recordCount,
                    stopwatch.Elapsed,
                    nativeQueryVerified: true);
            }
            EventLogProbeStatus status =
                scanned == 0
                    ? EventLogProbeStatus.NoEvent
                    : scanLimitReached
                        ? EventLogProbeStatus.LimitReached
                        : EventLogProbeStatus.NoUsableTimestamp;
            return ProbeFailure(
                logName,
                target,
                status,
                scanned == 0
                    ? "No event matched the native query."
                    : scanLimitReached
                        ? $"The first {scanned} matching events did not contain a usable timestamp and additional matches remain."
                        : $"All {scanned} matching events were scanned, but none contained a usable timestamp.",
                scanned,
                recordCount,
                stopwatch.Elapsed,
                nativeQueryVerified: true);
        } catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested) {
            throw;
        } catch (OperationCanceledException) {
            return ProbeFailure(
                logName,
                target,
                EventLogProbeStatus.Timeout,
                $"Timed out after {effectiveTimeout.TotalMilliseconds:F0} ms.",
                scanned,
                recordCount,
                stopwatch.Elapsed);
        } catch (Exception exception) {
            EventLogProbeStatus status =
                ClassifyFailure(
                    machineName,
                    exception);
            return ProbeFailure(
                logName,
                target,
                status,
                exception.Message,
                scanned,
                recordCount,
                stopwatch.Elapsed);
        }
    }

    internal static DateTime? FindFirstUsableTimestampUtc(
        IEnumerable<EventObject> events,
        int maxEventsToScan,
        out int scanned,
        out bool limitReached) {

        if (maxEventsToScan <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(maxEventsToScan),
                "Maximum events to scan must be positive.");
        }
        scanned = 0;
        limitReached = false;
        foreach (EventObject eventObject in events) {
            if (scanned >= maxEventsToScan) {
                limitReached = true;
                break;
            }
            scanned++;
            if (eventObject.TimeCreated ==
                DateTime.MinValue) {
                continue;
            }
            return eventObject.TimeCreated
                .ToUniversalTime();
        }
        return null;
    }

    internal static T RunCancelableProbeStage<T>(
        Func<T> stage,
        TimeSpan remaining,
        CancellationToken cancellationToken) {

        if (stage == null) {
            throw new ArgumentNullException(
                nameof(stage));
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (remaining <= TimeSpan.Zero) {
            throw new TimeoutException(
                "The probe deadline was reached before the optional stage started.");
        }
        Task<T> operation = Task.Run(stage);
        Task completed = Task.WhenAny(
                operation,
                Task.Delay(
                    remaining,
                    cancellationToken))
            .GetAwaiter()
            .GetResult();
        if (!ReferenceEquals(
                completed,
                operation)) {
            _ = operation.ContinueWith(
                static failed => {
                    _ = failed.Exception;
                },
                CancellationToken.None,
                TaskContinuationOptions
                    .OnlyOnFaulted |
                TaskContinuationOptions
                    .ExecuteSynchronously,
                TaskScheduler.Default);
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException(
                "The probe deadline was reached during the optional stage.");
        }
        T result = operation
            .GetAwaiter()
            .GetResult();
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    internal static long? TryRunOptionalRecordCountStage(
        Func<long?> stage,
        TimeSpan remaining,
        CancellationToken probeCancellationToken,
        CancellationToken callerCancellationToken) {

        try {
            return RunCancelableProbeStage(
                stage,
                remaining,
                probeCancellationToken);
        } catch (OperationCanceledException)
            when (!callerCancellationToken
                .IsCancellationRequested) {
            return null;
        } catch (TimeoutException) {
            callerCancellationToken
                .ThrowIfCancellationRequested();
            return null;
        }
    }

    internal static long? TryReadRecordCount(
        string logName,
        string? machineName,
        NetworkCredential? credential,
        EventLogAuthentication authentication,
        TimeSpan remaining,
        CancellationToken cancellationToken,
        Func<EventLogSession>? localSessionFactory = null,
        Func<EventLogSession, EventLogInformation>?
            informationFactory = null) {

        if (remaining <= TimeSpan.Zero) {
            return null;
        }
        var stopwatch = Stopwatch.StartNew();
        try {
            cancellationToken.ThrowIfCancellationRequested();
            int sessionBudget =
                RemainingMilliseconds(
                    remaining,
                    stopwatch.Elapsed);
            EventLogSessionOpenResult? sessionResult =
                EventLogSessionManager
                    .CreateSessionResult(
                        machineName,
                        "ProbeRecordCount",
                        logName,
                        sessionBudget,
                        emitDiagnostics: false,
                        credential: credential,
                        authentication:
                            authentication,
                        cancellationToken:
                            cancellationToken,
                        localSessionFactory:
                            localSessionFactory);
            try {
                cancellationToken.ThrowIfCancellationRequested();
                if (!sessionResult.Success ||
                    sessionResult.Session == null) {
                    return null;
                }
                using var sessionLifetime =
                    new RetainedDisposable<EventLogSessionOpenResult>(
                        sessionResult);
                sessionResult = null;
                int informationBudget =
                    RemainingMilliseconds(
                        remaining,
                        stopwatch.Elapsed);
                EventLogInformation information =
                    EventLogNativeOperation.Execute(
                        () => informationFactory == null
                            ? sessionLifetime.Value.Session!
                                .GetLogInformation(
                                    logName,
                                    PathType.LogName)
                            : informationFactory(
                                sessionLifetime.Value.Session!),
                        informationBudget,
                        $"Timed out reading the record count for '{logName}' after {informationBudget} ms.",
                        cancellationToken,
                        operationLease:
                            sessionLifetime.Retain());
                cancellationToken.ThrowIfCancellationRequested();
                return information.RecordCount;
            } finally {
                sessionResult?.Dispose();
            }
        } catch (OperationCanceledException) {
            throw;
        } catch {
            return null;
        }
    }

    private static int RemainingMilliseconds(
        TimeSpan budget,
        TimeSpan elapsed) {

        TimeSpan remaining = budget - elapsed;
        if (remaining <= TimeSpan.Zero) {
            throw new TimeoutException(
                "The probe deadline was reached.");
        }
        return checked((int)Math.Min(
            int.MaxValue,
            Math.Max(
                1,
                remaining.TotalMilliseconds)));
    }

    internal static EventLogProbeStatus ClassifyFailure(
        string? machineName,
        Exception exception) {

        if (exception is Win32Exception win32) {
            return win32.NativeErrorCode switch {
                ErrorEvtInvalidChannelPath or
                ErrorEvtChannelNotFound =>
                    EventLogProbeStatus.LogNotFound,
                ErrorEvtInvalidQuery or
                ErrorEvtMalformedXmlText =>
                    EventLogProbeStatus.InvalidQuery,
                5 => EventLogProbeStatus.AccessDenied,
                121 or 1460 =>
                    EventLogProbeStatus.Timeout,
                53 or 64 or 1231 or 1722 or
                1726 or 1818 =>
                    EventLogProbeStatus.HostUnavailable,
                _ => EventLogProbeStatus.Error
            };
        }
        if (EventLogRemoteQueryFailureClassifier
            .TryClassify(
                machineName,
                exception,
                out EventLogRemoteQueryFailureKind kind)) {
            return kind switch {
                EventLogRemoteQueryFailureKind.AccessDenied =>
                    EventLogProbeStatus.AccessDenied,
                EventLogRemoteQueryFailureKind.Timeout =>
                    EventLogProbeStatus.Timeout,
                EventLogRemoteQueryFailureKind.HostUnavailable =>
                    EventLogProbeStatus.HostUnavailable,
                _ => EventLogProbeStatus.Error
            };
        }
        return exception switch {
            EventLogNotFoundException =>
                EventLogProbeStatus.LogNotFound,
            EventLogException eventLogException
                when Reports.QueryHelpers.QueryFailureHelpers.IsInvalidEventQuery(eventLogException) =>
                EventLogProbeStatus.InvalidQuery,
            UnauthorizedAccessException =>
                EventLogProbeStatus.AccessDenied,
            TimeoutException =>
                EventLogProbeStatus.Timeout,
            _ => EventLogProbeStatus.Error
        };
    }

    private static void ValidateArguments(
        string logName,
        TimeSpan? timeout,
        int maxEventsToScan) {

        if (string.IsNullOrWhiteSpace(
                logName)) {
            throw new ArgumentException(
                "Log name cannot be null or whitespace.",
                nameof(logName));
        }
        if (timeout.HasValue &&
            timeout.Value <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                "Timeout must be positive when provided.");
        }
        if (maxEventsToScan <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(maxEventsToScan),
                "Maximum events to scan must be positive.");
        }
    }

    private static string ResolveProbeTarget(
        string? machineName) {

        return EventLogTarget.IsLocalMachine(
            machineName)
            ? EventLogTarget.LocalMachineName
            : machineName!.Trim();
    }

    private static EventLogProbeResult ProbeFailure(
        string logName,
        string target,
        EventLogProbeStatus status,
        string message,
        int scanned,
        long? recordCount,
        TimeSpan duration,
        bool nativeQueryVerified = false) {

        return new EventLogProbeResult(
            logName,
            target,
            null,
            status,
            message,
            scanned,
            recordCount,
            duration,
            nativeQueryVerified);
    }
}
