namespace EventViewerX;

internal sealed class ObservationJsonDocument {
    public int SchemaVersion { get; set; } = default!;
    public string Identity { get; set; } = default!;
    public string TypeName { get; set; } = default!;
    public int EventId { get; set; } = default!;
    public long? RecordId { get; set; } = default!;
    public string ProviderName { get; set; } = default!;
    public string SourceLog { get; set; } = default!;
    public string ContainerLog { get; set; } = default!;
    public string SourceComputer { get; set; } = default!;
    public string CollectorComputer { get; set; } = default!;
    public DateTime EventTimeUtc { get; set; } = default!;
    public DateTime ReceivedTimeUtc { get; set; } = default!;
    public DateTime ProcessedTimeUtc { get; set; } = default!;
    public IReadOnlyDictionary<string, object?> Fields { get; set; } = default!;
}

internal sealed class FindingJsonDocument {
    public int SchemaVersion { get; set; } = default!;
    public string RuleId { get; set; } = default!;
    public string RuleVersion { get; set; } = default!;
    public string PackId { get; set; } = default!;
    public string PackVersion { get; set; } = default!;
    public string SourceKind { get; set; } = default!;
    public string SourceId { get; set; } = default!;
    public string SourceStatus { get; set; } = default!;
    public string SourceHash { get; set; } = default!;
    public string License { get; set; } = default!;
    public string Title { get; set; } = default!;
    public EventDetectionSeverity Severity { get; set; } = default!;
    public int Confidence { get; set; } = default!;
    public EventDetectionFindingStatus Status { get; set; } = default!;
    public DateTime StartTimeUtc { get; set; } = default!;
    public DateTime EndTimeUtc { get; set; } = default!;
    public IReadOnlyList<string> EvidenceIdentities { get; set; } = default!;
    public IReadOnlyList<string> Tags { get; set; } = default!;
    public IReadOnlyList<string> FalsePositives { get; set; } = default!;
    public IReadOnlyList<string> References { get; set; } = default!;
    public IReadOnlyDictionary<string, string> Entities { get; set; } = default!;
    public System.Text.Json.JsonElement Coverage { get; set; } = default!;
    public string Explanation { get; set; } = default!;
    public string? CompletenessDiagnostic { get; set; } = default!;
}

internal sealed class PlanExplanationJsonDocument {
    public int SchemaVersion { get; set; } = default!;
    public string PlanHash { get; set; } = default!;
    public int RuleCount { get; set; } = default!;
    public int StatefulRuleCount { get; set; } = default!;
    public IReadOnlyList<EventType> RequiredEventTypes { get; set; } = default!;
    public IReadOnlyList<EventDetectionRulePlanExplanation> Rules { get; set; } = default!;
}

internal sealed class RuleTraceJsonDocument {
    public int SchemaVersion { get; set; } = default!;
    public string RuleId { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string ObservationIdentity { get; set; } = default!;
    public string Outcome { get; set; } = default!;
    public IReadOnlyList<string> MatchingSteps { get; set; } = default!;
    public IReadOnlyList<EventDetectionConditionResult> Conditions { get; set; } = default!;
}

internal sealed class DetectionScopeJsonDocument {
    public string[] RuleChannels { get; set; } = default!;
    public string[] RuleProviders { get; set; } = default!;
    public int[] RuleEventIds { get; set; } = default!;
    public EventType[] RuleEventTypes { get; set; } = default!;
    public string[]? StepChannels { get; set; } = default!;
    public string[]? StepProviders { get; set; } = default!;
    public int[]? StepEventIds { get; set; } = default!;
    public EventType[]? StepEventTypes { get; set; } = default!;
}

internal sealed class PlanHashJsonDocument {
    public int SchemaVersion { get; set; } = default!;
    public EventDetectionRuleDefinition[] Rules { get; set; } = default!;
    public SuppressionHashJsonDocument[] Suppressions { get; set; } = default!;
}

internal sealed class SuppressionHashJsonDocument {
    public string RuleId { get; set; } = default!;
    public EventDetectionSuppression[] Values { get; set; } = default!;
}

internal sealed class EvidencePrivacyJsonDocument {
    public int SchemaVersion { get; set; } = default!;
    public string RowType { get; set; } = default!;
    public IReadOnlyCollection<string> RetainedValueFields { get; set; } = default!;
    public IReadOnlyCollection<string> PseudonymizedValueFields { get; set; } = default!;
    public bool PseudonymizeSourceIdentities { get; set; } = default!;
}
