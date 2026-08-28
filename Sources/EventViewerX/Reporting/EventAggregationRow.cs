namespace EventViewerX.Reporting;

/// <summary>One deterministic aggregate or trend row.</summary>
public sealed class EventAggregationRow {
    /// <summary>Canonical group dimensions.</summary>
    public IReadOnlyDictionary<string, object?> Group { get; internal set; } =
        new Dictionary<string, object?>();

    /// <summary>Inclusive UTC bucket start.</summary>
    public DateTime? BucketStartUtc { get; internal set; }

    /// <summary>Exclusive UTC bucket end.</summary>
    public DateTime? BucketEndUtc { get; internal set; }

    /// <summary>Offset-qualified local bucket label.</summary>
    public string? BucketLabel { get; internal set; }

    /// <summary>Computed measure values.</summary>
    public IReadOnlyDictionary<string, object?> Measures { get; internal set; } =
        new Dictionary<string, object?>();
}
