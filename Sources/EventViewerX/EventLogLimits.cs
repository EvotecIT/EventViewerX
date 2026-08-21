namespace EventViewerX;

/// <summary>Shared safety limits for Windows Event Log operations.</summary>
public static class EventLogLimits {
    /// <summary>
    /// Default maximum number of offline records inspected while resolving
    /// provider wildcards before the caller supplies an explicit scan limit.
    /// </summary>
    public const long MaximumOfflineProviderDiscoveryEvents =
        65536;

    /// <summary>
    /// Maximum number of independent event-log sources opened concurrently by
    /// the shared query, catalog, and event-type engines.
    /// </summary>
    public const int MaximumConcurrency = 64;
}
