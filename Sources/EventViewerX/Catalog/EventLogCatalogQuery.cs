using System.Globalization;
using System.Net;

namespace EventViewerX;

/// <summary>Connection and projection options for channel and provider discovery.</summary>
public sealed class EventLogCatalogQuery {
    /// <summary>Remote computer name. Null targets the local computer.</summary>
    public string? MachineName { get; set; }

    /// <summary>Optional remote credentials.</summary>
    public NetworkCredential? Credential { get; set; }

    /// <summary>Authentication package for the remote session.</summary>
    public EventLogAuthentication Authentication { get; set; }

    /// <summary>Maximum time for RPC preflight and session establishment.</summary>
    public int ConnectionTimeoutMilliseconds { get; set; } = 5000;

    /// <summary>Culture requested for provider display metadata.</summary>
    public CultureInfo? Culture { get; set; }

    /// <summary>
    /// Includes every event definition and template. This can be expensive for large providers.
    /// </summary>
    public bool IncludeEvents { get; set; }
}
