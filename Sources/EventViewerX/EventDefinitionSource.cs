namespace EventViewerX;

/// <summary>Declares one Windows Event Log source for a custom definition.</summary>
public sealed class EventDefinitionSource {
    /// <summary>Original event channel.</summary>
    public string LogName { get; set; } = string.Empty;
    /// <summary>Event identifiers accepted from the channel.</summary>
    public IReadOnlyList<int> EventIds { get; set; } = Array.Empty<int>();
    /// <summary>Optional provider-name allow list.</summary>
    public IReadOnlyList<string> ProviderNames { get; set; } = Array.Empty<string>();
}
