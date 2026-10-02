namespace EventViewerX;

public static partial class EventLogBatchEngine {
    /// <summary>
    /// Asynchronously reads a deterministic merge with concurrently primed sources and a bounded
    /// 64-event output buffer. One producer advances the merge without scheduling work per event.
    /// </summary>
    public static IAsyncEnumerable<EventObject> ReadAsync(
        EventLogBatchQuery query,
        CancellationToken cancellationToken = default) {

        EventLogBatchExecutionPlan plan = CreateExecutionPlan(query);
        return EventLogEngine.ReadAsync(
            token => ReadWithAsynchronousPriming(plan, token),
            bufferCapacity: 64,
            cancellationToken);
    }

    private static IEnumerable<EventObject> ReadWithAsynchronousPriming(
        EventLogBatchExecutionPlan plan,
        CancellationToken cancellationToken) {

        // This iterator runs only on the bounded stream's producer, never the caller's thread.
        // Async priming retains prompt cancellation and detached cleanup of stalled source opens.
        var primingTimer = System.Diagnostics.Stopwatch.StartNew();
        EventSourceCursor?[] primed = PrimeConcurrentlyAsync<EventSourceCursor>(
            plan.Sources.Length,
            plan.MaxConcurrency,
            cancellationToken,
            (index, primingToken) => PrimeSourceAsync(
                index, plan.Sources[index], plan.ContinueOnError, plan.FailureHandler,
                cancellationToken, primingToken, plan.ExecutionInfo)).GetAwaiter().GetResult();
        if (plan.ExecutionInfo != null) {
            plan.ExecutionInfo.PrimingDuration = primingTimer.Elapsed;
        }
        foreach (EventObject item in ReadPrimed(plan, primed, cancellationToken)) {
            yield return item;
        }
    }
    private static async Task<EventSourceCursor?> PrimeSourceAsync(
        int index,
        EventSourceSnapshot source,
        bool continueOnError,
        Action<EventLogQueryFailure>? failureHandler,
        CancellationToken requestToken,
            CancellationToken primingToken,
            EventQueryExecutionInfo? executionInfo = null) {

        return await Task.Run(() => {
            CancellationTokenSource sourceLifetime =
                CreatePrimedSourceLifetime(
                    requestToken,
                    primingToken);
            EventSourceCursor? cursor;
            try {
                cursor = TryOpenCursor(
                    index,
                    source,
                    continueOnError,
                    failureHandler,
                    sourceLifetime.Token,
                    sourceLifetime,
                    executionInfo);
            } catch {
                sourceLifetime.Dispose();
                throw;
            }
            if (cursor == null) {
                sourceLifetime.Dispose();
                return null;
            }
            try {
                if (TryMoveNext(
                        cursor,
                        continueOnError,
                        failureHandler,
                        primingToken)) {
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

}
