using System.Text.Json;

namespace EventViewerX;

public sealed partial class EventDetectionPlan {
    /// <summary>Serializes effective rules and suppressions for reproducible replay.</summary>
    public string ToJson() => JsonSerializer.Serialize(new PlanDocument {
        SchemaVersion = 1, PlanHash = PlanHash,
        Rules = CompiledRules.Select(rule => rule.Definition).ToArray(),
        Suppressions = CompiledRules.SelectMany(rule => rule.SuppressionDefinitions).ToArray()
    }, EventAnalysisJson.CreateSerializerOptions());

    /// <summary>Restores an effective plan and rejects unsupported or inconsistent identities.</summary>
    public static EventDetectionPlan FromJson(string json) {
        PlanDocument document = JsonSerializer.Deserialize<PlanDocument>(json, EventAnalysisJson.CreateSerializerOptions())
            ?? throw new InvalidDataException("Missing detection plan.");
        if (document.SchemaVersion != 1 || document.Rules == null || document.Suppressions == null) {
            throw new InvalidDataException("Unsupported detection plan document.");
        }
        EventDetectionPlan plan = Compile(document.Rules.Select(rule => new EventDetectionRule(rule)),
            new EventDetectionTuning(suppressions: document.Suppressions));
        if (!string.Equals(plan.PlanHash, document.PlanHash, StringComparison.Ordinal)) {
            throw new InvalidDataException("Detection plan identity does not match its effective rules and suppressions.");
        }
        return plan;
    }

    private sealed class PlanDocument {
        public int SchemaVersion { get; set; }
        public string PlanHash { get; set; } = string.Empty;
        public EventDetectionRuleDefinition[] Rules { get; set; } = Array.Empty<EventDetectionRuleDefinition>();
        public EventDetectionSuppression[] Suppressions { get; set; } = Array.Empty<EventDetectionSuppression>();
    }
}
