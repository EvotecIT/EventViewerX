using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace EventViewerX.Native;

internal static class WindowsEventRemoteReader {
    internal static IEnumerable<EventObject> Read(
        string machineName,
        string? path,
        string query,
        string displayName,
        string containerLog,
        WindowsEventNativeMethods.QueryFlags flags,
        EventReadMode readMode,
        int messageLocale,
        int fallbackMessageLocale,
        bool includeBookmark,
        long maxEvents,
        int connectionTimeoutMilliseconds,
        int readTimeoutMilliseconds,
        int bufferCapacity,
        int rpcEndpointPort,
        NetworkCredential? credential,
        EventLogAuthentication authentication,
        string? bookmarkXml,
        long bookmarkOffset,
        bool strictBookmark,
        Action<EventLogQueryFailure>? failureHandler,
        CancellationToken cancellationToken) {

        return ReadIterator(
            machineName,
            path,
            query,
            displayName,
            containerLog,
            flags,
            readMode,
            messageLocale,
            fallbackMessageLocale,
            includeBookmark,
            maxEvents,
            connectionTimeoutMilliseconds,
            readTimeoutMilliseconds,
            bufferCapacity,
            rpcEndpointPort,
            credential,
            authentication,
            bookmarkXml,
            bookmarkOffset,
            strictBookmark,
            failureHandler,
            cancellationToken);
    }

    private static IEnumerable<EventObject> ReadIterator(
        string machineName,
        string? path,
        string query,
        string displayName,
        string containerLog,
        WindowsEventNativeMethods.QueryFlags flags,
        EventReadMode readMode,
        int messageLocale,
        int fallbackMessageLocale,
        bool includeBookmark,
        long maxEvents,
        int connectionTimeoutMilliseconds,
        int readTimeoutMilliseconds,
        int bufferCapacity,
        int rpcEndpointPort,
        NetworkCredential? credential,
        EventLogAuthentication authentication,
        string? bookmarkXml,
        long bookmarkOffset,
        bool strictBookmark,
        Action<EventLogQueryFailure>? failureHandler,
        CancellationToken cancellationToken) {

        string connectionTimeoutMessage =
            $"Timed out connecting to '{displayName}' on '{machineName}' after {connectionTimeoutMilliseconds} ms.";
        string readTimeoutMessage =
            $"Timed out reading '{displayName}' on '{machineName}' after {readTimeoutMilliseconds} ms without progress.";
        var connectionBudget = Stopwatch.StartNew();
        if (EventLogSessionManager
            .TryGetHostNegativeCacheExpiry(
                machineName,
                out DateTime cachedUntilUtc)) {
            throw new System.ComponentModel.Win32Exception(
                1722,
                $"Host '{machineName}' is temporarily cached as unreachable until {cachedUntilUtc:u}.");
        }
        RpcEndpointProbeStatus rpcStatus =
            RpcEndpointProbe.Probe(
                machineName,
                rpcEndpointPort,
                GetRemainingConnectionTimeout(
                    connectionBudget,
                    connectionTimeoutMilliseconds,
                    connectionTimeoutMessage),
                cancellationToken);
        EnsureRpcEndpointAvailable(
            machineName,
            rpcEndpointPort,
            connectionTimeoutMilliseconds,
            rpcStatus);
        IDisposable operationSlot = BoundedNativeOperation.Acquire(
            GetRemainingConnectionTimeout(
                connectionBudget,
                connectionTimeoutMilliseconds,
                connectionTimeoutMessage),
            connectionTimeoutMessage,
            cancellationToken);
        int sessionConnectionTimeout;
        try {
            sessionConnectionTimeout =
                GetRemainingConnectionTimeout(
                    connectionBudget,
                    connectionTimeoutMilliseconds,
                    connectionTimeoutMessage);
        } catch {
            operationSlot.Dispose();
            throw;
        }
        var results = new BlockingCollection<EventObject>(bufferCapacity);
        var failures = new ConcurrentQueue<Exception>();
        var queryFailures = new ConcurrentQueue<EventLogQueryFailure>();
        var sessionOpened = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var workerCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task producer;
        try {
            var completion = new TaskCompletionSource<object?>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() => {
                try {
                    Produce(
                        machineName,
                        path,
                        query,
                        displayName,
                        containerLog,
                        flags,
                        readMode,
                        messageLocale,
                        fallbackMessageLocale,
                        includeBookmark,
                        maxEvents,
                        readTimeoutMilliseconds,
                        results,
                        failures,
                        sessionOpened,
                        operationSlot,
                        credential,
                        authentication,
                        sessionConnectionTimeout,
                        bookmarkXml,
                        bookmarkOffset,
                        strictBookmark,
                        failureHandler == null
                            ? null
                            : queryFailures.Enqueue,
                        workerCancellation.Token);
                    completion.TrySetResult(null);
                } catch (Exception ex) {
                    completion.TrySetException(ex);
                }
            }) {
                IsBackground = true,
                Name = $"EventViewerX remote reader: {machineName}"
            };
            thread.Start();
            producer = completion.Task;
        } catch {
            operationSlot.Dispose();
            results.Dispose();
            throw;
        }

