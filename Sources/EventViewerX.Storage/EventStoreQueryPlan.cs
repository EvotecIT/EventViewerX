namespace EventViewerX.Storage;

/// <summary>Explains SQLite prefiltering and exact managed verification for a stored query.</summary>
public sealed class EventStoreQueryPlan {
    internal EventStoreQueryPlan(
        IReadOnlyList<EventStoreQueryPlanStep> steps,
        long maxCandidates) {

        Steps = steps;
        MaxCandidates = maxCandidates;
    }

    /// <summary>Direct, indexed, and managed query stages.</summary>
    public IReadOnlyList<EventStoreQueryPlanStep> Steps { get; }
    /// <summary>Candidate cap used when exact managed verification is required.</summary>
    public long MaxCandidates { get; }
    /// <summary>Whether at least one predicate dimension is pushed into indexed SQLite selection.</summary>
    public bool HasSqlPredicatePrefilter => Steps.Any(static step => step.Stage == EventStoreQueryPlanStage.Sql);
    /// <summary>Whether exact normalized-row verification is required.</summary>
    public bool HasManagedVerification => Steps.Any(static step => step.Stage == EventStoreQueryPlanStage.Managed);
}

/// <summary>Stored-query execution stage.</summary>
public enum EventStoreQueryPlanStage {
    /// <summary>Direct query dimensions applied to indexed SQLite columns.</summary>
    Sql,
    /// <summary>Exact predicate verification against the normalized typed row.</summary>
    Managed
}

/// <summary>One stored-query planning decision.</summary>
public sealed class EventStoreQueryPlanStep {
    internal EventStoreQueryPlanStep(string expression, EventStoreQueryPlanStage stage, string reason) {
        Expression = expression;
        Stage = stage;
        Reason = reason;
    }

    /// <summary>Query dimension or predicate expression.</summary>
    public string Expression { get; }
    /// <summary>SQLite or managed stage.</summary>
    public EventStoreQueryPlanStage Stage { get; }
    /// <summary>Reason for the selected stage.</summary>
    public string Reason { get; }
}
