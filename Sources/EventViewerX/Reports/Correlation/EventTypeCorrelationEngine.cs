namespace EventViewerX.Reports.Correlation;

/// <summary>
/// Queries typed events and builds bounded correlation groups and timeline buckets.
/// </summary>
public static class EventTypeCorrelationEngine {
    /// <summary>Supported event-field dimensions for grouping.</summary>
    public static IReadOnlyList<string> AllowedCorrelationKeys =>
        NamedEventsTimelineQueryExecutor.AllowedCorrelationKeys;

    /// <summary>Default identity and computer dimensions.</summary>
    public static IReadOnlyList<string> DefaultCorrelationKeys =>
        NamedEventsTimelineQueryExecutor.DefaultCorrelationKeys;

    /// <summary>
    /// Queries the selected event types and returns ordered rows, groups, and minute buckets.
    /// </summary>
    public static Task<(NamedEventsTimelineQueryResult? Result, NamedEventsTimelineQueryFailure? Failure)> TryQueryAsync(
        NamedEventsTimelineQueryRequest request,
        CancellationToken cancellationToken = default) =>
        NamedEventsTimelineQueryExecutor.TryBuildAsync(request, cancellationToken);
}
