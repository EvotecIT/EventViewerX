using System.Collections.ObjectModel;
using System.Text.Json;

namespace EventViewerX;

/// <summary>Stable public analysis document kinds.</summary>
public enum EventAnalysisContractKind {
    /// <summary>Canonical event observation.</summary>
    Observation,
    /// <summary>Detection finding and evidence identities.</summary>
    Finding,
    /// <summary>Expected-versus-observed collection coverage.</summary>
    Coverage,
    /// <summary>Compiled detection plan explanation.</summary>
    Plan,
    /// <summary>Versioned detection pack.</summary>
    Pack,
    /// <summary>Per-rule explainability trace.</summary>
    RuleTrace
}

/// <summary>Version and JSON Schema for one public analysis contract.</summary>
public sealed class EventAnalysisContractDescriptor {
    internal EventAnalysisContractDescriptor(EventAnalysisContractKind kind, int schemaVersion, string jsonSchema) {
        Kind = kind;
        SchemaVersion = schemaVersion;
        JsonSchema = jsonSchema;
    }

    /// <summary>Document kind.</summary>
    public EventAnalysisContractKind Kind { get; }
    /// <summary>Integer schema version embedded in serialized documents where applicable.</summary>
    public int SchemaVersion { get; }
    /// <summary>Draft 2020-12 JSON Schema.</summary>
    public string JsonSchema { get; }
}

/// <summary>Versioned JSON contracts for observations, findings, coverage, plans, packs, and traces.</summary>
public static class EventAnalysisContractCatalog {
    /// <summary>Current analysis document version.</summary>
    public const int CurrentSchemaVersion = 1;

    private static readonly IReadOnlyDictionary<EventAnalysisContractKind, EventAnalysisContractDescriptor> Contracts =
        new ReadOnlyDictionary<EventAnalysisContractKind, EventAnalysisContractDescriptor>(
            Enum.GetValues(typeof(EventAnalysisContractKind))
                .Cast<EventAnalysisContractKind>()
                .ToDictionary(
                    static kind => kind,
                    static kind => new EventAnalysisContractDescriptor(kind, CurrentSchemaVersion, CreateSchema(kind))));

    /// <summary>Returns every supported contract in stable enum order.</summary>
    public static IReadOnlyList<EventAnalysisContractDescriptor> GetContracts() =>
        Contracts.OrderBy(static item => item.Key).Select(static item => item.Value).ToArray();

    /// <summary>Returns one contract descriptor.</summary>
    public static EventAnalysisContractDescriptor Get(EventAnalysisContractKind kind) =>
        Contracts.TryGetValue(kind, out EventAnalysisContractDescriptor? contract)
            ? contract
            : throw new ArgumentOutOfRangeException(nameof(kind));

    private static string CreateSchema(EventAnalysisContractKind kind) {
        string[] required = kind switch {
            EventAnalysisContractKind.Observation => new[] {
                "schemaVersion", "identity", "typeName", "eventId", "providerName", "sourceLog",
                "sourceComputer", "eventTimeUtc", "receivedTimeUtc", "processedTimeUtc", "fields"
            },
            EventAnalysisContractKind.Finding => new[] {
                "schemaVersion", "ruleId", "ruleVersion", "title", "severity", "confidence", "status",
                "startTimeUtc", "endTimeUtc", "evidenceIdentities", "coverage", "explanation"
            },
            EventAnalysisContractKind.Coverage => new[] { "schemaVersion", "isDeclared", "failures" },
            EventAnalysisContractKind.Plan => new[] {
                "schemaVersion", "planHash", "ruleCount", "statefulRuleCount", "requiredEventTypes", "rules"
            },
            EventAnalysisContractKind.Pack => new[] {
                "packId", "version", "minimumEngineVersion", "observationSchemaVersion", "contentHash", "rules"
            },
            EventAnalysisContractKind.RuleTrace => new[] {
                "schemaVersion", "ruleId", "observationIdentity", "outcome", "conditions"
            },
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var properties = required.ToDictionary(
            static name => name,
            static name => (object)new Dictionary<string, object> {
                ["type"] = name switch {
                    "schemaVersion" or "eventId" or "confidence" or "ruleCount" or "statefulRuleCount" => "integer",
                    "isDeclared" => "boolean",
                    "fields" or "coverage" => "object",
                    "rules" or "conditions" or "evidenceIdentities" or "failures" or "requiredEventTypes" => "array",
                    _ => "string"
                }
            },
            StringComparer.Ordinal);
        var schema = new Dictionary<string, object> {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["$id"] = "https://schemas.evotec.xyz/eventviewerx/analysis/v1/" + kind.ToString().ToLowerInvariant() + ".schema.json",
            ["title"] = "EventViewerX " + kind,
            ["type"] = "object",
            ["required"] = required,
            ["properties"] = properties,
            ["additionalProperties"] = true
        };
        return JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true });
    }
}
