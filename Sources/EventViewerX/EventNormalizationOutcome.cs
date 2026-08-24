namespace EventViewerX;

/// <summary>Outcome of deterministic normalization for one field value.</summary>
public enum EventNormalizationOutcome {
    /// <summary>The raw value already has the canonical representation.</summary>
    Unchanged,
    /// <summary>A known deterministic transformation produced a canonical value.</summary>
    Normalized,
    /// <summary>The field is recognized but the supplied value is not known.</summary>
    UnknownValue,
    /// <summary>The field is recognized but the supplied value is malformed.</summary>
    Malformed
}
