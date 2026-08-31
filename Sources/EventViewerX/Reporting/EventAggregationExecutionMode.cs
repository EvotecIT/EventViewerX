namespace EventViewerX.Reporting;

/// <summary>Owner that executed an aggregation.</summary>
public enum EventAggregationExecutionMode {
    /// <summary>Bounded shared managed engine.</summary>
    Managed,
    /// <summary>SQLite provider pushdown.</summary>
    SqlitePushdown
}
