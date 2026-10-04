using System.Text.Json;
using EventViewerX.Reporting;
using EventViewerX.Storage;

namespace EventViewerX.Cli;

internal static partial class Program {
    private static async Task<int> QueryAsync(CliArguments options) {
        ValidateQuerySource(options, allowSummary: false);
        ValidateQuerySummaryPath(options);
        ValidateOccurrenceOptions(options);
        using var cancellation = new ConsoleQueryCancellation();
        CancellationToken token = cancellation.Token;
        if (options.Get("store") is string storePath) {
            EventStoreQuery storedQuery = CreateStoreQuery(options);
            if (options.Has("explain")) {
                if (storedQuery.Predicate == null) {
                    throw new ArgumentException("--explain requires --where.");
                }
                EventStoreQueryPlan plan = await new EventStore(storePath)
                    .PlanAsync(storedQuery, token)
                    .ConfigureAwait(false);
                return WriteJson(plan);
            }
            if (options.Has("stream")) {
                EventStoreRowReadResult streamed = await new EventStore(storePath)
                    .StreamRowsAsync(storedQuery, (row, _) => {
                        Console.WriteLine(JsonSerializer.Serialize(EventReportJsonProjection.Project(row), JsonOptions));
                        return Task.CompletedTask;
                    }, token).ConfigureAwait(false);
                return CompleteQuery(new EventReportSummary(streamed.RowsRead, streamed.EventsScanned,
                    streamed.ScanLimitReached, streamed.CompletenessDiagnostic), options);
            }
            EventReport stored = await new EventStore(storePath)
                .ReadReportAsync(storedQuery, options.Get("title"), token)
                .ConfigureAwait(false);
            return WriteRows(ApplyOccurrenceGrouping(stored, options), options);
        }
        if (options.Get("context-store") != null) {
            EventReport contextual = await QueryGroupPolicyReportAsync(options).ConfigureAwait(false);
            await WriteStoreIfRequestedAsync(contextual, options).ConfigureAwait(false);
            return WriteRows(ApplyOccurrenceGrouping(contextual, options), options);
        }
        EventReportRequest request = CreateRequest(options);
        CollectionCheckpointContext? checkpoint =
            await PrepareCollectionCheckpointAsync(request, options)
                .ConfigureAwait(false);
        if (options.Has("explain")) {
            EventPredicate predicate = request.Predicate ??
                throw new ArgumentException("--explain requires --where.");
            if (request.Types != null && request.Types.Count > 0) {
                predicate = EventPredicateBuilder.ForTypes(request.Types).Normalize(predicate);
            }
            EventPredicatePlan plan = request.Definition != null
                ? EventDefinitionEngine.PlanPredicate(
                    request.Definition,
                    predicate,
                    request.Collectors != null && request.Collectors.Count > 0
                        ? "ForwardedEvents"
                        : null)
                : request.Collectors != null && request.Collectors.Count > 0
                    ? EventPredicatePlanner.PlanManaged(
                        predicate,
                        "ForwardedEvents uses the Windows Server 2025 safe '*' reader, so typed filtering is bounded and managed.")
                    : EventPredicatePlanner.Plan(predicate);
            return WriteJson(plan);
        }
        if (options.Has("stream")) {
            EventReportSummary summary = await EventReportEngine.StreamRowsAsync(request, (row, section, _) => {
                Console.WriteLine(JsonSerializer.Serialize(EventReportJsonProjection.Project(row, section), JsonOptions));
                return Task.CompletedTask;
            }, token).ConfigureAwait(false);
            return CompleteQuery(summary, options);
        }
        EventReport report = await EventReportEngine.QueryAsync(request, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        if (checkpoint != null) {
            await WriteCheckpointedStoreAsync(report, checkpoint)
                .ConfigureAwait(false);
        } else {
            await WriteStoreIfRequestedAsync(report, options).ConfigureAwait(false);
        }
        return WriteRows(ApplyOccurrenceGrouping(report, options), options);
    }

    private sealed class ConsoleQueryCancellation : IDisposable {
        private readonly CancellationTokenSource source = new();
        private readonly object gate = new();
        private bool disposed;
        internal ConsoleQueryCancellation() => Console.CancelKeyPress += Cancel;
        internal CancellationToken Token => source.Token;
        private void Cancel(object? sender, ConsoleCancelEventArgs args) {
            lock (gate) {
                if (!disposed) {
                    args.Cancel = true;
                    source.Cancel();
                }
            }
        }
        public void Dispose() {
            lock (gate) {
                disposed = true;
                Console.CancelKeyPress -= Cancel;
                source.Dispose();
            }
        }
    }
}
