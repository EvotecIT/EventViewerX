using System;
using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;

namespace EventViewerX;

/// <summary>Classifies expected failures while querying a remote Windows Event Log target.</summary>
public enum EventLogRemoteQueryFailureKind {
    /// <summary>No expected remote-target failure was identified.</summary>
    None,
    /// <summary>The caller was not authorized to read the remote event log.</summary>
    AccessDenied,
    /// <summary>The remote operation exceeded its configured timeout.</summary>
    Timeout,
    /// <summary>The remote host or Event Log service could not be reached.</summary>
    HostUnavailable,
    /// <summary>The remote Event Log API returned another target-specific failure.</summary>
    EventLogError,
    /// <summary>The requested event-log channel does not exist on the remote target.</summary>
    LogNotFound
}

/// <summary>Provides a reusable boundary for expected per-target Event Log query failures.</summary>
public static class EventLogRemoteQueryFailureClassifier {
    /// <summary>
    /// Classifies an exception only when it belongs to a non-local target. Caller cancellation and
    /// unexpected programming errors deliberately remain unclassified so callers can propagate them.
    /// </summary>
    /// <param name="machineName">Remote machine supplied to the query.</param>
    /// <param name="exception">Exception raised while opening or reading the target.</param>
    /// <param name="failureKind">Typed failure kind when classification succeeds.</param>
    /// <returns><c>true</c> for an expected remote-target failure; otherwise <c>false</c>.</returns>
    public static bool TryClassify(
        string? machineName,
        Exception exception,
        out EventLogRemoteQueryFailureKind failureKind) {

        if (EventLogTarget.IsLocalMachine(machineName) || exception is OperationCanceledException) {
            failureKind = EventLogRemoteQueryFailureKind.None;
            return false;
        }

        if (exception is EventLogNotFoundException) {
            failureKind = EventLogRemoteQueryFailureKind.LogNotFound;
            return true;
        }

        if (exception is EventLogSessionException sessionException) {
            failureKind = sessionException.Status switch {
                EventLogSessionOpenStatus.AccessDenied => EventLogRemoteQueryFailureKind.AccessDenied,
                EventLogSessionOpenStatus.Timeout => EventLogRemoteQueryFailureKind.Timeout,
                EventLogSessionOpenStatus.NegativeCache or
                EventLogSessionOpenStatus.RpcUnavailable or
                EventLogSessionOpenStatus.EventLogSessionUnavailable => EventLogRemoteQueryFailureKind.HostUnavailable,
                _ => EventLogRemoteQueryFailureKind.EventLogError
            };
            return true;
        }

        if (exception is EventLogException eventLogException &&
            Reports.QueryHelpers.QueryFailureHelpers.IsInvalidEventQuery(eventLogException)) {
            failureKind = EventLogRemoteQueryFailureKind.None;
            return false;
        }

        if (exception is Win32Exception win32Exception) {
            failureKind = win32Exception.NativeErrorCode switch {
                15001 => EventLogRemoteQueryFailureKind.None,
                15000 or 15007 => EventLogRemoteQueryFailureKind.LogNotFound,
                5 => EventLogRemoteQueryFailureKind.AccessDenied,
                121 or 1460 => EventLogRemoteQueryFailureKind.Timeout,
                53 or 64 or 1231 or 1722 or 1726 or 1818 =>
                    EventLogRemoteQueryFailureKind.HostUnavailable,
                _ => EventLogRemoteQueryFailureKind.EventLogError
            };
            return failureKind != EventLogRemoteQueryFailureKind.None;
        }

        failureKind = exception switch {
            UnauthorizedAccessException => EventLogRemoteQueryFailureKind.AccessDenied,
            TimeoutException => EventLogRemoteQueryFailureKind.Timeout,
            EventLogException => EventLogRemoteQueryFailureKind.EventLogError,
            _ => EventLogRemoteQueryFailureKind.None
        };
        return failureKind != EventLogRemoteQueryFailureKind.None;
    }
}
