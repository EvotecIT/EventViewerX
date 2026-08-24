using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace EventViewerX.Native;

internal static class BoundedNativeOperation {
    internal const int MaximumConcurrentOperations = 16;
    private static readonly SemaphoreSlim Slots = new(
        MaximumConcurrentOperations,
        MaximumConcurrentOperations);

    internal static IDisposable Acquire(
        int timeoutMilliseconds,
        string timeoutMessage) {

        return Acquire(
            timeoutMilliseconds,
            timeoutMessage,
            CancellationToken.None);
    }

    internal static IDisposable Acquire(
        CancellationToken cancellationToken) {

        cancellationToken.ThrowIfCancellationRequested();
        Slots.Wait(cancellationToken);
        return new SlotLease();
    }

    internal static IDisposable Acquire(
        int timeoutMilliseconds,
        string timeoutMessage,
        CancellationToken cancellationToken) {

        cancellationToken.ThrowIfCancellationRequested();
        if (!Slots.Wait(
                timeoutMilliseconds,
                cancellationToken)) {
            throw new BoundedNativeOperationAdmissionTimeoutException(
                timeoutMessage);
        }
        return new SlotLease();
    }

    internal static T Execute<T>(
        Func<T> operation,
        int timeoutMilliseconds,
        string timeoutMessage,
        Action<T>? lateResultCleanup = null,
        IDisposable? operationLease = null) {

        return Execute(
            operation,
            timeoutMilliseconds,
            timeoutMessage,
            CancellationToken.None,
            lateResultCleanup,
            operationLease);
    }

    internal static T Execute<T>(
        Func<T> operation,
        int timeoutMilliseconds,
        string timeoutMessage,
        CancellationToken cancellationToken,
        Action<T>? lateResultCleanup = null,
        IDisposable? operationLease = null,
        Action? operationAccepted = null) {

        try {
            cancellationToken.ThrowIfCancellationRequested();
        } catch {
            operationLease?.Dispose();
            throw;
        }
        if (timeoutMilliseconds <= 0) {
            using (operationLease) {
                return operation();
            }
        }

        var timeoutBudget = Stopwatch.StartNew();
        try {
            if (!Slots.Wait(
                    timeoutMilliseconds,
                    cancellationToken)) {
                throw new BoundedNativeOperationAdmissionTimeoutException(
                    timeoutMessage);
            }
        } catch {
            operationLease?.Dispose();
            throw;
        }

        int remainingTimeout = timeoutMilliseconds -
            (int)Math.Min(timeoutBudget.ElapsedMilliseconds, timeoutMilliseconds);
        if (remainingTimeout <= 0) {
            Slots.Release();
            operationLease?.Dispose();
            throw new BoundedNativeOperationAdmissionTimeoutException(
                timeoutMessage);
        }

        Task<T> task;
        long completedAfterMilliseconds = long.MaxValue;
        try {
            var completion = new TaskCompletionSource<T>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() => {
                try {
                    T result = operation();
                    Volatile.Write(
                        ref completedAfterMilliseconds,
                        timeoutBudget.ElapsedMilliseconds);
                    completion.TrySetResult(result);
                } catch (Exception ex) {
                    Volatile.Write(
                        ref completedAfterMilliseconds,
                        timeoutBudget.ElapsedMilliseconds);
                    completion.TrySetException(ex);
                } finally {
                    operationLease?.Dispose();
                    Slots.Release();
                }
            }) {
                IsBackground = true,
                Name = "EventViewerX bounded native operation"
            };
            thread.Start();
            task = completion.Task;
        } catch {
            Slots.Release();
            operationLease?.Dispose();
            throw;
        }
        try {
            operationAccepted?.Invoke();
        } catch {
            ObserveLateResult(
                task,
                lateResultCleanup);
            throw;
        }

        bool completed;
        try {
            completed = task.Wait(
                remainingTimeout,
                cancellationToken);
        } catch (AggregateException) {
            return task.GetAwaiter().GetResult();
        } catch (OperationCanceledException) {
            ObserveLateResult(
                task,
                lateResultCleanup);
            throw;
        }

        if (completed && Volatile.Read(ref completedAfterMilliseconds) <= timeoutMilliseconds) {
            cancellationToken.ThrowIfCancellationRequested();
            return task.GetAwaiter().GetResult();
        }

        ObserveLateResult(
            task,
            lateResultCleanup);
        throw new TimeoutException(timeoutMessage);
    }

    private static void ObserveLateResult<T>(
        Task<T> task,
        Action<T>? lateResultCleanup) {

        _ = task.ContinueWith(
            completedTask => {
                if (completedTask.Status == TaskStatus.RanToCompletion) {
                    try {
                        lateResultCleanup?.Invoke(completedTask.Result);
                    } catch {
                    }
                } else if (completedTask.IsFaulted) {
                    _ = completedTask.Exception;
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private sealed class SlotLease : IDisposable {
        private int _released;

        public void Dispose() {
            if (Interlocked.Exchange(ref _released, 1) == 0) {
                Slots.Release();
            }
        }
    }
}
