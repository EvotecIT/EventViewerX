namespace EventViewerX.Providers;

/// <summary>One ordered and typed field in an event payload template.</summary>
public sealed class EventProviderFieldDefinition {
    /// <summary>Field name written into structured EventData.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Native Windows input type.</summary>
    public EventProviderFieldType Type { get; set; } =
        EventProviderFieldType.UnicodeString;

    /// <summary>Optional rendering hint.</summary>
    public EventProviderFieldOutputType OutputType { get; set; }

    /// <summary>Optional custom output type for advanced manifests.</summary>
    public string CustomOutputType { get; set; } = string.Empty;

    /// <summary>Optional value-map or bit-map name.</summary>
    public string Map { get; set; } = string.Empty;

    /// <summary>
    /// Optional fixed length or the name of an earlier numeric length field.
    /// </summary>
    public string Length { get; set; } = string.Empty;

    /// <summary>
    /// Optional fixed count or the name of an earlier numeric count field.
    /// </summary>
    public string Count { get; set; } = string.Empty;

    /// <summary>Creates a typed payload field.</summary>
    public static EventProviderFieldDefinition Create(
        string name,
        EventProviderFieldType type) {

        return new EventProviderFieldDefinition {
            Name = name,
            Type = type
        };
    }
}
