using System.Runtime.CompilerServices;

namespace EventViewerX;

public static partial class EventLogBatchEngine {
    /// <summary>
    /// Asynchronously reads a bounded deterministic merge. Independent sources are primed concurrently,
    /// while consumer backpressure keeps only one detached head record per source in merge memory.
    /// </summary>
    public static IAsyncEnumerable<EventObject> ReadAsync(
        EventLogBatchQuery query,
        CancellationToken cancellationToken = default) {

        return ReadAsynchronously(
            CreateExecutionPlan(query),
            cancellationToken);
    }

    private static async IAsyncEnumerable<EventObject>
        ReadAsynchronously(
            EventLogBatchExecutionPlan plan,
            [EnumeratorCancellation]
            CancellationToken cancellationToken) {

        EventSourceCursor?[] primed =
            await PrimeConcurrentlyAsync<EventSourceCursor>(
                    plan.Sources.Length,
                    plan.MaxConcurrency,
                    cancellationToken,
                    (index, primingToken) =>
                        PrimeSourceAsync(
                            index,
                            plan.Sources[index],
                            plan.ContinueOnError,
                            plan.FailureHandler,
                            primingToken))
                .ConfigureAwait(false);
        var cursors = primed
            .Where(static cursor => cursor != null)
            .Cast<EventSourceCursor>()
            .ToList();
        var queue = new SortedSet<EventSourceCursor>(
            new EventSourceCursorComparer(plan.Oldest));
        foreach (EventSourceCursor cursor in cursors) {
            queue.Add(cursor);
        }

        long returned = 0;
        try {
            while (queue.Count > 0) {
                cancellationToken.ThrowIfCancellationRequested();
                EventSourceCursor cursor = queue.Min!;
                queue.Remove(cursor);
                yield return cursor.Current;
                returned++;
                if (plan.MaxEvents > 0 &&
                    returned >= plan.MaxEvents) {
                    yield break;
                }

                Task<bool> moveNext = Task.Run(
                    () => TryMoveNext(
                        cursor,
                        plan.ContinueOnError,
                        plan.FailureHandler,
                        cancellationToken),
                    CancellationToken.None);
                (bool completed, bool hasNext) =
                    await AwaitMoveNextAsync(
                            moveNext,
                            cursor,
                            cancellationToken)
                        .ConfigureAwait(false);
                if (!completed) {
                    cursors.Remove(cursor);
                    cancellationToken
                        .ThrowIfCancellationRequested();
                    throw new OperationCanceledException(
                        cancellationToken);
                }
                if (hasNext) {
                    queue.Add(cursor);
                }
            }
        } finally {
            foreach (EventSourceCursor cursor in cursors) {
                cursor.Dispose();
            }
        }
    }

    private static async Task<EventSourceCursor?> PrimeSourceAsync(
        int index,
        EventSourceSnapshot source,
        bool continueOnError,
        Action<EventLogQueryFailure>? failureHandler,
        CancellationToken cancellationToken) {

        return await Task.Run(() => {
            EventSourceCursor? cursor = TryOpenCursor(
                index,
                source,
                continueOnError,
                failureHandler,
                cancellationToken);
            if (cursor == null) {
                return null;
            }
            try {
                if (TryMoveNext(
                        cursor,
                        continueOnError,
                        failureHandler,
                        cancellationToken)) {
                    EventSourceCursor result = cursor;
                    cursor = null;
                    return result;
                }
                return null;
            } finally {
                cursor?.Dispose();
            }
        }, CancellationToken.None).ConfigureAwait(false);
    }

    internal static async Task<(bool Completed, bool HasNext)>
        AwaitMoveNextAsync(
            Task<bool> moveNext,
            IDisposable owner,
            CancellationToken cancellationToken) {

        if (moveNext == null) {
            throw new ArgumentNullException(nameof(moveNext));
        }
        if (owner == null) {
            throw new ArgumentNullException(nameof(owner));
        }
        if (!cancellationToken.CanBeCanceled ||
            moveNext.IsCompleted) {
            return (
                true,
                await moveNext.ConfigureAwait(false));
        }

        Task canceled =
            Task.Delay(
                Timeout.Infinite,
                cancellationToken);
        Task completed =
            await Task.WhenAny(
                    moveNext,
                    canceled)
                .ConfigureAwait(false);
        if (completed == moveNext ||
            moveNext.IsCompleted) {
            return (
                true,
                await moveNext.ConfigureAwait(false));
        }

        _ = moveNext.ContinueWith(
            task => {
                if (task.IsFaulted) {
                    _ = task.Exception;
                }
                try {
                    owner.Dispose();
                } catch {
                    // Preserve the cancellation result after detached native work.
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return (false, false);
    }
}
