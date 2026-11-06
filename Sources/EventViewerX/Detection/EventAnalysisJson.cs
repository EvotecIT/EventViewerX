using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace EventViewerX;

/// <summary>Canonical versioned JSON projections for public analysis workflows.</summary>
public static class EventAnalysisJson {
    private static readonly JsonSerializerOptions Compact = CreateSerializerOptions(false);
    private static readonly JsonSerializerOptions Indented = CreateSerializerOptions(true);

    /// <summary>Serializes one canonical observation without embedding its runtime source object.</summary>
#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Caller-owned field values require preserved JSON metadata. Supply generated value contracts to use Native AOT.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Caller-owned field values may require runtime serialization code. Supply generated value contracts to use Native AOT.")]
#endif
    public static string Serialize(EventObservation observation, bool indented = false) =>
        SerializeObservation(observation, EventJson.WithReflectionValues(Options(indented), stringEnums: true));

    /// <summary>Serializes one canonical observation with generated contracts for additional caller-owned field values.</summary>
    /// <param name="observation">Observation to serialize.</param>
    /// <param name="indented">Whether to indent the output.</param>
    /// <param name="valueTypes">Generated contracts for additional field values. An empty collection uses the built-in contracts.</param>
    public static string Serialize(EventObservation observation, bool indented, IReadOnlyCollection<JsonTypeInfo> valueTypes) =>
        SerializeObservation(observation, EventJson.WithValueContracts(Options(indented), valueTypes));

    private static string SerializeObservation(EventObservation observation, JsonSerializerOptions options) {
        if (observation == null) {
            throw new ArgumentNullException(nameof(observation));
        }
        return EventJson.Serialize(new ObservationJsonDocument {
            SchemaVersion = EventAnalysisContractCatalog.CurrentSchemaVersion,
            Identity = observation.Identity,
            TypeName = observation.TypeName,
            EventId = observation.EventId,
            RecordId = observation.RecordId,
            ProviderName = observation.ProviderName,
            SourceLog = observation.SourceLog,
            ContainerLog = observation.ContainerLog,
            SourceComputer = observation.SourceComputer,
            CollectorComputer = observation.CollectorComputer,
            EventTimeUtc = observation.EventTimeUtc,
            ReceivedTimeUtc = observation.ReceivedTimeUtc,
            ProcessedTimeUtc = observation.ProcessedTimeUtc,
            Fields = observation.Fields
        }, options);
    }

    /// <summary>Serializes one finding with stable evidence identities and coverage, excluding duplicate source objects.</summary>
    public static string Serialize(EventDetectionFinding finding, bool indented = false) {
        if (finding == null) {
            throw new ArgumentNullException(nameof(finding));
        }
        return EventJson.Serialize(new FindingJsonDocument {
            SchemaVersion = EventAnalysisContractCatalog.CurrentSchemaVersion,
            RuleId = finding.RuleId,
            RuleVersion = finding.RuleVersion,
            PackId = finding.PackId,
            PackVersion = finding.PackVersion,
            SourceKind = finding.SourceKind,
            SourceId = finding.SourceId,
            SourceStatus = finding.SourceStatus,
            SourceHash = finding.SourceHash,
            License = finding.License,
            Title = finding.Title,
            Severity = finding.Severity,
            Confidence = finding.Confidence,
            Status = finding.Status,
            StartTimeUtc = finding.StartTimeUtc,
            EndTimeUtc = finding.EndTimeUtc,
            EvidenceIdentities = finding.EvidenceIdentities,
            Tags = finding.Tags,
            FalsePositives = finding.FalsePositives,
            References = finding.References,
            Entities = finding.Entities,
            Coverage = ParseElement(finding.Coverage.ToJson()),
            Explanation = finding.Explanation,
            CompletenessDiagnostic = finding.CompletenessDiagnostic
        }, Options(indented));
    }

    /// <summary>Serializes one compiled plan explanation.</summary>
    public static string Serialize(EventDetectionPlan plan, bool indented = false) {
        if (plan == null) {
            throw new ArgumentNullException(nameof(plan));
        }
        EventDetectionPlanExplanation explanation = plan.Explain();
        return EventJson.Serialize(new PlanExplanationJsonDocument {
            SchemaVersion = EventAnalysisContractCatalog.CurrentSchemaVersion,
            PlanHash = explanation.PlanHash,
            RuleCount = explanation.RuleCount,
            StatefulRuleCount = explanation.StatefulRuleCount,
            RequiredEventTypes = explanation.RequiredEventTypes,
            Rules = explanation.Rules
        }, Options(indented));
    }

    /// <summary>Serializes one per-rule explainability trace.</summary>
    public static string Serialize(EventDetectionRuleTrace trace, bool indented = false) {
        if (trace == null) {
            throw new ArgumentNullException(nameof(trace));
        }
        return EventJson.Serialize(new RuleTraceJsonDocument {
            SchemaVersion = EventAnalysisContractCatalog.CurrentSchemaVersion,
            RuleId = trace.RuleId,
            Title = trace.Title,
            ObservationIdentity = trace.ObservationIdentity,
            Outcome = trace.Outcome,
            MatchingSteps = trace.MatchingSteps,
            Conditions = trace.Conditions
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
        EventJson.AddStringEnumConverters(options);
        return options;
    }

    private static JsonElement ParseElement(string json) {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
