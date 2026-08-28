namespace EventViewerX;

/// <summary>Safety bounds for one stateful detection execution.</summary>
public sealed class EventDetectionEngineOptions {
    /// <summary>Maximum observations accepted by one evaluator. Zero is unlimited.</summary>
    public long MaximumObservations { get; set; } = 1_000_000;
    /// <summary>Maximum threshold groups retained at once.</summary>
    public int MaximumGroups { get; set; } = 25_000;
    /// <summary>Maximum evidence observations retained across all threshold groups.</summary>
    public int MaximumStateObservations { get; set; } = 250_000;
    /// <summary>Maximum estimated bytes retained by stateful rules.</summary>
    public long MaximumStateBytes { get; set; } = 256L * 1024L * 1024L;
    /// <summary>Maximum candidate rules evaluated for one observation.</summary>
    public int MaximumCandidateRules { get; set; } = 10_000;
}
