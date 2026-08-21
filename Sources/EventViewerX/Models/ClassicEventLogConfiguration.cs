using System.Diagnostics;

namespace EventViewerX;

/// <summary>Desired state for a classic Windows Event Log and its source.</summary>
public sealed class ClassicEventLogConfiguration {
    /// <summary>Classic log name.</summary>
    public string LogName { get; set; } = string.Empty;

    /// <summary>Source registered to the log. Defaults to LogName when empty.</summary>
    public string SourceName { get; set; } = string.Empty;

    /// <summary>Optional remote computer; null targets the local computer.</summary>
    public string? MachineName { get; set; }

    /// <summary>Optional maximum size in kilobytes. Null preserves the current value.</summary>
    public long? MaximumKilobytes { get; set; }

    /// <summary>Optional overflow behavior. Null preserves the current Windows setting.</summary>
    public OverflowAction? OverflowAction { get; set; }

    /// <summary>Retention days used only with OverwriteOlder. Null preserves the current Windows setting.</summary>
    public int? RetentionDays { get; set; }

    /// <summary>Optional source resource configuration used when registration is required.</summary>
    public ClassicEventSourceConfiguration? Source { get; set; }
}
