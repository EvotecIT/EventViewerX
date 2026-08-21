namespace EventViewerX;

/// <summary>
/// Controls how <see cref="EventTypeRecord"/> discovers and routes event rule types.
/// </summary>
public enum EventRuleDiscoveryMode {
    /// <summary>
    /// Uses explicit registrations when provided; otherwise falls back to reflection-based discovery.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Uses only explicitly registered rules. Reflection discovery is disabled.
    /// </summary>
    ExplicitOnly = 1,

    /// <summary>
    /// Always uses reflection-based discovery (legacy behavior).
    /// </summary>
    Reflection = 2
}

