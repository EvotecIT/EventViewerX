namespace EventViewerX.Providers;

/// <summary>
/// Complete, serializable definition of one manifest-based Windows event
/// provider and the package used to deploy it.
/// </summary>
public sealed class EventProviderDefinition {
    /// <summary>Stable provider name displayed by Windows.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Stable provider GUID. Changing this value creates a different provider.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>C-compatible provider symbol. A safe symbol is generated when omitted.</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Version of the deployable provider package.</summary>
    public string PackageVersion { get; set; } = "1.0.0";

    /// <summary>Culture used when a localized value is not supplied.</summary>
    public string DefaultCulture { get; set; } = "en-US";

    /// <summary>Localized provider display names keyed by culture.</summary>
    public Dictionary<string, string> DisplayNames { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Localized provider descriptions keyed by culture.</summary>
    public Dictionary<string, string> Descriptions { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Channels owned or referenced by the provider.</summary>
    public List<EventProviderChannelDefinition> Channels { get; set; } = new();

    /// <summary>Custom provider levels.</summary>
    public List<EventProviderLevelDefinition> Levels { get; set; } = new();

    /// <summary>Provider tasks.</summary>
    public List<EventProviderTaskDefinition> Tasks { get; set; } = new();

    /// <summary>Provider-wide custom opcodes.</summary>
    public List<EventProviderOpcodeDefinition> Opcodes { get; set; } = new();

    /// <summary>Provider keywords.</summary>
    public List<EventProviderKeywordDefinition> Keywords { get; set; } = new();

    /// <summary>Value and bit maps used by payload fields.</summary>
    public List<EventProviderMapDefinition> Maps { get; set; } = new();

    /// <summary>Events declared by the provider.</summary>
    public List<EventProviderEventDefinition> Events { get; set; } = new();

    /// <summary>Creates a definition with a stable name and identifier.</summary>
    public static EventProviderDefinition Create(
        string name,
        Guid id,
        string packageVersion = "1.0.0") {

        return new EventProviderDefinition {
            Name = name,
            Id = id,
            PackageVersion = packageVersion
        };
    }

    /// <summary>Adds a channel and returns this definition for fluent setup.</summary>
    public EventProviderDefinition AddChannel(
        EventProviderChannelDefinition channel) {

        Channels.Add(channel ??
                     throw new ArgumentNullException(nameof(channel)));
        return this;
    }

    /// <summary>Adds an event and returns this definition for fluent setup.</summary>
    public EventProviderDefinition AddEvent(
        EventProviderEventDefinition eventDefinition) {

        Events.Add(eventDefinition ??
                   throw new ArgumentNullException(nameof(eventDefinition)));
        return this;
    }
}
