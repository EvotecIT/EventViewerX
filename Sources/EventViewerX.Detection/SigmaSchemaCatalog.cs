using System.Text.Json;
using Json.Schema;
using YamlDotNet.RepresentationModel;

namespace EventViewerX.Sigma;

/// <summary>Bundled JSON Schema contracts for the losslessly supported Sigma 2.1 profile.</summary>
public static class SigmaSchemaCatalog {
    /// <summary>Upstream Sigma specification version implemented by this adapter.</summary>
    public const string SupportedSpecificationVersion = "2.1.0";

    private const string DetectionSchemaResource = "EventViewerX.Detection.Schemas.sigma-detection-rule-2.1.0.json";
    private const string CorrelationSchemaResource = "EventViewerX.Detection.Schemas.sigma-correlation-rule-2.1.0.json";
    private static readonly string DetectionSchemaJson = ReadSchema(DetectionSchemaResource);
    private static readonly string CorrelationSchemaJson = ReadSchema(CorrelationSchemaResource);
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

    private static string ReadSchema(string resourceName) {
        using Stream stream = typeof(SigmaSchemaCatalog).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The bundled Sigma schema resource '{resourceName}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
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

}
