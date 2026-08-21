namespace EventViewerX;

/// <summary>
/// Common event severity levels.
/// </summary>
public enum Level {
    /// <summary>Trace/verbose detail.</summary>
    Verbose = 5,
    /// <summary>Informational events.</summary>
    Informational = 4,
    /// <summary>Warning events.</summary>
    Warning = 3,
    /// <summary>Error events.</summary>
    Error = 2,
    /// <summary>Critical events.</summary>
    Critical = 1,
    /// <summary>Always logged regardless of level filtering.</summary>
    LogAlways = 0
}
