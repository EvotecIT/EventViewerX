namespace EventViewerX;

/// <summary>
/// High-level event query shared by PowerShell, applications, exporters, and future delivery adapters.
/// Exactly one source family must be populated.
/// </summary>
public sealed class EventQueryDefinition {
    /// <summary>Channel names or wildcard patterns.</summary>
    public IReadOnlyList<string>? LogNames { get; set; }

    /// <summary>Offline event-log paths or wildcard patterns.</summary>
    public IReadOnlyList<string>? Paths { get; set; }

    /// <summary>Provider names or wildcard patterns. Linked channels are resolved automatically.</summary>
    public IReadOnlyList<string>? ProviderNames { get; set; }

    /// <summary>Complete Windows Event Log QueryList XML.</summary>
    public string? QueryXml { get; set; }

    /// <summary>Typed native filter. This cannot be combined with FilterXPath or QueryXml.</summary>
    public EventFilter? Filter { get; set; }

    /// <summary>Raw XPath applied to every resolved channel or file.</summary>
    public string? FilterXPath { get; set; }

    /// <summary>Local or remote query targets. Null or empty means the local computer.</summary>
    public IReadOnlyList<string?>? MachineNames { get; set; }

    /// <summary>Projection, ordering, remote-session, and batch controls.</summary>
    public EventLogQueryOptions Options { get; set; } = new();

    /// <summary>Includes wildcard-matched analytic and debug channels.</summary>
    public bool IncludeAnalyticAndDebugChannels { get; set; }

    /// <summary>Allows QueryList paths unsupported on a target to be tolerated by Windows.</summary>
    public bool TolerateQueryErrors { get; set; }
}