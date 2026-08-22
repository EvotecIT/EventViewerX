namespace EventViewerX;

/// <summary>One typed, actionable readiness check result.</summary>
public sealed class EventReadinessCheckResult {
    internal EventReadinessCheckResult(
        EventReadinessLayer layer,
        string check,
        string target,
        EventReadinessStatus status,
        EventReadinessEvidenceLevel evidenceLevel,
        string evidence,
        string remediation,
        bool required,
        string? requirementKey = null,
        TimeSpan? duration = null,
        EventReadinessDiagnosticKind diagnosticKind = EventReadinessDiagnosticKind.None) {

        Layer = layer;
        Check = check;
        Target = target;
        Status = status;
        EvidenceLevel = evidenceLevel;
        Evidence = evidence;
        Remediation = remediation;
        Required = required;
        RequirementKey = requirementKey;
        Duration = duration ?? TimeSpan.Zero;
        DiagnosticKind = diagnosticKind;
    }

    /// <summary>Operational layer.</summary>
    public EventReadinessLayer Layer { get; }
    /// <summary>Stable check name.</summary>
    public string Check { get; }
    /// <summary>Machine, domain, forest, or local scope inspected.</summary>
    public string Target { get; }
    /// <summary>Check outcome.</summary>
    public EventReadinessStatus Status { get; }
    /// <summary>Strongest evidence represented by the result.</summary>
    public EventReadinessEvidenceLevel EvidenceLevel { get; }
    /// <summary>Concise evidence statement.</summary>
    public string Evidence { get; }
    /// <summary>Actionable next step when the result is not a pass.</summary>
    public string Remediation { get; }
    /// <summary>Whether this check contributes to readiness.</summary>
    public bool Required { get; }
    /// <summary>Stable requirement catalog key when applicable.</summary>
    public string? RequirementKey { get; }
    /// <summary>Check duration.</summary>
    public TimeSpan Duration { get; }
    /// <summary>Structured reason for a warning, failure, or unknown result.</summary>
    public EventReadinessDiagnosticKind DiagnosticKind { get; }
}