        var inactivity = Stopwatch.StartNew();
        bool observedOpenSession = false;
        try {
            while (true) {
                cancellationToken.ThrowIfCancellationRequested();
                DrainQueryFailures(queryFailures, failureHandler);
                if (results.TryTake(
                        out EventObject? eventObject,
                        100,
                        cancellationToken)) {
                    inactivity.Restart();
                    observedOpenSession = true;
                    yield return eventObject;
                    continue;
                }
                if (results.IsCompleted) {
                    break;
                }
                if (!observedOpenSession &&
                    sessionOpened.Task.Status == TaskStatus.RanToCompletion) {
                    observedOpenSession = true;
                    inactivity.Restart();
                }
                bool connectionTimedOut =
                    !observedOpenSession &&
                    connectionBudget.ElapsedMilliseconds >=
                    connectionTimeoutMilliseconds;
                bool readTimedOut =
                    observedOpenSession &&
                    readTimeoutMilliseconds > 0 &&
                    inactivity.ElapsedMilliseconds >=
                    readTimeoutMilliseconds;
                if (connectionTimedOut || readTimedOut) {
                    workerCancellation.Cancel();
                    throw new TimeoutException(observedOpenSession
                        ? readTimeoutMessage
                        : connectionTimeoutMessage);
                }
            }

            producer.GetAwaiter().GetResult();
            DrainQueryFailures(queryFailures, failureHandler);
            if (failures.TryDequeue(out Exception? failure)) {
                throw failure;
            }
        } finally {
            workerCancellation.Cancel();
            if (producer.IsCompleted) {
                results.Dispose();
            } else {
                _ = producer.ContinueWith(
                    completed => {
                        _ = completed.Exception;
                        results.Dispose();
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }
    }

    private static void Produce(
        string machineName,
        string? path,
        string query,
        string displayName,
        string containerLog,
        WindowsEventNativeMethods.QueryFlags flags,
        EventReadMode readMode,
        int messageLocale,
        int fallbackMessageLocale,
        bool includeBookmark,
        long maxEvents,
        int readTimeoutMilliseconds,
        BlockingCollection<EventObject> results,
        ConcurrentQueue<Exception> failures,
        TaskCompletionSource<object?> sessionOpened,
        IDisposable operationSlot,
        NetworkCredential? credential,
        EventLogAuthentication authentication,
        int connectionTimeoutMilliseconds,
        string? bookmarkXml,
        long bookmarkOffset,
        bool strictBookmark,
        Action<EventLogQueryFailure>? failureHandler,
        CancellationToken cancellationToken) {

        IDisposable? setupSlot = operationSlot;
        try {
            try {
                using WindowsEventNativeMethods.EventHandle session =
                    WindowsEventRemoteSession.Open(
                        machineName,
                        credential,
                        authentication,
                        connectionTimeoutMilliseconds);
                EventLogSessionManager.ClearNegativeCache(
                    machineName);
                var nativeQuery = new NativeEventQuery(
                    session.DangerousGetHandle(),
                    path,
                    query,
                    flags,
                    $"{displayName} on {machineName}",
                    messageLocale: messageLocale,
                    fallbackMessageLocale: fallbackMessageLocale,
                    nextTimeoutMilliseconds: readTimeoutMilliseconds,
                    includeBookmark: includeBookmark,
                    bookmarkXml: bookmarkXml,
                    bookmarkOffset: bookmarkOffset,
                    strictBookmark: strictBookmark,
                    machineName: machineName,
                    failureHandler: failureHandler);
                using IEnumerator<EventObject> enumerator =
                    WindowsEventReader.Read(
                            nativeQuery,
                            readMode,
                            machineName,
                            containerLog,
                            cancellationToken,
                            () => sessionOpened.TrySetResult(null))
                        .GetEnumerator();
                bool hasFirst = enumerator.MoveNext();
                setupSlot.Dispose();
                setupSlot = null;
                if (hasFirst) {
                    results.Add(
                        enumerator.Current,
                        cancellationToken);
                    if (maxEvents == 0 || maxEvents > 1) {
                        CopyToBuffer(
                            enumerator,
                            results,
                            maxEvents > 0
                                ? maxEvents - 1
                                : 0,
                            readTimeoutMilliseconds,
                            $"Timed out waiting for an available native read slot for '{displayName}' on '{machineName}'.",
                            cancellationToken);
                    }
                }
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            } catch (Exception ex) {
                failures.Enqueue(ex);
            } finally {
                results.CompleteAdding();
            }
        } finally {
            setupSlot?.Dispose();
        }
    }

    internal static void CopyToBuffer(
        IEnumerator<EventObject> enumerator,
        BlockingCollection<EventObject> results,
        long maxEvents,
        int readTimeoutMilliseconds,
        string slotTimeoutMessage,
        CancellationToken cancellationToken) {

        long returned = 0;
        while (true) {
            cancellationToken.ThrowIfCancellationRequested();
            bool hasNext;
            using (readTimeoutMilliseconds > 0
                       ? BoundedNativeOperation.Acquire(
                           readTimeoutMilliseconds,
                           slotTimeoutMessage,
                           cancellationToken)
                       : BoundedNativeOperation.Acquire(
                           cancellationToken)) {
                hasNext = enumerator.MoveNext();
            }
            if (!hasNext) {
                break;
            }

            results.Add(
                enumerator.Current,
                cancellationToken);
            returned++;
            if (maxEvents > 0 && returned >= maxEvents) {
                break;
            }
        }
    }

    private static void DrainQueryFailures(
        ConcurrentQueue<EventLogQueryFailure> failures,
        Action<EventLogQueryFailure>? failureHandler) {

        if (failureHandler == null) {
            return;
        }
        while (failures.TryDequeue(
                   out EventLogQueryFailure? failure)) {
            failureHandler(failure);
        }
    }

    internal static void EnsureRpcEndpointAvailable(
        string machineName,
        int rpcEndpointPort,
        int connectionTimeoutMilliseconds,
        RpcEndpointProbeStatus status) {

        if (status == RpcEndpointProbeStatus.Connected) {
            return;
        }
        if (status == RpcEndpointProbeStatus.TimedOut) {
            throw new TimeoutException(
                $"Timed out probing RPC on '{machineName}' after {connectionTimeoutMilliseconds} ms.");
        }
        EventLogSessionManager.MarkHostUnreachable(
            machineName);
        throw new System.ComponentModel.Win32Exception(
            1722,
            $"RPC preflight to '{machineName}' on port {rpcEndpointPort} failed within {connectionTimeoutMilliseconds} ms.");
    }

    internal static int GetRemainingConnectionTimeout(
        Stopwatch budget,
        int timeoutMilliseconds,
        string timeoutMessage) {

        int elapsed = (int)Math.Min(
            budget.ElapsedMilliseconds,
            timeoutMilliseconds);
        int remaining = timeoutMilliseconds - elapsed;
        if (remaining <= 0) {
            throw new TimeoutException(timeoutMessage);
        }
        return remaining;
    }
}
