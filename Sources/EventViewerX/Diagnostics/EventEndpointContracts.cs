namespace EventViewerX;

/// <summary>One original endpoint artifact. Opening or analyzing it never invokes a command on the endpoint.</summary>
public sealed class EventEndpointInput {
    /// <summary>Original file path to copy into the investigation.</summary>
    public string Path { get; set; } = string.Empty;
    /// <summary>Log, DsRegCmd, Facts, Registry, or Attachment. Registry and attachments are retained without executing or importing them.</summary>
    public string Kind { get; set; } = "Log";
    /// <summary>Collector-declared endpoint identity. Empty keeps application correlation source-local.</summary>
    public string Device { get; set; } = string.Empty;
    /// <summary>Collector-declared capture instant; null when unknown.</summary>
    public DateTimeOffset? CapturedAt { get; set; }
    /// <summary>User, SYSTEM, or Unknown; supplied by the collector, not inferred from a filename.</summary>
    public string ExecutionContext { get; set; } = "Unknown";
    /// <summary>UTC offset of the log writer when absent from individual records.</summary>
    public TimeSpan? UtcOffset { get; set; }
}

/// <summary>Offline endpoint capture bounds and intended state. All original files remain sensitive evidence.</summary>
public sealed class EventEndpointCapture {
    /// <summary>Original files, with declared collection context.</summary>
    public EventEndpointInput[] Inputs { get; set; } = Array.Empty<EventEndpointInput>();
    /// <summary>Optional intended join state: Entra, Hybrid, Domain, or Unjoined. Null means unknown intent.</summary>
    public string? ExpectedJoin { get; set; }
    /// <summary>Maximum total diagnostic records.</summary>
    public int MaximumRecords { get; set; } = 25_000;
    /// <summary>Maximum bytes copied across all original endpoint inputs.</summary>
    public long MaximumInputBytes { get; set; } = 256L * 1024 * 1024;
    /// <summary>Maximum bytes retained across derived diagnostic records.</summary>
    public long MaximumRecordBytes { get; set; } = 64L * 1024 * 1024;
}

/// <summary>A proposed evidence collection action with an explicit reason. It is descriptive, never executable.</summary>
public sealed class EventDiagnosticNextCheck {
    /// <summary>Artifact or configuration needed.</summary>
    public string Artifact { get; set; } = string.Empty;
    /// <summary>What to inspect or collect.</summary>
    public string Action { get; set; } = string.Empty;
    /// <summary>Why existing evidence cannot settle the question.</summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>Evidence-backed endpoint conclusion. Absence of records is never proof of absence of activity.</summary>
public sealed class EventDiagnosticFinding {
    /// <summary>Versioned independently authored rule identity.</summary>
    public string RuleId { get; set; } = string.Empty;
    /// <summary>Rule content version.</summary>
    public string RuleVersion { get; set; } = "1.0.0";
    /// <summary>Operator-facing conclusion.</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>Observed, Failure, or InsufficientEvidence.</summary>
    public string Status { get; set; } = "InsufficientEvidence";
    /// <summary>Explanation restricted to the evidence.</summary>
    public string Explanation { get; set; } = string.Empty;
    /// <summary>Identities of supporting records or original artifacts.</summary>
    public string[] EvidenceIdentities { get; set; } = Array.Empty<string>();
    /// <summary>Next checks needed to refine the conclusion.</summary>
    public EventDiagnosticNextCheck[] NextChecks { get; set; } = Array.Empty<EventDiagnosticNextCheck>();
    /// <summary>Primary documentation for the platform contract.</summary>
    public string[] References { get; set; } = Array.Empty<string>();
}

/// <summary>One application attempt. Phase observations and installation outcomes are distinct from deployment completion.</summary>
public sealed class EventIntuneApplicationAttempt {
    /// <summary>Explicit application ID.</summary>
    public string ApplicationId { get; set; } = string.Empty;
    /// <summary>Explicit user/system context, or Unknown.</summary>
    public string Context { get; set; } = "Unknown";
    /// <summary>Explicit attempt ID when supplied, otherwise a conservative source-local inferred sequence.</summary>
    public string AttemptId { get; set; } = string.Empty;
    /// <summary>Whether the attempt boundary is explicit or inferred.</summary>
    public string BoundaryQuality { get; set; } = "Inferred";
    /// <summary>Last observed phase; phases not in evidence remain unknown.</summary>
    public string LastPhase { get; set; } = "Unknown";
    /// <summary>Installed, InstallerSucceededDetectionUnknown, RestartRequired, Failure, NotApplicable, or Unknown.</summary>
    public string Outcome { get; set; } = "Unknown";
    /// <summary>Raw installer result; process exit codes are interpreted only in their declared context.</summary>
    public string? ExitCode { get; set; }
    /// <summary>Exact phase evidence in deterministic observation order.</summary>
    public string[] EvidenceIdentities { get; set; } = Array.Empty<string>();
    /// <summary>Distinct confirmed phases in this attempt.</summary>
    public string[] Phases { get; set; } = Array.Empty<string>();
}

/// <summary>One captured DSRegCmd field. Section and original value are retained, including unknown and conflicting values.</summary>
public sealed class EventDsRegField {
    /// <summary>Section heading.</summary>
    public string Section { get; set; } = string.Empty;
    /// <summary>Field name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Uninterpreted captured value.</summary>
    public string Value { get; set; } = string.Empty;
    /// <summary>One-based source line.</summary>
    public int Line { get; set; }
}

/// <summary>DSRegCmd snapshot with declared context; user SSO and device registration are separate contracts.</summary>
public sealed class EventDsRegSnapshot {
    /// <summary>Original artifact identity.</summary>
    public string EvidenceIdentity { get; set; } = string.Empty;
    /// <summary>Declared capture instant, or unknown.</summary>
    public DateTimeOffset? CapturedAt { get; set; }
    /// <summary>Declared User, SYSTEM, or Unknown execution context.</summary>
    public string ExecutionContext { get; set; } = "Unknown";
    /// <summary>Captured fields, including unrecognized entries.</summary>
    public EventDsRegField[] Fields { get; set; } = Array.Empty<EventDsRegField>();
}

/// <summary>Reproducible endpoint analysis over captured records, alongside the session's native Windows-event findings.</summary>
public sealed class EventEndpointAnalysis {
    /// <summary>Endpoint analysis schema version.</summary>
    public int SchemaVersion { get; set; } = 1;
    /// <summary>Rule/reducer content version.</summary>
    public string AnalyzerVersion { get; set; } = "1.0.0";
    /// <summary>Parsed diagnostic records, bounded by capture limits.</summary>
    public EventDiagnosticRecord[] Records { get; set; } = Array.Empty<EventDiagnosticRecord>();
    /// <summary>Captured identity snapshots.</summary>
    public EventDsRegSnapshot[] IdentitySnapshots { get; set; } = Array.Empty<EventDsRegSnapshot>();
    /// <summary>Application attempts reconstructed from explicitly attributable evidence.</summary>
    public EventIntuneApplicationAttempt[] Applications { get; set; } = Array.Empty<EventIntuneApplicationAttempt>();
    /// <summary>Conclusions and next required artifacts.</summary>
    public EventDiagnosticFinding[] Findings { get; set; } = Array.Empty<EventDiagnosticFinding>();
    /// <summary>True only when all supplied log bytes were read as complete valid records within bounds. It does not assert endpoint or lifecycle coverage.</summary>
    public bool InputComplete { get; set; }
    /// <summary>Parsing, attribution, timing, and collection limitations.</summary>
    public string[] CoverageDiagnostics { get; set; } = Array.Empty<string>();
}