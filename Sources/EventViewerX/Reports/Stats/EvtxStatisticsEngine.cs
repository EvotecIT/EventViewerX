using EventViewerX.Reports.Evtx;

namespace EventViewerX.Reports.Stats;

/// <summary>
/// Builds bounded statistics for a Windows EVTX file using the shared EventViewerX query and aggregation engine.
/// </summary>
public static class EvtxStatisticsEngine {
    /// <summary>
    /// Queries one EVTX file and returns counts, time bounds, and ranked dimensions.
    /// </summary>
    public static bool TryQuery(
        EvtxStatsQueryRequest request,
        out EvtxStatsQueryResult result,
        out EvtxQueryFailure? failure,
        CancellationToken cancellationToken = default) =>
        EvtxStatsQueryExecutor.TryBuild(request, out result, out failure, cancellationToken);
}
