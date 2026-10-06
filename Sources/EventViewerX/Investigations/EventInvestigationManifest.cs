namespace EventViewerX;

/// <summary>One hashed original input or generated investigation artifact, relative to the session directory.</summary>
public sealed class EventInvestigationArtifact {
    /// <summary>Relative path inside the investigation directory.</summary>
    public string Path { get; set; } = string.Empty;
    /// <summary>SHA-256 digest of the exact bytes.</summary>
    public string Sha256 { get; set; } = string.Empty;
    /// <summary>Exact file length.</summary>
    public long Length { get; set; }
    /// <summary>Input, observations, plan, or output.</summary>
    public string Role { get; set; } = string.Empty;
    /// <summary>Original input filename, retained separately from its safe local artifact name.</summary>
    public string? OriginalName { get; set; }
}

/// <summary>Versioned reproducibility manifest. Original inputs and derived artifacts remain separate.</summary>
public sealed class EventInvestigationManifest {
    /// <summary>Durable format version.</summary>
    public int SchemaVersion { get; set; } = 1;
    /// <summary>Stable session identity.</summary>
    public Guid SessionId { get; set; } = Guid.NewGuid();
    /// <summary>UTC creation timestamp.</summary>
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    /// <summary>Inclusive investigation start.</summary>
    public DateTime StartUtc { get; set; }
    /// <summary>Exclusive investigation end.</summary>
    public DateTime EndUtc { get; set; }
    /// <summary>Identity of the collection query contract.</summary>
    public string QueryIdentity { get; set; } = string.Empty;
    /// <summary>Retained query definition for inspection; opening a session never executes it.</summary>
    public string QueryDefinition { get; set; } = string.Empty;
    /// <summary>Effective detection plan digest, including tuning.</summary>
    public string PlanHash { get; set; } = string.Empty;
    /// <summary>Exact engine module identity used to produce derived results.</summary>
    public string EngineIdentity { get; set; } = string.Empty;
    /// <summary>Parser names and versions used for each collection input.</summary>
    public Dictionary<string, string> ParserVersions { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Per-source collection receipts, including unsuccessful sources.</summary>
    public EventCoverageWindow[] Sources { get; set; } = Array.Empty<EventCoverageWindow>();
    /// <summary>Hashed copied evidence and generated artifacts.</summary>
    public EventInvestigationArtifact[] Artifacts { get; set; } = Array.Empty<EventInvestigationArtifact>();
    /// <summary>Number of retained canonical observations.</summary>
    public int ObservationCount { get; set; }
    /// <summary>Serialized expected-versus-observed detection coverage.</summary>
    public string CoverageJson { get; set; } = string.Empty;
    /// <summary>Evaluation limits used for original output and replay.</summary>
    public EventInvestigationLimits Limits { get; set; } = new();
}

/// <summary>Serializable execution and evidence bounds shared by original evaluation and replay.</summary>
public sealed class EventInvestigationLimits {
    /// <summary>Maximum canonical observations stored in this session.</summary>
    public int MaximumObservations { get; set; } = 100_000;
    /// <summary>Maximum correlation groups.</summary>
    public int MaximumGroups { get; set; } = 25_000;
    /// <summary>Maximum observations retained by correlation.</summary>
    public int MaximumStateObservations { get; set; } = 250_000;
    /// <summary>Maximum estimated correlation bytes.</summary>
    public long MaximumStateBytes { get; set; } = 256L * 1024 * 1024;
    /// <summary>Maximum candidate rules per observation.</summary>
    public int MaximumCandidateRules { get; set; } = 10_000;
    /// <summary>Maximum bytes in one serialized observation.</summary>
    public int MaximumObservationBytes { get; set; } = 4 * 1024 * 1024;
    /// <summary>Maximum generated findings retained in one evaluation.</summary>
    public int MaximumFindings { get; set; } = 100_000;
    internal EventDetectionEngineOptions Options(EventDetectionCoverage coverage) {
        if (MaximumObservations <= 0 || MaximumObservationBytes <= 0 || MaximumFindings <= 0 || MaximumFindings == int.MaxValue) { throw new InvalidDataException("Investigation evidence bounds must be positive and finite."); }
        return new EventDetectionEngineOptions(MaximumObservations, MaximumGroups, MaximumStateObservations,
            MaximumStateBytes, MaximumCandidateRules, coverage);
    }
}
