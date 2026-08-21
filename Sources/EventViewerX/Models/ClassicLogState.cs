namespace EventViewerX;

/// <summary>
/// Snapshot of classic Event Log and source state used by callers that need preview/apply planning.
/// </summary>
public sealed class ClassicLogState {
    /// <summary>Target classic log name.</summary>
    public string LogName { get; set; } = string.Empty;
    /// <summary>Target event source name.</summary>
    public string SourceName { get; set; } = string.Empty;
    /// <summary>Optional remote machine name; <c>null</c> targets the local host.</summary>
    public string? MachineName { get; set; }
    /// <summary>Whether the classic log currently exists.</summary>
    public bool LogExists { get; set; }
    /// <summary>Whether the event source currently exists.</summary>
    public bool SourceExists { get; set; }
    /// <summary>Current log registration for the event source when it exists.</summary>
    public string? SourceRegisteredLogName { get; set; }
    /// <summary>Display name of the classic log when available.</summary>
    public string? LogDisplayName { get; set; }
    /// <summary>Configured maximum size in kilobytes when available.</summary>
    public int? MaximumKilobytes { get; set; }
    /// <summary>Canonical overflow action name when available.</summary>
    public string? OverflowActionName { get; set; }
    /// <summary>Minimum retention in days when available.</summary>
    public int? MinimumRetentionDays { get; set; }
}
