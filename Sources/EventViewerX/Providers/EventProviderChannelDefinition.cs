namespace EventViewerX.Providers;

/// <summary>One channel declared by a manifest-based event provider.</summary>
public sealed class EventProviderChannelDefinition {
    /// <summary>Provider-local channel identifier referenced by events.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Complete Windows channel name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>C-compatible channel symbol. A safe symbol is generated when omitted.</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Channel kind.</summary>
    public EventProviderChannelType Type { get; set; } =
        EventProviderChannelType.Operational;

    /// <summary>Channel security and backing-log isolation.</summary>
    public EventProviderChannelIsolation Isolation { get; set; } =
        EventProviderChannelIsolation.Application;

    /// <summary>Whether the channel is enabled when installed.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Whether events are retained instead of overwritten.</summary>
    public bool? Retention { get; set; }

    /// <summary>Whether a full retained log is automatically backed up.</summary>
    public bool? AutoBackup { get; set; }

    /// <summary>Optional maximum channel size in bytes.</summary>
    public long? MaximumSizeBytes { get; set; }

    /// <summary>Optional SDDL controlling channel access.</summary>
    public string Access { get; set; } = string.Empty;

    /// <summary>Localized channel display names keyed by culture.</summary>
    public Dictionary<string, string> DisplayNames { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates an operational channel with conventional defaults.</summary>
    public static EventProviderChannelDefinition Operational(
        string id,
        string name) {

        return new EventProviderChannelDefinition {
            Id = id,
            Name = name,
            Type = EventProviderChannelType.Operational,
            Isolation = EventProviderChannelIsolation.Application,
            Enabled = true
        };
    }
}
