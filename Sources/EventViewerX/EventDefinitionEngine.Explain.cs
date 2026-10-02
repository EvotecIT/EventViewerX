namespace EventViewerX;

public static partial class EventDefinitionEngine {
    /// <summary>Explains resolved declarative sources, native partitions, projection, predicates, and limits without reading events.</summary>
    public static EventQueryExplanation Explain(EventDefinitionQuery query) {
        EventDefinitionQuery snapshot = CreateSnapshot(query);
        EventPredicatePlan? predicate = CreatePredicatePlan(snapshot);
        (DateTime? start, DateTime? end) = EventTimeRange.Resolve(
            snapshot.StartTime, snapshot.EndTime, snapshot.TimePeriod);
        EventLogBatchQuery? batch = snapshot.Paths != null && snapshot.Paths.Count > 0
            ? CreateFileBatch(snapshot, predicate?.NativeFilter, start, end)
            : CreateChannelBatch(snapshot, new EventDefinitionQueryExecutionInfo(), predicate?.NativeFilter, start, end);
        if (batch != null) {
            batch.MaxEvents = 0;
            batch.MaxConcurrency = snapshot.MaxConcurrency;
            batch.ContinueOnError = snapshot.ContinueOnRemoteFailure;
        }
        var stages = new List<string> { "Declarative projection: " + snapshot.Definition.Name };
        if (predicate?.ManagedPredicate != null) {
            stages.Add("Exact declarative predicate verification");
        }
        if (snapshot.ResultPredicate != null) {
            stages.Add("Caller-supplied result predicate");
        }
        return new EventQueryExplanation(batch, stages, predicate, snapshot.MaxCandidates, snapshot.MaxEvents);
    }
}