namespace EventViewerX;

public static partial class EventTypeEngine {
    /// <summary>Explains resolved typed sources, native partitions, projection, predicates, and limits without reading events.</summary>
    public static EventQueryExplanation Explain(EventTypeQuery query) {
        EventTypeQuery snapshot = EventTypeQuerySnapshot.Copy(query);
        Validate(snapshot);
        EventTypeProjectionPlan projection = EventTypeCatalog.CompileProjectionPlan(snapshot.Types);
        IReadOnlyList<EventSourceDefinition> sources = RestrictSources(
            EventTypeCatalog.GetSources(projection.ExpandedTypes), snapshot.SourceLogName,
            snapshot.SourceEventIds, snapshot.SourceProviderNames);
        EventPredicatePlan? predicate = CreateQueryPredicatePlan(snapshot, projection.ExpandedTypes);
        EventLogBatchQuery? batch = sources.Count == 0 ? null :
            CreateBatch(snapshot, sources, new EventTypeQueryExecutionInfo(), predicate?.NativeFilter);
        var stages = new List<string> { "Typed projection: " + string.Join(", ", projection.ExpandedTypes) };
        if (predicate?.ManagedPredicate != null) {
            stages.Add("Exact typed predicate verification");
        }
        if (snapshot.ResultPredicate != null) {
            stages.Add("Caller-supplied result predicate");
        }
        return new EventQueryExplanation(batch, stages, predicate, snapshot.MaxCandidates, snapshot.MaxEvents);
    }

    private static EventPredicatePlan? CreateQueryPredicatePlan(EventTypeQuery query,
        IReadOnlyList<EventType> resolvedTypes) {
        if (query.Predicate == null) {
            return null;
        }
        EventPredicate exact = EventPredicateBuilder.ForTypes(resolvedTypes).Normalize(query.Predicate);
        return string.IsNullOrWhiteSpace(query.CollectorLogName)
            ? EventPredicatePlanner.Plan(exact)
            : EventPredicatePlanner.PlanManagedOnly(exact,
                "ForwardedEvents uses the Windows Server 2025 safe '*' reader, so typed filtering is bounded and managed.");
    }
}