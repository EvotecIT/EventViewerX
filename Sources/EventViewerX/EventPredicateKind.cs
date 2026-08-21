namespace EventViewerX;

/// <summary>Node kinds in a reusable typed event predicate.</summary>
public enum EventPredicateKind {
    /// <summary>One field comparison.</summary>
    Comparison,
    /// <summary>Every child must match.</summary>
    All,
    /// <summary>At least one child must match.</summary>
    Any,
    /// <summary>The single child must not match.</summary>
    Not
}
