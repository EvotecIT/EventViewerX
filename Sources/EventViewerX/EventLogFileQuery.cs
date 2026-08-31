using System;
using System.Globalization;

namespace EventViewerX;

/// <summary>
/// Defines a low-level, streaming query against an offline Windows event log.
/// </summary>
public sealed class EventLogFileQuery {
    /// <summary>
    /// Creates an offline event query.
    /// </summary>
    /// <param name="path">Path to an offline log accepted by the Windows Event Log API. EVTX is the validated format.</param>
    public EventLogFileQuery(string path) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Event log path cannot be null or empty.", nameof(path));
        }
        Path = path;
    }

    /// <summary>Path to the offline event log.</summary>
    public string Path { get; }

    /// <summary>XPath expression applied by the Windows event query engine.</summary>
    public string XPath { get; set; } = "*";

    /// <summary>Whether records are returned from oldest to newest.</summary>
    public bool Oldest { get; set; }

    /// <summary>Amount of event data materialized for each record.</summary>
    public EventReadMode ReadMode { get; set; } = EventReadMode.Message;

    /// <summary>
    /// Optional parser-neutral saved-event reader. Null uses the Windows Eventing API. A custom reader must
    /// preserve query semantics and report corruption or unsupported behavior explicitly.
    /// </summary>
    public ISavedEventReader? SavedEventReader { get; set; }

    /// <summary>Optional sink for per-region corruption, recovery, and fidelity diagnostics.</summary>
    public Action<SavedEventReadDiagnostic>? SavedEventDiagnosticHandler { get; set; }

    /// <summary>
    /// Culture used for provider messages and display names. A null value uses
    /// <see cref="CultureInfo.CurrentUICulture"/>.
    /// </summary>
    public CultureInfo? MessageCulture { get; set; }

    /// <summary>Fallback culture used when provider resources do not contain MessageCulture.</summary>
    public CultureInfo? FallbackMessageCulture { get; set; }

    /// <summary>Maximum number of records returned. Zero streams every match.</summary>
    public long MaxEvents { get; set; }

    internal string? BatchSourceIdentity { get; set; }

    /// <summary>Materializes a native bookmark for every returned event.</summary>
    public bool IncludeBookmark { get; set; }

    /// <summary>
    /// Optional native bookmark XML used as the seek origin before enumeration starts.
    /// </summary>
    public string? BookmarkXml { get; set; }

    /// <summary>
    /// Record offset relative to <see cref="BookmarkXml"/>. The default of one resumes after the bookmarked event.
    /// </summary>
    public long BookmarkOffset { get; set; } = 1;

    /// <summary>
    /// Requires the bookmark to identify an event present in the result set.
    /// </summary>
    public bool StrictBookmark { get; set; } = true;
}
