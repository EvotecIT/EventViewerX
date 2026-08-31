namespace EventViewerX.Reporting;

/// <summary>Validated shared aggregation, trend, and top-N contract.</summary>
public sealed class EventAggregationDefinition {
    /// <summary>Canonical fields forming each group key.</summary>
    public IReadOnlyList<string> GroupBy { get; set; } = Array.Empty<string>();

    /// <summary>Optional calendar bucket.</summary>
    public EventAggregationBucket Bucket { get; set; }

    /// <summary>Cross-platform timezone identifier. UTC is the default.</summary>
    public string TimeZoneId { get; set; } = "UTC";

    /// <summary>Requested measures.</summary>
    public IReadOnlyList<EventAggregationMeasure> Measures { get; set; } = new[] {
        new EventAggregationMeasure { Operation = EventAggregationOperation.Count, OutputName = "Count" }
    };

    /// <summary>Maximum ranked groups returned. Zero returns all groups.</summary>
    public int Top { get; set; }

    /// <summary>Ranking scope when a bucket is present.</summary>
    public EventAggregationTopScope TopScope { get; set; } = EventAggregationTopScope.GlobalGroup;

    /// <summary>Measure output used for ranking. The first measure is used when omitted.</summary>
    public string? RankingMeasure { get; set; }

    /// <summary>Null handling for group-key fields.</summary>
    public EventAggregationNullPolicy GroupNulls { get; set; } = EventAggregationNullPolicy.Include;

    /// <summary>Explicit query-window start used by an unbucketed Rate.</summary>
    public DateTime? WindowStart { get; set; }

    /// <summary>Explicit query-window end used by an unbucketed Rate.</summary>
    public DateTime? WindowEnd { get; set; }

    /// <summary>Maximum managed group states.</summary>
    public int MaximumGroups { get; set; } = 25000;

    /// <summary>Maximum distinct values retained by any single measure state.</summary>
    public int MaximumDistinctValues { get; set; } = 100000;

    /// <summary>Approximate total managed state budget in bytes.</summary>
    public long MaximumStateBytes { get; set; } = 64L * 1024L * 1024L;
}
