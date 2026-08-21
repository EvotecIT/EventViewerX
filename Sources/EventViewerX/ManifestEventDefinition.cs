namespace EventViewerX;

/// <summary>
/// Detached manifest definition required to write one ETW event.
/// </summary>
public sealed class ManifestEventDefinition {
    /// <summary>Registered provider name.</summary>
    public string ProviderName { get; internal set; } = string.Empty;

    /// <summary>Registered provider identifier.</summary>
    public Guid ProviderId { get; internal set; }

    /// <summary>Manifest event identifier.</summary>
    public int Id { get; internal set; }

    /// <summary>Manifest event version.</summary>
    public byte Version { get; internal set; }

    /// <summary>Provider channel index used by the ETW descriptor.</summary>
    public byte Channel { get; internal set; }

    /// <summary>Event level value.</summary>
    public byte Level { get; internal set; }

    /// <summary>Event opcode value.</summary>
    public byte Opcode { get; internal set; }

    /// <summary>Event task value.</summary>
    public ushort Task { get; internal set; }

    /// <summary>Combined event keyword bitmask.</summary>
    public long Keywords { get; internal set; }

    /// <summary>Destination channel declared by the provider.</summary>
    public string LogName { get; internal set; } = string.Empty;

    /// <summary>Raw provider template XML.</summary>
    public string Template { get; internal set; } = string.Empty;

    /// <summary>Ordered payload fields declared by the event template.</summary>
    public IReadOnlyList<ManifestEventPayloadField> PayloadFields {
        get;
        internal set;
    } = Array.Empty<ManifestEventPayloadField>();
}
