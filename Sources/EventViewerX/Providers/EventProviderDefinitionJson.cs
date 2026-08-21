using System.Text.Json;
using System.Text.Json.Serialization;

namespace EventViewerX.Providers;

/// <summary>Reads and writes portable event-provider definition files.</summary>
public static class EventProviderDefinitionJson {
    private static readonly JsonSerializerOptions Options = CreateOptions();

    /// <summary>Serializes a provider definition as stable, readable JSON.</summary>
    public static string Serialize(EventProviderDefinition definition) {
        EventProviderDefinitionValidator.ValidateOrThrow(definition);
        return JsonSerializer.Serialize(
            definition,
            Options);
    }

    /// <summary>Deserializes and validates a provider definition.</summary>
    public static EventProviderDefinition Parse(string json) {
        if (string.IsNullOrWhiteSpace(json)) {
            throw new ArgumentException(
                "Provider definition JSON cannot be empty.",
                nameof(json));
        }
        EventProviderDefinition definition =
            JsonSerializer.Deserialize<EventProviderDefinition>(
                json,
                Options) ??
            throw new InvalidDataException(
                "Provider definition JSON did not produce a definition.");
        EventProviderDefinitionValidator.ValidateOrThrow(definition);
        return definition;
    }

    /// <summary>Loads and validates a UTF-8 JSON definition file.</summary>
    public static EventProviderDefinition Load(string path) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException(
                "Provider definition path cannot be empty.",
                nameof(path));
        }
        return Parse(File.ReadAllText(path, Encoding.UTF8));
    }

    /// <summary>Writes a validated UTF-8 JSON definition file atomically.</summary>
    public static void Save(
        EventProviderDefinition definition,
        string path,
        bool overwrite = false) {

        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException(
                "Provider definition path cannot be empty.",
                nameof(path));
        }
        string fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath) && !overwrite) {
            throw new IOException(
                $"File '{fullPath}' already exists.");
        }
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }
        string temporaryPath = fullPath + "." +
                               Guid.NewGuid().ToString("N") +
                               ".tmp";
        try {
            File.WriteAllText(
                temporaryPath,
                Serialize(definition),
                new UTF8Encoding(false));
            PromoteTemporaryFile(
                temporaryPath,
                fullPath,
                overwrite);
        } finally {
            DeleteTemporaryBestEffort(
                temporaryPath);
        }
    }

    internal static JsonSerializerOptions SerializerOptions => Options;

    /// <summary>
    /// Promotes a fully written temporary definition while preserving the
    /// caller's overwrite contract at the atomic filesystem boundary.
    /// </summary>
    internal static void PromoteTemporaryFile(
        string temporaryPath,
        string fullPath,
        bool overwrite) {

        if (!overwrite) {
            File.Move(temporaryPath, fullPath);
            return;
        }
        if (File.Exists(fullPath)) {
            File.Replace(
                temporaryPath,
                fullPath,
                null);
            return;
        }
        try {
            File.Move(temporaryPath, fullPath);
        } catch (IOException) when (File.Exists(fullPath)) {
            File.Replace(
                temporaryPath,
                fullPath,
                null);
        }
    }

    internal static void DeleteTemporaryBestEffort(
        string temporaryPath,
        Action<string>? deleteFile = null) {

        try {
            (deleteFile ?? File.Delete)(
                temporaryPath);
        } catch (IOException) {
            // Preserve the authoritative serialization, write, or promotion failure.
        } catch (UnauthorizedAccessException) {
            // Preserve the authoritative serialization, write, or promotion failure.
        }
    }

    private static JsonSerializerOptions CreateOptions() {
        var options = new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            UnmappedMemberHandling =
                JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(
            new JsonStringEnumConverter(
                JsonNamingPolicy.CamelCase,
                allowIntegerValues: false));
        return options;
    }
}
