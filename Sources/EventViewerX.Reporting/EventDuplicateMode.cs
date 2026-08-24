namespace EventViewerX.Reporting;

/// <summary>Requested duplicate presentation level.</summary>
public enum EventDuplicateMode {
    /// <summary>Return one group per source observation.</summary>
    None,
    /// <summary>Group repeated transports of the exact same source event.</summary>
    Transport,
    /// <summary>Also group source events carrying a validated shared causal discriminator.</summary>
    Semantic
}
