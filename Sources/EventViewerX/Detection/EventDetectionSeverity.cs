namespace EventViewerX;

/// <summary>Operator-facing impact assigned to a detection finding.</summary>
public enum EventDetectionSeverity {
    /// <summary>Informational evidence that does not imply a threat.</summary>
    Informational,
    /// <summary>Low-impact activity worth retaining or reviewing.</summary>
    Low,
    /// <summary>Material activity that normally warrants investigation.</summary>
    Medium,
    /// <summary>High-impact or strongly suspicious activity.</summary>
    High,
    /// <summary>Critical activity requiring prompt response.</summary>
    Critical
}
