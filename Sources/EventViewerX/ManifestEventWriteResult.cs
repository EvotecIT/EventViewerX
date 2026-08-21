namespace EventViewerX;

/// <summary>
/// Confirmed result of a manifest/ETW event write.
/// </summary>
public sealed class ManifestEventWriteResult {
    /// <summary>Resolved event definition used for the write.</summary>
    public ManifestEventDefinition Definition { get; internal set; } =
        null!;

    /// <summary>Number of positional payload values written.</summary>
    public int PayloadCount { get; internal set; }

    /// <summary>Native Windows status code. Zero indicates success.</summary>
    public uint NativeStatus { get; internal set; }

    /// <summary>Whether Windows accepted the event.</summary>
    public bool Success => NativeStatus == 0;
}
