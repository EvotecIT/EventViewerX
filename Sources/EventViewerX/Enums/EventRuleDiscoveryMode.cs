namespace EventViewerX;

/// <summary>
/// Controls how <see cref="EventTypeRecord"/> discovers and routes event rule types.
/// </summary>
public enum EventRuleDiscoveryMode {
    /// <summary>
    /// Uses explicit registrations when provided, with built-in factory registrations as the fallback.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Uses only explicitly registered rules.
    /// </summary>
    ExplicitOnly = 1,

    /// <summary>
    /// Uses the legacy built-in registration tables, populated from static factories.
    /// </summary>
    Reflection = 2
}

