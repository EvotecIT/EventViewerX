namespace EventViewerX;

/// <summary>
/// Describes a built-in typed event contract, including its native sources, output schema, and composite membership.
/// </summary>
public sealed class EventTypeDefinition {
    internal EventTypeDefinition(
        EventType type,
        string displayName,
        string description,
        string category,
        IReadOnlyList<EventSourceDefinition> sources,
        IReadOnlyList<EventFieldDefinition> fields,
        Type? recordType,
        IReadOnlyList<EventType> includedTypes) {

        Type = type;
        Name = type.ToString();
        DisplayName = displayName;
        Description = description;
        Category = category;
        Sources = sources;
        Fields = fields;
        RecordType = recordType;
        IncludedTypes = includedTypes;
    }

    /// <summary>Stable built-in event type.</summary>
    public EventType Type { get; }

    /// <summary>Stable definition name.</summary>
    public string Name { get; }

    /// <summary>Human-friendly definition name.</summary>
    public string DisplayName { get; }

    /// <summary>Short user-facing purpose.</summary>
    public string Description { get; }

    /// <summary>Broad report and discovery category.</summary>
    public string Category { get; }

    /// <summary>Native sources after composite expansion.</summary>
    public IReadOnlyList<EventSourceDefinition> Sources { get; }

    /// <summary>Strongly typed output fields.</summary>
    public IReadOnlyList<EventFieldDefinition> Fields { get; }

    /// <summary>Concrete projected CLR record type for a leaf definition.</summary>
    public Type? RecordType { get; }

    /// <summary>Direct members of a composite definition.</summary>
    public IReadOnlyList<EventType> IncludedTypes { get; }

    /// <summary>Whether this definition combines several leaf definitions.</summary>
    public bool IsComposite => IncludedTypes.Count > 0;

    /// <summary>Fastest read mode that preserves every current built-in typed projection.</summary>
    public EventReadMode RecommendedReadMode => EventReadMode.StructuredDataAndMessage;
}
