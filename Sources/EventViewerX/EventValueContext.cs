namespace EventViewerX;

/// <summary>Provider, event, type, field, and sibling-value context supplied to a normalizer.</summary>
public sealed class EventValueContext {
    /// <summary>Provider that emitted the event.</summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>Windows event identifier.</summary>
    public int EventId { get; set; }

    /// <summary>Stable built-in or custom event type.</summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>Projected field name.</summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>Original projected value.</summary>
    public object? RawValue { get; set; }

    /// <summary>Sibling projected values from the same record.</summary>
    public IReadOnlyDictionary<string, object?> Values { get; set; } =
        new Dictionary<string, object?>();
}
