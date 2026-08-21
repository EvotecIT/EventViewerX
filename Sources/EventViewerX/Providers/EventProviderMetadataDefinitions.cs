namespace EventViewerX.Providers;

/// <summary>Custom provider level.</summary>
public sealed class EventProviderLevelDefinition {
    /// <summary>Provider-local level name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Custom level value from 16 through 255.</summary>
    public byte Value { get; set; }
    /// <summary>Optional C-compatible symbol.</summary>
    public string Symbol { get; set; } = string.Empty;
    /// <summary>Localized display names keyed by culture.</summary>
    public Dictionary<string, string> DisplayNames { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Provider task and optional task-local opcodes.</summary>
public sealed class EventProviderTaskDefinition {
    /// <summary>Provider-local task name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Task descriptor value from 1 through 65535.</summary>
    public ushort Value { get; set; }
    /// <summary>Optional classic task event GUID.</summary>
    public Guid? EventGuid { get; set; }
    /// <summary>Optional C-compatible symbol.</summary>
    public string Symbol { get; set; } = string.Empty;
    /// <summary>Localized display names keyed by culture.</summary>
    public Dictionary<string, string> DisplayNames { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Task-local opcode definitions.</summary>
    public List<EventProviderOpcodeDefinition> Opcodes { get; set; } = new();
}

/// <summary>Provider or task-local opcode.</summary>
public sealed class EventProviderOpcodeDefinition {
    /// <summary>Provider-local opcode name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Custom opcode value from 10 through 239.</summary>
    public byte Value { get; set; }
    /// <summary>Optional C-compatible symbol.</summary>
    public string Symbol { get; set; } = string.Empty;
    /// <summary>Localized display names keyed by culture.</summary>
    public Dictionary<string, string> DisplayNames { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Provider keyword.</summary>
public sealed class EventProviderKeywordDefinition {
    /// <summary>Provider-local keyword name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>One non-reserved bit in the low 48 keyword bits.</summary>
    public ulong Mask { get; set; }
    /// <summary>Optional C-compatible symbol.</summary>
    public string Symbol { get; set; } = string.Empty;
    /// <summary>Localized display names keyed by culture.</summary>
    public Dictionary<string, string> DisplayNames { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
