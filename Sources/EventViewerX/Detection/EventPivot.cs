namespace EventViewerX;

/// <summary>One normalized value suitable for hunting across observations and findings.</summary>
public sealed class EventPivot {
    internal EventPivot(EventPivotKind kind, string field, string value) {
        Kind = kind;
        Field = field;
        Value = value;
    }

    /// <summary>Canonical pivot category.</summary>
    public EventPivotKind Kind { get; }
    /// <summary>Source field that supplied the value.</summary>
    public string Field { get; }
    /// <summary>Normalized non-empty pivot value.</summary>
    public string Value { get; }
}
