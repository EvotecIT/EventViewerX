namespace EventViewerX.Providers;

/// <summary>One versioned event declared by a manifest-based provider.</summary>
public sealed class EventProviderEventDefinition {
    /// <summary>Friendly and symbolic event name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Event identifier from 0 through 65535.</summary>
    public int Id { get; set; }

    /// <summary>
    /// Schema version. Increment this value whenever the ordered payload schema
    /// changes.
    /// </summary>
    public byte Version { get; set; }

    /// <summary>Provider-local channel identifier.</summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>
    /// Standard level such as <c>win:Informational</c>, or a custom level name.
    /// </summary>
    public string Level { get; set; } = "win:Informational";

    /// <summary>Optional provider task name.</summary>
    public string Task { get; set; } = string.Empty;

    /// <summary>Optional provider or task opcode name.</summary>
    public string Opcode { get; set; } = string.Empty;

    /// <summary>Keyword names combined into the event descriptor.</summary>
    public List<string> Keywords { get; set; } = new();

    /// <summary>Ordered fields in the event payload.</summary>
    public List<EventProviderFieldDefinition> Fields { get; set; } = new();

    /// <summary>
    /// Localized event messages keyed by culture. Named placeholders such as
    /// <c>{ComputerName}</c> are compiled to Windows insertion strings.
    /// </summary>
    public Dictionary<string, string> Messages { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates a new versioned event.</summary>
    public static EventProviderEventDefinition Create(
        string name,
        int id,
        string channel,
        byte version = 0) {

        return new EventProviderEventDefinition {
            Name = name,
            Id = id,
            Channel = channel,
            Version = version
        };
    }

    /// <summary>Adds a payload field and returns this event for fluent setup.</summary>
    public EventProviderEventDefinition AddField(
        EventProviderFieldDefinition field) {

        Fields.Add(field ??
                   throw new ArgumentNullException(nameof(field)));
        return this;
    }

    /// <summary>
    /// Creates an event whose ordered fields are inferred from a typed payload.
    /// </summary>
    public static EventProviderEventDefinition FromType<TPayload>(
        string name,
        int id,
        string channel,
        byte version = 0) {

        EventProviderEventDefinition definition =
            Create(name, id, channel, version);
        definition.Fields.AddRange(
            EventProviderTypedPayload.Describe(typeof(TPayload)));
        return definition;
    }
}
