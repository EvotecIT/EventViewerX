using System.Runtime.ExceptionServices;

namespace EventViewerX;

public static partial class EventLogBatchEngine {
    /// <summary>
    /// Primes bounded synchronous sources and cancels sibling workers as soon
    /// as one source raises a fatal failure.
    /// </summary>
    internal static T?[] PrimeConcurrently<T>(
        int sourceCount,
        int maxConcurrency,
        CancellationToken cancellationToken,
        Func<int, CancellationToken, T?> prime)
        where T : class, IDisposable {

        if (sourceCount < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(sourceCount));
        }
        ValidateConcurrency(maxConcurrency);
        if (prime == null) {
            throw new ArgumentNullException(nameof(prime));
        }

        var primed = new T?[sourceCount];
        using var fatalCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
        ExceptionDispatchInfo? firstFailure = null;
        int nextSource = -1;
        int workerCount = Math.Min(
            maxConcurrency,
            sourceCount);
        Task[] workers =
            Enumerable
                .Range(0, workerCount)
                .Select(_ => Task.Factory.StartNew(() => {
                    try {
                        while (true) {
                            fatalCancellation.Token
                                .ThrowIfCancellationRequested();
                            int index = Interlocked.Increment(
                                ref nextSource);
                            if (index >= sourceCount) {
                                break;
                            }
                            primed[index] = prime(
                                index,
                                fatalCancellation.Token);
                        }
                    } catch (Exception exception) {
                        Interlocked.CompareExchange(
                            ref firstFailure,
                            ExceptionDispatchInfo.Capture(
                                exception),
                            null);
                        CancelWithoutThrowing(
                            fatalCancellation);
                        throw;
                    }
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default))
                .ToArray();

        try {
            Task.WhenAll(workers)
                .GetAwaiter()
                .GetResult();
            return primed;
        } catch {
            DisposePrimed(primed);
            cancellationToken.ThrowIfCancellationRequested();
            firstFailure?.Throw();
            throw;
        }
    }

    /// <summary>
    /// Primes bounded asynchronous sources and cancels queued or active
    /// siblings as soon as one source raises a fatal failure.
    /// </summary>
    internal static async Task<T?[]> PrimeConcurrentlyAsync<T>(
        int sourceCount,
        int maxConcurrency,
        CancellationToken cancellationToken,
        Func<int, CancellationToken, Task<T?>> prime)
        where T : class, IDisposable {

        if (sourceCount < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(sourceCount));
        }
        ValidateConcurrency(maxConcurrency);
        if (prime == null) {
            throw new ArgumentNullException(nameof(prime));
        }

        var fatalCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
        var concurrencyGate =
            new SemaphoreSlim(
                maxConcurrency,
                maxConcurrency);
        bool cleanupDetached = false;
        ExceptionDispatchInfo? firstFailure = null;
        Task<T?>[] tasks =
            Enumerable
                .Range(0, sourceCount)
                .Select(PrimeOneAsync)
                .ToArray();

        try {
            Task<T?[]> allTasks = Task.WhenAll(tasks);
            if (cancellationToken.CanBeCanceled) {
                var canceled = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                using CancellationTokenRegistration registration =
                    cancellationToken.Register(
                        static state =>
                            ((TaskCompletionSource<bool>)state!)
                                .TrySetResult(true),
                        canceled);
                Task completed = await Task.WhenAny(
                        allTasks,
                        canceled.Task)
                    .ConfigureAwait(false);
                if (!ReferenceEquals(completed, allTasks)) {
                    cleanupDetached = true;
                    _ = allTasks.ContinueWith(
                        _ => {
                            DisposeCompleted(tasks);
                            concurrencyGate.Dispose();
                            fatalCancellation.Dispose();
                        },
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new OperationCanceledException(
                        cancellationToken);
                }
            }
            return await allTasks.ConfigureAwait(false);
        } catch {
            if (!cleanupDetached) {
                DisposeCompleted(tasks);
            }
            cancellationToken.ThrowIfCancellationRequested();
            firstFailure?.Throw();
            throw;
        } finally {
            if (!cleanupDetached) {
                concurrencyGate.Dispose();
                fatalCancellation.Dispose();
            }
        }

        async Task<T?> PrimeOneAsync(int index) {
            bool admitted = false;
            try {
                await concurrencyGate.WaitAsync(
                        fatalCancellation.Token)
                    .ConfigureAwait(false);
                admitted = true;
                return await prime(
                        index,
                        fatalCancellation.Token)
                    .ConfigureAwait(false);
            } catch (Exception exception) {
                Interlocked.CompareExchange(
                    ref firstFailure,
                    ExceptionDispatchInfo.Capture(
                        exception),
                    null);
                CancelWithoutThrowing(
                    fatalCancellation);
                throw;
            } finally {
                if (admitted) {
                    concurrencyGate.Release();
                }
            }
        }
    }

    private static void CancelWithoutThrowing(
        CancellationTokenSource cancellation) {

        try {
            cancellation.Cancel();
        } catch {
            // Preserve the source failure that triggered sibling cancellation.
        }
    }

    private static void DisposePrimed<T>(
        IEnumerable<T?> primed)
        where T : class, IDisposable {

        foreach (T? value in primed) {
            try {
                value?.Dispose();
            } catch {
                // Preserve the source failure that aborted priming.
            }
        }
    }

    private static void DisposeCompleted<T>(
        IEnumerable<Task<T?>> tasks)
        where T : class, IDisposable {

        foreach (Task<T?> task in tasks) {
            if (task.Status ==
                TaskStatus.RanToCompletion) {
                try {
                    task.Result?.Dispose();
                } catch {
                    // Preserve the source failure that aborted priming.
                }
            }
        }
    }
}
