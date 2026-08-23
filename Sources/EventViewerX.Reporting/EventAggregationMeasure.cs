namespace EventViewerX.Reporting;

/// <summary>Typed descriptor for one aggregation measure.</summary>
public sealed class EventAggregationMeasure {
    /// <summary>Aggregation operation.</summary>
    public EventAggregationOperation Operation { get; set; } = EventAggregationOperation.Count;

    /// <summary>Semantic field operand. Count and Rate do not require one.</summary>
    public string? Field { get; set; }

    /// <summary>Unique result field name. A stable name is generated when omitted.</summary>
    public string? OutputName { get; set; }

    /// <summary>Null participation for operand-based measures.</summary>
    public EventAggregationNullPolicy Nulls { get; set; } = EventAggregationNullPolicy.Exclude;

    /// <summary>Rate unit. Required for Rate and ignored by other operations.</summary>
    public TimeSpan? RateUnit { get; set; }
}
