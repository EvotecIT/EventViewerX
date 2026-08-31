using System;
using System.Globalization;
using System.Net;

namespace EventViewerX;

/// <summary>Defines a low-level streaming query against a local or remote Windows event channel.</summary>
public sealed class EventLogChannelQuery {
    /// <summary>Creates a channel query.</summary>
    /// <param name="logName">Windows event channel name, for example System or Security.</param>
    public EventLogChannelQuery(string logName) {
        if (string.IsNullOrWhiteSpace(logName)) {
            throw new ArgumentException("Event log name cannot be null or empty.", nameof(logName));
        }
        LogName = logName;
    }

    /// <summary>Creates a query against a collector's ForwardedEvents channel constrained to the source channel.</summary>
    /// <param name="originalLogName">Original source channel stored in each forwarded event, for example Security.</param>
    /// <param name="machineName">Collector computer. A null or empty value targets the local collector.</param>
    /// <param name="xpath">Additional native XPath filter applied with the original-channel constraint.</param>
    public static EventLogChannelQuery ForCollector(
        string originalLogName,
        string? machineName = null,
        string xpath = "*") => new(ForwardedEventsQuerySafety.ChannelName) {
            MachineName = machineName,
            XPath = EventFilterCompiler.AddOriginalChannelPredicate(xpath, originalLogName)
        };

    /// <summary>Windows event channel name.</summary>
    public string LogName { get; }

    /// <summary>Remote computer name. A null or empty value targets the local computer.</summary>
    public string? MachineName { get; set; }

    /// <summary>
    /// Optional credentials used for a remote Windows Event Log session.
    /// Credentials are rejected for local queries so an accidental local target cannot silently ignore them.
    /// </summary>
    public NetworkCredential? Credential { get; set; }

    /// <summary>Authentication package used for a remote Windows Event Log session.</summary>
    public EventLogAuthentication Authentication { get; set; }

    /// <summary>XPath expression applied by the Windows event query engine.</summary>
    public string XPath { get; set; } = "*";

    /// <summary>Whether records are returned from oldest to newest.</summary>
    public bool Oldest { get; set; }

    /// <summary>Amount of event data materialized for each record.</summary>
    public EventReadMode ReadMode { get; set; } = EventReadMode.Message;

    /// <summary>
    /// Culture requested for provider messages and display names.
    /// A null value uses <see cref="CultureInfo.CurrentUICulture"/>.
    /// </summary>
    public CultureInfo? MessageCulture { get; set; }

    /// <summary>Fallback culture used when provider resources do not contain MessageCulture.</summary>
    public CultureInfo? FallbackMessageCulture { get; set; }

    /// <summary>Maximum number of records returned. Zero streams every match.</summary>
    public long MaxEvents { get; set; }

    internal string? BatchSourceIdentity { get; set; }

    internal DateTime? ManagedStartTimeUtc { get; set; }

    internal DateTime? ManagedEndTimeUtc { get; set; }

    internal Func<EventObject, bool>? ManagedPredicate { get; set; }

    internal long ManagedMaxEventsScanned { get; set; }

    internal Action? ManagedScanLimitReached { get; set; }

    /// <summary>Materializes a native bookmark for every returned event.</summary>
    public bool IncludeBookmark { get; set; }

    /// <summary>Maximum time for the RPC probe, worker slot, and remote session establishment.</summary>
    public int RemoteConnectionTimeoutMilliseconds { get; set; } = 5000;

    /// <summary>
    /// Maximum time without remote read progress. Zero keeps the read unbounded.
    /// </summary>
    public int RemoteReadTimeoutMilliseconds { get; set; }

    /// <summary>Maximum detached event snapshots buffered between the remote native worker and caller.</summary>
    public int BufferCapacity { get; set; } = 64;

    /// <summary>RPC endpoint mapper port probed before starting a remote native query.</summary>
    public int RpcEndpointPort { get; set; } = 135;

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
