namespace EventViewerX;

/// <summary>Structured reason attached to a non-pass readiness result.</summary>
public enum EventReadinessDiagnosticKind {
    /// <summary>No additional diagnostic classification is required.</summary>
    None,
    /// <summary>The current security context was not permitted to inspect the evidence.</summary>
    AccessDenied,
    /// <summary>The bounded operation exceeded its deadline.</summary>
    Timeout,
    /// <summary>The target or required service could not be reached.</summary>
    Unavailable,
    /// <summary>A required channel, setting, target, or other resource is absent.</summary>
    Missing,
    /// <summary>The inspected configuration does not satisfy the requirement.</summary>
    InvalidConfiguration,
    /// <summary>The check ran but did not obtain evidence strong enough to prove the requirement.</summary>
    NoEvidence,
    /// <summary>A declared scan or discovery bound was reached.</summary>
    Truncated,
    /// <summary>Another classified operation failure occurred.</summary>
    Error
}
