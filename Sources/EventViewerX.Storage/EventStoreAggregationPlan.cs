using EventViewerX.Reporting;

namespace EventViewerX.Storage;

/// <summary>Explains whether a stored aggregation can execute in SQLite or requires the shared managed engine.</summary>
public sealed class EventStoreAggregationPlan {
    internal EventStoreAggregationPlan(EventAggregationExecutionMode mode, string reason) {
        ExecutionMode = mode;
        Reason = reason;
    }

    /// <summary>Selected execution owner.</summary>
    public EventAggregationExecutionMode ExecutionMode { get; }

    /// <summary>Human-readable explanation of the selected execution path.</summary>
    public string Reason { get; }
}
