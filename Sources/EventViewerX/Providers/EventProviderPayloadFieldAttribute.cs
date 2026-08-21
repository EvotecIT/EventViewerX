namespace EventViewerX.Providers;

/// <summary>
/// Controls how a public property is represented in an inferred event payload
/// schema.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class EventProviderPayloadFieldAttribute : Attribute {
    /// <summary>Creates field metadata for the specified zero-based order.</summary>
    public EventProviderPayloadFieldAttribute(int order) {
        Order = order;
    }

    /// <summary>Stable zero-based field order.</summary>
    public int Order { get; }

    /// <summary>Optional manifest field name override.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional native type override. The default infers the type from the
    /// attributed property.
    /// </summary>
    public EventProviderFieldType Type { get; set; } =
        EventProviderFieldType.Auto;

    /// <summary>Optional rendering hint.</summary>
    public EventProviderFieldOutputType OutputType { get; set; }

    /// <summary>Optional map name.</summary>
    public string Map { get; set; } = string.Empty;

    /// <summary>Optional length expression.</summary>
    public string Length { get; set; } = string.Empty;

    /// <summary>Optional count expression.</summary>
    public string Count { get; set; } = string.Empty;
}
