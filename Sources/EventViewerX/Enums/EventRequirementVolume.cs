namespace EventViewerX;

/// <summary>Expected relative event-volume guidance for a prerequisite.</summary>
public enum EventRequirementVolume {
    /// <summary>No stable volume guidance is currently recorded.</summary>
    Unknown,
    /// <summary>Usually low volume.</summary>
    Low,
    /// <summary>Usually moderate volume.</summary>
    Medium,
    /// <summary>Usually high volume.</summary>
    High,
    /// <summary>Microsoft documents the source as very high volume.</summary>
    VeryHigh
}
