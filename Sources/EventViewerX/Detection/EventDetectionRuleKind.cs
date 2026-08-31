namespace EventViewerX;

/// <summary>Evaluation behavior implemented by a native detection definition.</summary>
public enum EventDetectionRuleKind {
    /// <summary>One matching observation produces one finding.</summary>
    Stateless,
    /// <summary>A bounded count of matching observations within a time window produces a finding.</summary>
    Threshold,
    /// <summary>A bounded count of distinct field values within a time window produces a finding.</summary>
    DistinctValue,
    /// <summary>All configured steps must occur within a window, in any order.</summary>
    Temporal,
    /// <summary>All configured steps must occur within a window in declared order.</summary>
    OrderedTemporal
}
