namespace EventViewerX;

/// <summary>Detached relationship between an event provider and a channel.</summary>
public sealed class EventProviderLogLink {
    internal EventProviderLogLink(
        string logName,
        string displayName,
        bool isImported) {

        LogName = logName;
        DisplayName = displayName;
        IsImported = isImported;
    }

    /// <summary>Channel name.</summary>
    public string LogName { get; }

    /// <summary>Localized channel display name.</summary>
    public string DisplayName { get; }

    /// <summary>Whether the channel definition is imported from another publisher.</summary>
    public bool IsImported { get; }
}
