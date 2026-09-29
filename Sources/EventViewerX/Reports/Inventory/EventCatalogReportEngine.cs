namespace EventViewerX.Reports.Inventory;

/// <summary>Lists event log channels and providers through the shared EventViewerX catalog logic.</summary>
public static class EventCatalogReportEngine {
    /// <summary>Lists channels visible to the current identity on the selected machine.</summary>
    public static bool TryListChannels(
        EventCatalogQueryRequest request,
        out EventChannelListResult result,
        out EventCatalogFailure? failure,
        CancellationToken cancellationToken = default) =>
        EventCatalogQueryExecutor.TryListChannels(request, out result, out failure, cancellationToken);

    /// <summary>Lists providers visible to the current identity on the selected machine.</summary>
    public static bool TryListProviders(
        EventCatalogQueryRequest request,
        out EventProviderListResult result,
        out EventCatalogFailure? failure,
        CancellationToken cancellationToken = default) =>
        EventCatalogQueryExecutor.TryListProviders(request, out result, out failure, cancellationToken);
}
