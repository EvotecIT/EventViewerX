namespace EventViewerX;

/// <summary>
/// Specifies how numeric event field values should be interpreted when converting to typed values.
/// </summary>
public enum EventFieldNumericBase {
    /// <summary>
    /// Auto-detect base from the value (prefers decimal unless a hex prefix or explicit hex pattern is present).
    /// </summary>
    Auto = 0,
    /// <summary>Interpret value as decimal.</summary>
    Decimal = 10,
    /// <summary>Interpret value as hexadecimal.</summary>
    Hexadecimal = 16
}
