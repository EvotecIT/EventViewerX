using EventViewerX.Reports.Evtx;

namespace EventViewerX.Reports.Security;

/// <summary>
/// Builds bounded security summaries from one Windows EVTX file using the shared EventViewerX report engine.
/// </summary>
public static class SecurityEvtxSummaryEngine {
    /// <summary>Summarizes user logon and logoff events.</summary>
    public static bool TryQueryUserLogons(
        SecurityEvtxQueryRequest request,
        out SecurityUserLogonsQueryResult result,
        out EvtxQueryFailure? failure,
        CancellationToken cancellationToken = default) =>
        SecurityEvtxQueryExecutor.TryBuildUserLogons(request, out result, out failure, cancellationToken);

    /// <summary>Summarizes failed logon events and their status details.</summary>
    public static bool TryQueryFailedLogons(
        SecurityEvtxQueryRequest request,
        out SecurityFailedLogonsQueryResult result,
        out EvtxQueryFailure? failure,
        CancellationToken cancellationToken = default) =>
        SecurityEvtxQueryExecutor.TryBuildFailedLogons(request, out result, out failure, cancellationToken);

    /// <summary>Summarizes account lockout events and their affected identities.</summary>
    public static bool TryQueryAccountLockouts(
        SecurityEvtxQueryRequest request,
        out SecurityAccountLockoutsQueryResult result,
        out EvtxQueryFailure? failure,
        CancellationToken cancellationToken = default) =>
        SecurityEvtxQueryExecutor.TryBuildAccountLockouts(request, out result, out failure, cancellationToken);
}
