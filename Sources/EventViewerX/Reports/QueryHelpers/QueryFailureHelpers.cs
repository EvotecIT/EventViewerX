using System;
using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;

namespace EventViewerX.Reports.QueryHelpers;

internal enum NativeQueryFailureKind {
    InvalidQuery,
    LogNotFound,
    AccessDenied,
    Timeout,
    HostUnavailable,
    Exception
}

internal static class QueryFailureHelpers {
    internal static bool IsTimeoutLike(string? message) {
        if (string.IsNullOrWhiteSpace(message)) {
            return false;
        }

        var text = message!;
        return text.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0 ||
               text.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    internal static bool IsInvalidEventQuery(EventLogException exception) {
        const int ErrorEvtInvalidQuery = 15001;
        return (exception.HResult & 0xffff) == ErrorEvtInvalidQuery ||
               exception.Message.IndexOf("query is invalid", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    internal static NativeQueryFailureKind Classify(
        Win32Exception exception) {

        return exception.NativeErrorCode switch {
            15000 or 15007 =>
                NativeQueryFailureKind.LogNotFound,
            15001 or 15027 =>
                NativeQueryFailureKind.InvalidQuery,
            5 =>
                NativeQueryFailureKind.AccessDenied,
            121 or 1460 =>
                NativeQueryFailureKind.Timeout,
            53 or 64 or 1231 or 1722 or
            1726 or 1818 =>
                NativeQueryFailureKind.HostUnavailable,
            _ =>
                NativeQueryFailureKind.Exception
        };
    }
}
