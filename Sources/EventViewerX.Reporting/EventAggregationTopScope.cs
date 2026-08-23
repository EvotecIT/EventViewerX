namespace EventViewerX.Reporting;

/// <summary>Ranking scope when top-N and time buckets are combined.</summary>
public enum EventAggregationTopScope {
    /// <summary>Rank canonical groups across the complete query window.</summary>
    GlobalGroup,
    /// <summary>Rank groups independently inside each calendar bucket.</summary>
    PerBucket
}
