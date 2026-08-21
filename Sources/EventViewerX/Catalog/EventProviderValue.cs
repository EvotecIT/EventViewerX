namespace EventViewerX;

/// <summary>Detached provider level, task, opcode, or keyword metadata.</summary>
public sealed class EventProviderValue {
    internal EventProviderValue(
        string name,
        string displayName,
        long value,
        Guid? eventGuid = null) {

        Name = name;
        DisplayName = displayName;
        Value = value;
        EventGuid = eventGuid;
    }

    /// <summary>Manifest symbol name.</summary>
    public string Name { get; }

    /// <summary>Localized display name.</summary>
    public string DisplayName { get; }

    /// <summary>Numeric value.</summary>
    public long Value { get; }

    /// <summary>Task event GUID when supplied by the provider manifest.</summary>
    public Guid? EventGuid { get; }
}
