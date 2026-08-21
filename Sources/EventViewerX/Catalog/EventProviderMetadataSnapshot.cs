namespace EventViewerX;

/// <summary>Fully detached provider metadata suitable for C#, PowerShell, serialization, and caching.</summary>
public sealed class EventProviderMetadataSnapshot {
    internal EventProviderMetadataSnapshot(
        string name,
        Guid id,
        string displayName,
        string messageFilePath,
        string resourceFilePath,
        string parameterFilePath,
        Uri? helpLink,
        IReadOnlyList<EventProviderLogLink> logLinks,
        IReadOnlyList<EventProviderValue> levels,
        IReadOnlyList<EventProviderValue> tasks,
        IReadOnlyList<EventProviderValue> opcodes,
        IReadOnlyList<EventProviderValue> keywords,
        IReadOnlyList<EventProviderEventMetadataSnapshot> events,
        IReadOnlyList<string> diagnostics) {

        Name = name;
        Id = id;
        DisplayName = displayName;
        MessageFilePath = messageFilePath;
        ResourceFilePath = resourceFilePath;
        ParameterFilePath = parameterFilePath;
        HelpLink = helpLink;
        LogLinks = logLinks;
        Levels = levels;
        Tasks = tasks;
        Opcodes = opcodes;
        Keywords = keywords;
        Events = events;
        Diagnostics = diagnostics;
    }

    /// <summary>Provider name.</summary>
    public string Name { get; }
    /// <summary>Provider GUID.</summary>
    public Guid Id { get; }
    /// <summary>Localized display name.</summary>
    public string DisplayName { get; }
    /// <summary>Message resource file path.</summary>
    public string MessageFilePath { get; }
    /// <summary>General resource file path.</summary>
    public string ResourceFilePath { get; }
    /// <summary>Parameter resource file path.</summary>
    public string ParameterFilePath { get; }
    /// <summary>Provider help link.</summary>
    public Uri? HelpLink { get; }
    /// <summary>Channels linked by the provider manifest.</summary>
    public IReadOnlyList<EventProviderLogLink> LogLinks { get; }
    /// <summary>Provider levels.</summary>
    public IReadOnlyList<EventProviderValue> Levels { get; }
    /// <summary>Provider tasks.</summary>
    public IReadOnlyList<EventProviderValue> Tasks { get; }
    /// <summary>Provider opcodes.</summary>
    public IReadOnlyList<EventProviderValue> Opcodes { get; }
    /// <summary>Provider keywords.</summary>
    public IReadOnlyList<EventProviderValue> Keywords { get; }
    /// <summary>Event definitions when IncludeEvents was requested.</summary>
    public IReadOnlyList<EventProviderEventMetadataSnapshot> Events { get; }
    /// <summary>Non-fatal metadata properties that could not be materialized.</summary>
    public IReadOnlyList<string> Diagnostics { get; }
}
