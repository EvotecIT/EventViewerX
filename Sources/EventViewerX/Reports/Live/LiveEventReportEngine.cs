namespace EventViewerX.Reports.Live;

/// <summary>Reads bounded live event rows and statistics using the shared EventViewerX report logic.</summary>
public static class LiveEventReportEngine {
    /// <summary>Reads matching events from a live local or remote channel.</summary>
    public static bool TryRead(
        LiveEventQueryRequest request,
        out LiveEventQueryResult result,
        out LiveEventQueryFailure? failure,
        CancellationToken cancellationToken = default) =>
        LiveEventQueryExecutor.TryRead(request, out result, out failure, cancellationToken);

    /// <summary>Aggregates matching events from a live local or remote channel.</summary>
    public static bool TryBuildStats(
        LiveStatsQueryRequest request,
        out LiveStatsQueryResult result,
        out LiveStatsQueryFailure? failure,
        CancellationToken cancellationToken = default) =>
        LiveStatsQueryExecutor.TryBuild(request, out result, out failure, cancellationToken);
}
