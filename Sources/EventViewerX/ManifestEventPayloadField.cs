namespace EventViewerX;

/// <summary>
/// One positional payload field declared by a manifest event template.
/// </summary>
public sealed class ManifestEventPayloadField {
    /// <summary>Zero-based positional index supplied to the event writer.</summary>
    public int Index { get; internal set; }

    /// <summary>Manifest field name.</summary>
    public string Name { get; internal set; } = string.Empty;

    /// <summary>Windows manifest input type, for example <c>win:UnicodeString</c>.</summary>
    public string InputType { get; internal set; } = string.Empty;

    /// <summary>Optional display/output type declared by the manifest.</summary>
    public string OutputType { get; internal set; } = string.Empty;

    /// <summary>Optional manifest value-map name.</summary>
    public string Map { get; internal set; } = string.Empty;

    /// <summary>
    /// Optional manifest length expression. This is either a non-negative
    /// constant or the name of another payload field.
    /// </summary>
    public string Length { get; internal set; } = string.Empty;

    /// <summary>
    /// Optional manifest element-count expression. This is either a
    /// non-negative constant or the name of another payload field.
    /// </summary>
    public string Count { get; internal set; } = string.Empty;
}
