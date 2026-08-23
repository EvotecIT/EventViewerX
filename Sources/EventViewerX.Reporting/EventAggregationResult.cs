namespace EventViewerX.Reporting;

/// <summary>Shared bounded aggregation result consumed by PowerShell, CLI, HTML, and Excel.</summary>
public sealed class EventAggregationResult {
    internal EventAggregationResult(
        EventAggregationDefinition definition,
        IReadOnlyList<EventAggregationRow> rows,
        EventAggregationInputCompleteness inputCompleteness,
        bool aggregationComplete,
        string? diagnostic,
        EventAggregationExecutionMode executionMode,
        long inputRows) {

        Definition = definition;
        Rows = rows;
        InputCompleteness = inputCompleteness;
        AggregationComplete = aggregationComplete;
        Diagnostic = diagnostic;
        ExecutionMode = executionMode;
        InputRows = inputRows;
    }

    /// <summary>Validated aggregation contract snapshot.</summary>
    public EventAggregationDefinition Definition { get; }

    /// <summary>Complete rows, or an empty collection when managed safety bounds were exceeded.</summary>
    public IReadOnlyList<EventAggregationRow> Rows { get; }

    /// <summary>Completeness of the supplied event-query envelope.</summary>
    public EventAggregationInputCompleteness InputCompleteness { get; }

    /// <summary>Whether aggregation state was exhaustive within every configured bound.</summary>
    public bool AggregationComplete { get; }

    /// <summary>True only when both input and aggregation are complete.</summary>
    public bool IsComplete => InputCompleteness == EventAggregationInputCompleteness.Complete && AggregationComplete;

    /// <summary>Reason aggregation rows were withheld or input completeness is limited.</summary>
    public string? Diagnostic { get; }

    /// <summary>Owner that executed the aggregation.</summary>
    public EventAggregationExecutionMode ExecutionMode { get; }

    /// <summary>Number of input rows evaluated.</summary>
    public long InputRows { get; }
}
