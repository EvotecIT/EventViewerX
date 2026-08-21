using System.Globalization;
using System.Net;

namespace EventViewerX;

/// <summary>
/// Common projection, ordering, remote-session, and batch options used by query factories.
/// </summary>
public sealed class EventLogQueryOptions {
    /// <summary>Whether records are returned oldest first.</summary>
    public bool Oldest { get; set; }

    /// <summary>Amount of event data materialized for each record.</summary>
    public EventReadMode ReadMode { get; set; } =
        EventReadMode.Message;

    /// <summary>Culture requested for provider messages and labels.</summary>
    public CultureInfo? MessageCulture { get; set; }

    /// <summary>Fallback provider-resource culture.</summary>
    public CultureInfo? FallbackMessageCulture {
        get;
        set;
    }

    /// <summary>Maximum merged records. Zero streams every match.</summary>
    public long MaxEvents { get; set; }

    /// <summary>
    /// Maximum native records inspected by a managed compatibility selector. Zero is unlimited.
    /// Native selective queries do not need this secondary bound.
    /// </summary>
    public long MaxEventsScanned { get; set; }

    /// <summary>Materializes a native bookmark for every result.</summary>
    public bool IncludeBookmark { get; set; }

    /// <summary>Native bookmark XML used as the seek origin.</summary>
    public string? BookmarkXml { get; set; }

    /// <summary>Record offset relative to BookmarkXml.</summary>
    public long BookmarkOffset { get; set; } = 1;

    /// <summary>Requires the bookmark to identify a record present in the result set.</summary>
    public bool StrictBookmark { get; set; } = true;

    /// <summary>Credentials shared by remote channel targets.</summary>
    public NetworkCredential? Credential { get; set; }

    /// <summary>Remote authentication package.</summary>
    public EventLogAuthentication Authentication {
        get;
        set;
    }

    /// <summary>Remote connection timeout in milliseconds.</summary>
    public int RemoteConnectionTimeoutMilliseconds {
        get;
        set;
    } = 5000;

    /// <summary>Remote no-progress read timeout in milliseconds. Zero is unbounded.</summary>
    public int RemoteReadTimeoutMilliseconds {
        get;
        set;
    }

    /// <summary>Detached snapshots buffered by each remote reader.</summary>
    public int BufferCapacity { get; set; } = 64;

    /// <summary>RPC endpoint mapper port used by remote preflight.</summary>
    public int RpcEndpointPort { get; set; } = 135;

    /// <summary>Maximum independent sources opened concurrently by asynchronous readers.</summary>
    public int MaxConcurrency { get; set; } = 8;

    /// <summary>Continues healthy sources after another source fails.</summary>
    public bool ContinueOnError { get; set; }

    /// <summary>Receives isolated source failures.</summary>
    public Action<EventLogQueryFailure>? FailureHandler {
        get;
        set;
    }
}
