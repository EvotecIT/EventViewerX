namespace EventViewerX;

/// <summary>
/// Request for writing a registered manifest/ETW event.
/// </summary>
public sealed class ManifestEventWriteRequest {
    /// <summary>Registered provider name.</summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>Manifest event identifier.</summary>
    public int Id { get; set; }

    /// <summary>
    /// Optional event version. It is required when the provider declares more
    /// than one version of the same event identifier.
    /// </summary>
    public byte? Version { get; set; }

    /// <summary>Positional values supplied to the event template.</summary>
    public IReadOnlyList<object?> Payload {
        get;
        set;
    } = Array.Empty<object?>();
}
