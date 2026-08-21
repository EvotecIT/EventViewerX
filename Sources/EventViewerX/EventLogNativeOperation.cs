using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Threading;
using EventViewerX.Native;

namespace EventViewerX;

internal static class EventLogNativeOperation {
    internal static T Execute<T>(
        Func<T> operation,
        int timeoutMilliseconds,
        string timeoutMessage,
        Action<T>? lateResultCleanup = null,
        IDisposable? operationLease = null) {

        return BoundedNativeOperation.Execute(
            operation,
            timeoutMilliseconds,
            timeoutMessage,
            lateResultCleanup,
            operationLease);
    }

    internal static T Execute<T>(
        Func<T> operation,
        int timeoutMilliseconds,
        string timeoutMessage,
        CancellationToken cancellationToken,
        Action<T>? lateResultCleanup = null,
        IDisposable? operationLease = null) {

        return BoundedNativeOperation.Execute(
            operation,
            timeoutMilliseconds,
            timeoutMessage,
            cancellationToken,
            lateResultCleanup,
            operationLease);
    }

    internal static EventRecord? ReadEvent(
        EventLogReader reader,
        int timeoutMilliseconds,
        string operation) {

        if (timeoutMilliseconds <= 0) {
            return reader.ReadEvent();
        }

        TimeSpan timeout =
            TimeSpan.FromMilliseconds(timeoutMilliseconds);
        var stopwatch = Stopwatch.StartNew();
        EventRecord? record = reader.ReadEvent(timeout);
        if (record == null &&
            stopwatch.Elapsed.Ticks >=
            timeout.Ticks * 9 / 10) {
            throw new TimeoutException(
                $"{operation} exceeded its {timeoutMilliseconds} ms native read window.");
        }
        return record;
    }
}
