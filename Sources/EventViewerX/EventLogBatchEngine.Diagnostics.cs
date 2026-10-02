using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace EventViewerX;

public static partial class EventLogBatchEngine {
    internal static IReadOnlyList<EventQuerySourceExplanation> ExplainSources(EventLogBatchQuery query) {
        EventLogBatchExecutionPlan plan = CreateExecutionPlan(query);
        return Array.AsReadOnly(plan.Sources.SelectMany(static source => source.Explain()).ToArray());
    }

    /// <summary>Reads a batch and records candidate, failure, timing, and completion diagnostics.</summary>
    public static IEnumerable<EventObject> Read(EventLogBatchQuery query, EventQueryExecutionInfo executionInfo,
        CancellationToken cancellationToken = default) {
        if (executionInfo == null) {
            throw new ArgumentNullException(nameof(executionInfo));
        }
        return ReadWithDiagnostics(CreateExecutionPlan(query, executionInfo), executionInfo, cancellationToken);
    }

    /// <summary>Streams a bounded asynchronous batch and records diagnostics through consumer completion.</summary>
    public static IAsyncEnumerable<EventObject> ReadAsync(EventLogBatchQuery query, EventQueryExecutionInfo executionInfo,
        CancellationToken cancellationToken = default) {
        if (executionInfo == null) {
            throw new ArgumentNullException(nameof(executionInfo));
        }
        EventLogBatchExecutionPlan plan = CreateExecutionPlan(query, executionInfo);
        return ReadWithDiagnosticsAsync(plan, executionInfo, cancellationToken);
    }

    private static IEnumerable<EventObject> ReadWithDiagnostics(EventLogBatchExecutionPlan plan,
        EventQueryExecutionInfo info, CancellationToken token) {
        info.Begin();
        var timer = Stopwatch.StartNew();
        try {
            foreach (EventObject item in ReadSynchronously(plan, token)) {
                info.EventsEmitted++;
                yield return item;
            }
            info.Completed = true;
        } finally {
            info.Duration = timer.Elapsed;
            info.Canceled = token.IsCancellationRequested;
        }
    }

    private static async IAsyncEnumerable<EventObject> ReadWithDiagnosticsAsync(EventLogBatchExecutionPlan plan,
        EventQueryExecutionInfo info, [EnumeratorCancellation] CancellationToken token) {
        info.Begin();
        var timer = Stopwatch.StartNew();
        try {
            await foreach (EventObject item in EventLogEngine.ReadAsync(
                stop => ReadWithAsynchronousPriming(plan, stop), 64, token, info).ConfigureAwait(false)) {
                info.EventsEmitted++;
                yield return item;
            }
            info.Completed = true;
        } finally {
            info.Duration = timer.Elapsed;
            info.Canceled = token.IsCancellationRequested;
        }
    }
}
