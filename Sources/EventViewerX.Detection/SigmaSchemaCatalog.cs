using System.Text.Json;
using Json.Schema;
using YamlDotNet.RepresentationModel;

namespace EventViewerX.Sigma;

/// <summary>Bundled JSON Schema contracts for the losslessly supported Sigma 2.1 profile.</summary>
public static class SigmaSchemaCatalog {
    /// <summary>Upstream Sigma specification version implemented by this adapter.</summary>
    public const string SupportedSpecificationVersion = "2.1.0";

    private static readonly JsonSchema DetectionSchema = Build(DetectionSchemaJson);
    private static readonly JsonSchema CorrelationSchema = Build(CorrelationSchemaJson);

    /// <summary>Returns the bundled detection or correlation JSON Schema.</summary>
    public static string GetSchema(bool correlation) => correlation ? CorrelationSchemaJson : DetectionSchemaJson;

    internal static bool Validate(YamlMappingNode root, bool correlation) {
        object? instance = ConvertNode(root);
        JsonElement json = JsonSerializer.SerializeToElement(instance);
        EvaluationResults result = (correlation ? CorrelationSchema : DetectionSchema).Evaluate(json);
        return result.IsValid;
    }

    private static JsonSchema Build(string json) {
        using JsonDocument document = JsonDocument.Parse(json);
        return JsonSchema.Build(document.RootElement.Clone(), new BuildOptions { Dialect = Dialect.Draft202012 });
    }

    private static object? ConvertNode(YamlNode node) {
        if (node is YamlMappingNode mapping) {
            return mapping.Children.ToDictionary(
                static item => ((YamlScalarNode)item.Key).Value ?? string.Empty,
                static item => ConvertNode(item.Value),
                StringComparer.OrdinalIgnoreCase);
        }
        if (node is YamlSequenceNode sequence) {
            return sequence.Children.Select(ConvertNode).ToArray();
        }
        if (node is not YamlScalarNode scalar) {
            return null;
        }
        string value = scalar.Value ?? string.Empty;
        if (string.Equals(value, "null", StringComparison.OrdinalIgnoreCase) || value == "~") {
            return null;
        }
        if (bool.TryParse(value, out bool boolean)) {
            return boolean;
        }
        if (long.TryParse(value, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out long integer)) {
            return integer;
        }
        return value;
    }

    private const string DetectionSchemaJson = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "EventViewerX supported Sigma detection profile 2.1.0",
          "type": "object",
          "required": ["title", "logsource", "detection"],
          "properties": {
            "title": { "type": "string", "minLength": 1, "maxLength": 256 },
            "id": { "type": "string", "minLength": 1, "maxLength": 200 },
            "name": { "type": "string", "minLength": 1, "maxLength": 200 },
            "status": { "enum": ["stable", "test", "experimental", "deprecated", "unsupported"] },
            "description": { "type": "string", "maxLength": 65535 },
            "license": { "type": "string" },
            "references": { "type": "array", "items": { "type": "string" }, "uniqueItems": true },
            "tags": { "type": "array", "items": { "type": "string" }, "uniqueItems": true },
            "falsepositives": { "type": "array", "items": { "type": "string" } },
            "level": { "enum": ["informational", "low", "medium", "high", "critical"] },
            "logsource": {
              "type": "object",
              "minProperties": 1,
              "properties": {
                "product": { "type": "string" },
                "category": { "type": "string" },
                "service": { "type": "string" },
                "definition": { "type": "string" }
              },
              "additionalProperties": true
            },
            "detection": {
              "type": "object",
              "required": ["condition"],
              "minProperties": 2,
              "properties": {
                "condition": { "type": "string", "minLength": 1 },
                "timeframe": { "type": "string" }
              },
              "additionalProperties": true
            }
          },
          "additionalProperties": true
        }
        """;

    private const string CorrelationSchemaJson = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "EventViewerX supported Sigma correlation profile 2.1.0",
          "type": "object",
          "required": ["title", "correlation"],
          "properties": {
            "title": { "type": "string", "minLength": 1, "maxLength": 256 },
            "id": { "type": "string", "minLength": 1, "maxLength": 200 },
            "status": { "enum": ["stable", "test", "experimental", "deprecated", "unsupported"] },
            "description": { "type": "string", "maxLength": 65535 },
            "references": { "type": "array", "items": { "type": "string" }, "uniqueItems": true },
            "tags": { "type": "array", "items": { "type": "string" }, "uniqueItems": true },
            "falsepositives": { "type": "array", "items": { "type": "string" } },
            "level": { "enum": ["informational", "low", "medium", "high", "critical"] },
            "correlation": {
              "type": "object",
              "required": ["type", "rules", "timespan"],
              "properties": {
                "type": { "enum": ["event_count", "value_count", "temporal", "temporal_ordered"] },
                "rules": {
                  "type": "array",
                  "minItems": 1,
                  "uniqueItems": true,
                  "items": { "type": "string", "minLength": 1 }
                },
                "group-by": {
                  "type": "array",
                  "maxItems": 1,
                  "uniqueItems": true,
                  "items": { "type": "string", "minLength": 1 }
                },
                "timespan": { "type": "string", "pattern": "^[1-9][0-9]*[smhdwM]$", "maxLength": 10 },
                "condition": { "type": "object" },
                "generate": { "type": "boolean" }
              },
              "not": { "required": ["aliases"] },
              "additionalProperties": true
            }
          },
          "additionalProperties": true
        }
        """;
}
