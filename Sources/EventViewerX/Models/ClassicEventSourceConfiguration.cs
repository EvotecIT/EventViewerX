namespace EventViewerX;

/// <summary>Configuration used to register a classic Windows Event Log source.</summary>
public sealed class ClassicEventSourceConfiguration {
    /// <summary>Source name registered with Windows.</summary>
    public string SourceName { get; set; } = string.Empty;

    /// <summary>Classic log that owns the source.</summary>
    public string LogName { get; set; } = string.Empty;

    /// <summary>Optional remote computer; null targets the local computer.</summary>
    public string? MachineName { get; set; }

    /// <summary>Optional provider message resource DLL path.</summary>
    public string? MessageResourceFile { get; set; }

    /// <summary>Optional provider parameter resource DLL path.</summary>
    public string? ParameterResourceFile { get; set; }

    /// <summary>Optional provider category resource DLL path.</summary>
    public string? CategoryResourceFile { get; set; }

    /// <summary>Number of categories described by CategoryResourceFile.</summary>
    public int CategoryCount { get; set; }
}
