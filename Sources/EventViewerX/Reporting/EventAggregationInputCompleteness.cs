namespace EventViewerX.Reporting;

/// <summary>Completeness evidence supplied with aggregation input.</summary>
public enum EventAggregationInputCompleteness {
    /// <summary>A plain row stream whose source coverage is unknown.</summary>
    Unknown,
    /// <summary>Every requested source and row is known to be complete.</summary>
    Complete,
    /// <summary>At least one source, scan, or result bound is incomplete.</summary>
    Incomplete
}
