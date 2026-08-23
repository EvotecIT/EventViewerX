namespace EventViewerX;

/// <summary>Raw and canonical representations of one event field.</summary>
public sealed class EventNormalizedValue {
    /// <summary>Original projected value retained as evidence.</summary>
    public object? RawValue { get; internal set; }

    /// <summary>Canonical typed value used by grouping and aggregation.</summary>
    public object? Value { get; internal set; }

    /// <summary>Culture-invariant display value used by built-in renderers.</summary>
    public string DisplayValue { get; internal set; } = string.Empty;

    /// <summary>Stable semantic value kind.</summary>
    public EventNormalizedValueKind Kind { get; internal set; }

    /// <summary>Normalization outcome.</summary>
    public EventNormalizationOutcome Outcome { get; internal set; }

    /// <summary>Stable normalizer name.</summary>
    public string Normalizer { get; internal set; } = string.Empty;

    /// <summary>Normalizer contract version.</summary>
    public int NormalizerVersion { get; internal set; }

    /// <summary>Whether the canonical value fully represents the raw value.</summary>
    public bool IsLossless { get; internal set; } = true;

    /// <summary>Deterministic warnings explaining malformed, unknown, or lossy input.</summary>
    public IReadOnlyList<string> Warnings { get; internal set; } = Array.Empty<string>();
}
