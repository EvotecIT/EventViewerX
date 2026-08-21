using System.Diagnostics;

namespace EventViewerX;

/// <summary>One explicit classic Windows Event Log write operation.</summary>
public sealed class ClassicEventWriteRequest {
    /// <summary>Registered source name.</summary>
    public string SourceName { get; set; } = string.Empty;

    /// <summary>Target classic log.</summary>
    public string LogName { get; set; } = string.Empty;

    /// <summary>Message text or first provider insertion string.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Entry severity/type.</summary>
    public EventLogEntryType EntryType { get; set; } =
        EventLogEntryType.Information;

    /// <summary>Provider category in the Int16 non-negative range.</summary>
    public int Category { get; set; }

    /// <summary>Provider event identifier in the UInt16 range.</summary>
    public int EventId { get; set; }

    /// <summary>Optional remote computer; null targets the local computer.</summary>
    public string? MachineName { get; set; }

    /// <summary>Optional raw provider payload.</summary>
    public byte[]? RawData { get; set; }

    /// <summary>Optional additional provider insertion strings.</summary>
    public IReadOnlyList<string>? ReplacementStrings { get; set; }

    /// <summary>
    /// Registers a missing source explicitly before writing.
    /// The default is false so an ordinary write never performs an administrative configuration change.
    /// </summary>
    public bool CreateSourceIfMissing { get; set; }
}
