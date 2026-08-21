namespace EventViewerX.Providers;

/// <summary>Value or bit map referenced by event payload fields.</summary>
public sealed class EventProviderMapDefinition {
    /// <summary>Provider-local map name referenced by fields.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Whether values match exactly or represent individual bits.</summary>
    public EventProviderMapKind Kind { get; set; }
    /// <summary>Values and localized messages in the map.</summary>
    public List<EventProviderMapEntryDefinition> Entries { get; set; } = new();
}

/// <summary>One localized map value.</summary>
public sealed class EventProviderMapEntryDefinition {
    /// <summary>Numeric value or individual bit.</summary>
    public long Value { get; set; }
    /// <summary>Localized rendered values keyed by culture.</summary>
    public Dictionary<string, string> Messages { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
