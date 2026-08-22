namespace EventViewerX;

/// <summary>Strongest evidence represented by one readiness check.</summary>
public enum EventReadinessEvidenceLevel {
    /// <summary>No useful evidence could be collected.</summary>
    Unknown,
    /// <summary>The runtime or target was directly inspected.</summary>
    Inspected,
    /// <summary>A native event-log query executed successfully.</summary>
    Transport,
    /// <summary>The effective local Windows policy was read.</summary>
    Effective,
    /// <summary>A matching historical event was observed.</summary>
    Observed
}
