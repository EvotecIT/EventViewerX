using System.Text.Json;
using System.Text.Json.Serialization;

namespace EventViewerX;

/// <summary>Canonical versioned JSON projections for public analysis workflows.</summary>
public static class EventAnalysisJson {
    private static readonly JsonSerializerOptions Compact = CreateSerializerOptions(false);
    private static readonly JsonSerializerOptions Indented = CreateSerializerOptions(true);

    /// <summary>Serializes one canonical observation without embedding its runtime source object.</summary>
    public static string Serialize(EventObservation observation, bool indented = false) {
        if (observation == null) {
            throw new ArgumentNullException(nameof(observation));
        }
        return JsonSerializer.Serialize(new {
            SchemaVersion = EventAnalysisContractCatalog.CurrentSchemaVersion,
            observation.Identity,
            observation.TypeName,
            observation.EventId,
            observation.RecordId,
            observation.ProviderName,
            observation.SourceLog,
            observation.ContainerLog,
            observation.SourceComputer,
            observation.CollectorComputer,
            observation.EventTimeUtc,
            observation.ReceivedTimeUtc,
            observation.ProcessedTimeUtc,
            observation.Fields
        }, Options(indented));
    }

    /// <summary>Serializes one finding with stable evidence identities and coverage, excluding duplicate source objects.</summary>
    public static string Serialize(EventDetectionFinding finding, bool indented = false) {
        if (finding == null) {
            throw new ArgumentNullException(nameof(finding));
        }
        return JsonSerializer.Serialize(new {
            SchemaVersion = EventAnalysisContractCatalog.CurrentSchemaVersion,
            finding.RuleId,
            finding.RuleVersion,
            finding.PackId,
            finding.PackVersion,
            finding.SourceKind,
            finding.SourceId,
            finding.SourceStatus,
            finding.SourceHash,
            finding.License,
            finding.Title,
            finding.Severity,
            finding.Confidence,
            finding.Status,
            finding.StartTimeUtc,
            finding.EndTimeUtc,
            finding.EvidenceIdentities,
            finding.Tags,
            finding.FalsePositives,
            finding.References,
            finding.Entities,
            Coverage = ParseElement(finding.Coverage.ToJson()),
            finding.Explanation,
            finding.CompletenessDiagnostic
        }, Options(indented));
    }

    /// <summary>Serializes one compiled plan explanation.</summary>
    public static string Serialize(EventDetectionPlan plan, bool indented = false) {
        if (plan == null) {
            throw new ArgumentNullException(nameof(plan));
        }
        EventDetectionPlanExplanation explanation = plan.Explain();
        return JsonSerializer.Serialize(new {
            SchemaVersion = EventAnalysisContractCatalog.CurrentSchemaVersion,
            explanation.PlanHash,
            explanation.RuleCount,
            explanation.StatefulRuleCount,
            explanation.RequiredEventTypes,
            explanation.Rules
        }, Options(indented));
    }

    /// <summary>Serializes one per-rule explainability trace.</summary>
    public static string Serialize(EventDetectionRuleTrace trace, bool indented = false) {
        if (trace == null) {
            throw new ArgumentNullException(nameof(trace));
        }
        return JsonSerializer.Serialize(new {
            SchemaVersion = EventAnalysisContractCatalog.CurrentSchemaVersion,
            trace.RuleId,
            trace.Title,
            trace.ObservationIdentity,
            trace.Outcome,
            trace.MatchingSteps,
            trace.Conditions
        }, Options(indented));
    }

    private static JsonSerializerOptions Options(bool indented) => indented
        ? Indented
        : Compact;

    internal static JsonSerializerOptions CreateSerializerOptions(bool indented = false) {
        var options = new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = null,
            PropertyNameCaseInsensitive = true,
            WriteIndented = indented
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static JsonElement ParseElement(string json) {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
