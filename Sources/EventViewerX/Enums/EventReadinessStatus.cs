namespace EventViewerX;

/// <summary>Outcome of one readiness check.</summary>
public enum EventReadinessStatus {
    /// <summary>The required contract was proven.</summary>
    Pass,
    /// <summary>The contract is usable but deserves operator attention.</summary>
    Warning,
    /// <summary>The required contract was proven not ready.</summary>
    Fail,
    /// <summary>The current identity or mechanism could not prove the contract.</summary>
    Unknown,
    /// <summary>The check does not apply to the selected workflow.</summary>
    Skipped
}
