using System.Text.Json;

namespace EventViewerX.Providers;

/// <summary>
/// Canonical compatibility-critical snapshot included in every provider package.
/// </summary>
public sealed class EventProviderSchemaLock {
    /// <summary>Stable provider name.</summary>
    public string ProviderName { get; set; } = string.Empty;
    /// <summary>Stable provider GUID.</summary>
    public Guid ProviderId { get; set; }
    /// <summary>Compatibility-critical channel identities.</summary>
    public IReadOnlyList<EventProviderSchemaChannelLock> Channels {
        get;
        set;
    } = Array.Empty<EventProviderSchemaChannelLock>();
    /// <summary>Compatibility-critical event identities and payload schemas.</summary>
    public IReadOnlyList<EventProviderSchemaEventLock> Events {
        get;
        set;
    } = Array.Empty<EventProviderSchemaEventLock>();

    /// <summary>Creates a deterministic lock from a validated definition.</summary>
    public static EventProviderSchemaLock Create(
        EventProviderDefinition definition) {

        EventProviderDefinitionValidator.ValidateOrThrow(definition);
        return new EventProviderSchemaLock {
            ProviderName = definition.Name,
            ProviderId = definition.Id,
            Channels = definition.Channels
                .OrderBy(static channel => channel.Id, StringComparer.Ordinal)
                .Select(static channel =>
                    new EventProviderSchemaChannelLock {
                        Id = channel.Id,
                        Name = channel.Name,
                        Type = channel.Type,
                        Isolation = channel.Isolation
                    })
                .ToArray(),
            Events = definition.Events
                .OrderBy(static item => item.Id)
                .ThenBy(static item => item.Version)
                .Select(static item =>
                    new EventProviderSchemaEventLock {
                        Name = item.Name,
                        Id = item.Id,
                        Version = item.Version,
                        Channel = item.Channel,
                        Level = item.Level,
                        Task = item.Task,
                        Opcode = item.Opcode,
                        Keywords = item.Keywords
                            .OrderBy(static value => value, StringComparer.Ordinal)
                            .ToArray(),
                        Fields = item.Fields
                            .Select(static field =>
                                new EventProviderSchemaFieldLock {
                                    Name = field.Name,
                                    Type = field.Type,
                                    OutputType = field.OutputType,
                                    CustomOutputType =
                                        field.CustomOutputType,
                                    Map = field.Map,
                                    Length = field.Length,
                                    Count = field.Count
                                })
                            .ToArray()
                    })
                .ToArray()
        };
    }

    /// <summary>Serializes the lock using the package JSON contract.</summary>
    public string ToJson() {
        return JsonSerializer.Serialize(
            this,
            EventProviderDefinitionJson.SerializerOptions);
    }
}

/// <summary>Compatibility-critical channel identity.</summary>
public sealed class EventProviderSchemaChannelLock {
    /// <summary>Provider-local channel identifier.</summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>Complete Windows channel name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Channel kind.</summary>
    public EventProviderChannelType Type { get; set; }
    /// <summary>Channel security isolation.</summary>
    public EventProviderChannelIsolation Isolation { get; set; }
}

/// <summary>Compatibility-critical event identity and ordered schema.</summary>
public sealed class EventProviderSchemaEventLock {
    /// <summary>Stable event name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Event identifier.</summary>
    public int Id { get; set; }
    /// <summary>Event schema version.</summary>
    public byte Version { get; set; }
    /// <summary>Provider-local channel identifier.</summary>
    public string Channel { get; set; } = string.Empty;
    /// <summary>Event level reference.</summary>
    public string Level { get; set; } = string.Empty;
    /// <summary>Event task reference.</summary>
    public string Task { get; set; } = string.Empty;
    /// <summary>Event opcode reference.</summary>
    public string Opcode { get; set; } = string.Empty;
    /// <summary>Sorted keyword references.</summary>
    public IReadOnlyList<string> Keywords { get; set; } =
        Array.Empty<string>();
    /// <summary>Ordered payload fields.</summary>
    public IReadOnlyList<EventProviderSchemaFieldLock> Fields { get; set; } =
        Array.Empty<EventProviderSchemaFieldLock>();
}

/// <summary>Compatibility-critical event payload field.</summary>
public sealed class EventProviderSchemaFieldLock {
    /// <summary>Stable field name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Native Windows input type.</summary>
    public EventProviderFieldType Type { get; set; }
    /// <summary>Typed rendering hint.</summary>
    public EventProviderFieldOutputType OutputType { get; set; }
    /// <summary>Optional advanced output type.</summary>
    public string CustomOutputType { get; set; } = string.Empty;
    /// <summary>Optional map reference.</summary>
    public string Map { get; set; } = string.Empty;
    /// <summary>Optional length expression.</summary>
    public string Length { get; set; } = string.Empty;
    /// <summary>Optional count expression.</summary>
    public string Count { get; set; } = string.Empty;
}
