namespace EventViewerX;

/// <summary>Environment-specific overrides applied while compiling a detection plan.</summary>
public sealed class EventDetectionTuning {
    /// <summary>Rule identifiers omitted from the compiled plan.</summary>
    public IReadOnlyList<string> DisabledRuleIds { get; set; } = Array.Empty<string>();
    /// <summary>Severity overrides keyed by stable rule identifier.</summary>
    public IReadOnlyDictionary<string, EventDetectionSeverity> SeverityOverrides { get; set; } =
        new Dictionary<string, EventDetectionSeverity>();
    /// <summary>Threshold overrides keyed by stable rule identifier.</summary>
    public IReadOnlyDictionary<string, int> ThresholdOverrides { get; set; } =
        new Dictionary<string, int>();
    /// <summary>Predicate-based finding suppressions.</summary>
    public IReadOnlyList<EventDetectionSuppression> Suppressions { get; set; } = Array.Empty<EventDetectionSuppression>();
}

/// <summary>Suppresses matching observations for one rule without changing signed rule content.</summary>
public sealed class EventDetectionSuppression {
    /// <summary>Stable rule identifier.</summary>
    public string RuleId { get; set; } = string.Empty;
    /// <summary>Predicate selecting observations to suppress.</summary>
    public EventPredicate Predicate { get; set; } = null!;
    /// <summary>Optional UTC start of the suppression window.</summary>
    public DateTime? StartTimeUtc { get; set; }
    /// <summary>Optional UTC end of the suppression window.</summary>
    public DateTime? EndTimeUtc { get; set; }
    /// <summary>Operator-facing reason.</summary>
    public string Reason { get; set; } = string.Empty;
}
