namespace EventViewerX;

/// <summary>Requirements for one built-in leaf or composite event type.</summary>
public sealed class EventTypeRequirement {
    internal EventTypeRequirement(
        EventType type,
        IReadOnlyList<EventSourceDefinition> sources,
        IReadOnlyList<EventPrerequisite> prerequisites,
        IReadOnlyList<EventType> includedTypes) {

        Type = type;
        Name = type.ToString();
        Sources = sources;
        Prerequisites = prerequisites;
        IncludedTypes = includedTypes;
    }

    /// <summary>Built-in event type.</summary>
    public EventType Type { get; }
    /// <summary>Stable event type name.</summary>
    public string Name { get; }
    /// <summary>Native log names and event IDs required by the type.</summary>
    public IReadOnlyList<EventSourceDefinition> Sources { get; }
    /// <summary>Distinct channel, audit, configuration, and volume prerequisites.</summary>
    public IReadOnlyList<EventPrerequisite> Prerequisites { get; }
    /// <summary>Expanded leaf types represented by this result.</summary>
    public IReadOnlyList<EventType> IncludedTypes { get; }
    /// <summary>Whether the type combines multiple leaf definitions.</summary>
    public bool IsComposite => IncludedTypes.Count > 1 || IncludedTypes.Count == 1 && IncludedTypes[0] != Type;
}
