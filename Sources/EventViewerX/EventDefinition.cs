using System.Text.Json;
using System.Text.Json.Serialization;

namespace EventViewerX;

/// <summary>A declarative, serializable event definition for workflows not built into EventViewerX.</summary>
public sealed class EventDefinition {
    /// <summary>Reserved JSON envelope used for EventViewerX output metadata.</summary>
    public const string OutputMetadataFieldName = "_EventViewerX";

    private static readonly JsonSerializerOptions ReadOptions = CreateJsonOptions(writeIndented: false);
    private static readonly JsonSerializerOptions WriteOptions = CreateJsonOptions(writeIndented: true);
    private static readonly HashSet<string> ReservedNames = new(
        Enum.GetNames(typeof(EventType)).Concat(new[] {
            "Generic",
            "EventStoreSummary",
            "EventAggregation",
            "EventOccurrence",
            "GroupPolicyAudit"
        }),
        StringComparer.OrdinalIgnoreCase);

    /// <summary>Stable machine-readable name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Human-readable name.</summary>
    public string DisplayName { get; set; } = string.Empty;
    /// <summary>Definition purpose.</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>Grouping label.</summary>
    public string Category { get; set; } = "Custom";
    /// <summary>Source channels, event IDs, and optional providers.</summary>
    public IReadOnlyList<EventDefinitionSource> Sources { get; set; } = Array.Empty<EventDefinitionSource>();
    /// <summary>Projected output fields.</summary>
    public IReadOnlyList<EventDefinitionField> Fields { get; set; } = Array.Empty<EventDefinitionField>();

    /// <summary>Loads and validates a JSON definition.</summary>
    public static EventDefinition Load(string path) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Definition path cannot be empty.", nameof(path));
        }
        string json = File.ReadAllText(Path.GetFullPath(path));
        EventDefinition? definition = JsonSerializer.Deserialize<EventDefinition>(json, ReadOptions);
        if (definition == null) {
            throw new InvalidDataException("The definition JSON did not contain an object.");
        }
        definition.Validate();
        return definition;
    }

    /// <summary>Saves a validated JSON definition.</summary>
    public void Save(string path, bool indented = true) {
        Validate();
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory!);
        }
        File.WriteAllText(fullPath, JsonSerializer.Serialize(this, indented ? WriteOptions : ReadOptions));
    }

    /// <summary>Validates the declarative contract.</summary>
    public void Validate() {
        if (string.IsNullOrWhiteSpace(Name)) {
            throw new InvalidDataException("Definition Name is required.");
        }
        Name = Name.Trim();
        DisplayName = DisplayName?.Trim() ?? string.Empty;
        Description = Description?.Trim() ?? string.Empty;
        Category = string.IsNullOrWhiteSpace(Category) ? "Custom" : Category.Trim();
        if (ReservedNames.Contains(Name)) {
            throw new InvalidDataException(
                $"Definition Name '{Name}' is reserved by a built-in EventViewerX event type.");
        }
        if (Sources == null || Sources.Count == 0) {
            throw new InvalidDataException("At least one definition source is required.");
        }
        for (int index = 0; index < Sources.Count; index++) {
            EventDefinitionSource source = Sources[index] ?? throw new InvalidDataException($"Sources[{index}] cannot be null.");
            if (string.IsNullOrWhiteSpace(source.LogName)) {
                throw new InvalidDataException($"Sources[{index}].LogName is required.");
            }
            source.LogName = source.LogName.Trim();
            if (source.EventIds == null || source.EventIds.Count == 0 || source.EventIds.Any(static id => id <= 0)) {
                throw new InvalidDataException($"Sources[{index}].EventIds must contain positive values.");
            }
            if (source.ProviderNames == null || source.ProviderNames.Any(static provider => string.IsNullOrWhiteSpace(provider))) {
                throw new InvalidDataException($"Sources[{index}].ProviderNames cannot be null or contain empty values.");
            }
            source.ProviderNames = source.ProviderNames.Select(static provider => provider.Trim()).ToArray();
        }
        if (Fields == null) {
            throw new InvalidDataException("Definition Fields cannot be null.");
        }
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < Fields.Count; index++) {
            EventDefinitionField field = Fields[index] ?? throw new InvalidDataException($"Fields[{index}] cannot be null.");
            if (string.IsNullOrWhiteSpace(field.Name)) {
                throw new InvalidDataException($"Fields[{index}].Name must be non-empty and unique.");
            }
            field.Name = field.Name.Trim();
            field.DisplayName = field.DisplayName?.Trim() ?? string.Empty;
            field.Description = field.Description?.Trim() ?? string.Empty;
            if (string.Equals(field.Name, OutputMetadataFieldName, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidDataException(
                    $"Fields[{index}].Name '{field.Name}' is reserved for EventViewerX output metadata.");
            }
            if (!names.Add(field.Name)) {
                throw new InvalidDataException($"Fields[{index}].Name must be non-empty and unique.");
            }
            if (!Enum.IsDefined(typeof(EventFieldSource), field.Source)) {
                throw new InvalidDataException($"Fields[{index}].Source is invalid.");
            }
            if (!Enum.IsDefined(typeof(EventFieldValueKind), field.ValueKind)) {
                throw new InvalidDataException($"Fields[{index}].ValueKind is invalid.");
            }
            if (field.Aliases == null || field.Aliases.Any(static alias => string.IsNullOrWhiteSpace(alias))) {
                throw new InvalidDataException($"Fields[{index}].Aliases cannot be null or contain empty values.");
            }
            field.Aliases = field.Aliases.Select(static alias => alias.Trim()).ToArray();
            foreach (string alias in field.Aliases) {
                if (string.Equals(alias, OutputMetadataFieldName, StringComparison.OrdinalIgnoreCase)) {
                    throw new InvalidDataException(
                        $"Fields[{index}].Aliases contains '{alias}', which is reserved for EventViewerX output metadata.");
                }
                if (!names.Add(alias)) {
                    throw new InvalidDataException($"Fields[{index}].Aliases must be unique across field names and aliases.");
                }
            }
            if (field.Source != EventFieldSource.Message && string.IsNullOrWhiteSpace(field.SourceName)) {
                throw new InvalidDataException($"Fields[{index}].SourceName is required for {field.Source} fields.");
            }
            if (field.Source is not EventFieldSource.Message and not EventFieldSource.Constant) {
                field.SourceName = field.SourceName.Trim();
            }
            if (field.Source == EventFieldSource.Constant) {
                ValidateConfiguredLiteral(field, field.SourceName, nameof(field.SourceName), index);
            }
            if (field.DefaultValue != null) {
                ValidateConfiguredLiteral(field, field.DefaultValue, nameof(field.DefaultValue), index);
            }
        }
    }

    private static void ValidateConfiguredLiteral(
        EventDefinitionField field,
        string value,
        string propertyName,
        int index) {

        try {
            field.ConvertValue(value);
        } catch (InvalidDataException exception) {
            throw new InvalidDataException(
                $"Fields[{index}].{propertyName} is not valid for {field.ValueKind}.",
                exception);
        }
    }

    private static JsonSerializerOptions CreateJsonOptions(bool writeIndented) {
        var options = new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true,
            WriteIndented = writeIndented
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
