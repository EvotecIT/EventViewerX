using System.Globalization;
using System.Net;

namespace EventViewerX;

/// <summary>Defines a dependency-free native Windows Event Log subscription.</summary>
public sealed class EventLogSubscriptionQuery {
    /// <summary>Creates a channel subscription.</summary>
    public EventLogSubscriptionQuery(string logName) {
        if (string.IsNullOrWhiteSpace(logName)) {
            throw new ArgumentException(
                "Event log name cannot be null or empty.",
                nameof(logName));
        }
        LogName = logName.Trim();
    }

    /// <summary>Windows event channel name.</summary>
    public string LogName { get; }

    /// <summary>Remote computer name. Null targets the local computer.</summary>
    public string? MachineName { get; set; }

    /// <summary>Optional remote credentials.</summary>
    public NetworkCredential? Credential { get; set; }

    /// <summary>Authentication package for the remote session.</summary>
    public EventLogAuthentication Authentication { get; set; }

    /// <summary>
    /// Native XPath or QueryList XML applied by the subscription.
    /// QueryList XML carries its own channel paths.
    /// </summary>
    public string XPath { get; set; } = "*";

    /// <summary>Starting position.</summary>
    public EventLogSubscriptionStart Start { get; set; }

    /// <summary>Bookmark XML required by AfterBookmark.</summary>
    public string? BookmarkXml { get; set; }

    /// <summary>Fails when the bookmark is stale or outside the result set.</summary>
    public bool StrictBookmark { get; set; } = true;

    /// <summary>Continues when the native query contains an unavailable path.</summary>
    public bool TolerateQueryErrors { get; set; }

    /// <summary>Amount of data detached for every delivered event.</summary>
    public EventReadMode ReadMode { get; set; } = EventReadMode.Message;

    /// <summary>Culture used for provider messages and labels.</summary>
    public CultureInfo? MessageCulture { get; set; }

    /// <summary>Fallback culture used when provider resources do not contain MessageCulture.</summary>
    public CultureInfo? FallbackMessageCulture { get; set; }

    /// <summary>Maximum queued native handles awaiting projection.</summary>
    public int BufferCapacity { get; set; } = 256;

    /// <summary>Remote session-open timeout.</summary>
    public int RemoteConnectionTimeoutMilliseconds { get; set; } = 5000;
}
