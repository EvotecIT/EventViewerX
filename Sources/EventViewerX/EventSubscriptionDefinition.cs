using System.Globalization;
using System.Net;

namespace EventViewerX;

/// <summary>High-level native event subscription shared by PowerShell and applications.</summary>
public sealed class EventSubscriptionDefinition {
    /// <summary>Channel to monitor.</summary>
    public string LogName { get; set; } = string.Empty;

    /// <summary>Remote computer, or null for the local computer.</summary>
    public string? MachineName { get; set; }

    /// <summary>Credentials for a remote subscription.</summary>
    public NetworkCredential? Credential { get; set; }

    /// <summary>Remote authentication package.</summary>
    public EventLogAuthentication Authentication { get; set; }

    /// <summary>Typed native filter.</summary>
    public EventFilter? Filter { get; set; }

    /// <summary>Opaque native XPath used instead of Filter.</summary>
    public string? FilterXPath { get; set; }

    /// <summary>Subscription starting position.</summary>
    public EventLogSubscriptionStart Start { get; set; } =
        EventLogSubscriptionStart.Future;

    /// <summary>Native bookmark used with AfterBookmark.</summary>
    public string? BookmarkXml { get; set; }

    /// <summary>Requires the bookmark to still exist.</summary>
    public bool StrictBookmark { get; set; } = true;

    /// <summary>Allows Windows to tolerate unsupported QueryList paths.</summary>
    public bool TolerateQueryErrors { get; set; }

    /// <summary>Amount of data materialized for each delivered event.</summary>
    public EventReadMode ReadMode { get; set; } = EventReadMode.Full;

    /// <summary>Primary provider-resource culture.</summary>
    public CultureInfo? MessageCulture { get; set; }

    /// <summary>Fallback provider-resource culture.</summary>
    public CultureInfo? FallbackMessageCulture { get; set; }

    /// <summary>Maximum detached event snapshots awaiting delivery.</summary>
    public int BufferCapacity { get; set; } = 256;

    /// <summary>Remote connection timeout in milliseconds.</summary>
    public int RemoteConnectionTimeoutMilliseconds { get; set; } = 5000;
}