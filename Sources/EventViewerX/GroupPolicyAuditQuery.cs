using System.Globalization;
using System.Net;

namespace EventViewerX;

/// <summary>
/// Defines a source-neutral Group Policy audit query. Direct mode reads Security on each target;
/// collector mode reads the configured collector channel while preserving each event's source computer.
/// </summary>
public sealed class GroupPolicyAuditQuery {
    /// <summary>
    /// Optional context store populated and resolved only from selected Group Policy events.
    /// Supplying a store does not initiate directory or SYSVOL discovery.
    /// Results are buffered until the selected scan timeline has been stored so event-time context is final.
    /// </summary>
    public IEventContextStore? ContextStore { get; set; }

    /// <summary>
    /// Optional caller-authorized partition used to resolve non-shareable lookup or imported context.
    /// Supplying a value selects matching evidence; it does not grant access to that partition.
    /// </summary>
    public string? AuthorizationContext { get; set; }

    /// <summary>Optional offline event-log files.</summary>
    public IReadOnlyList<string>? Paths { get; set; }

    /// <summary>Domain controllers in direct mode or collector computers in collector mode.</summary>
    public IReadOnlyList<string?>? MachineNames { get; set; }

    /// <summary>Collector channel, normally ForwardedEvents. Null reads Security directly.</summary>
    public string? CollectorLogName { get; set; }

    /// <summary>Absolute start time.</summary>
    public DateTime? StartTime { get; set; }

    /// <summary>Absolute end time.</summary>
    public DateTime? EndTime { get; set; }

    /// <summary>Relative time window.</summary>
    public TimePeriod? TimePeriod { get; set; }

    /// <summary>Maximum matching Group Policy events. Zero is unlimited.</summary>
    public long MaxEvents { get; set; }

    /// <summary>Maximum candidate audit events examined. Zero is unlimited.</summary>
    public long MaxCandidates { get; set; }

    /// <summary>Maximum independent sources opened concurrently.</summary>
    public int MaxConcurrency { get; set; } = 8;

    /// <summary>Reads oldest events first. This defaults to true for forward-only checkpoint progression.</summary>
    public bool Oldest { get; set; } = true;

    /// <summary>Previously persisted source checkpoints.</summary>
    public IReadOnlyList<GroupPolicyAuditCheckpoint>? Checkpoints { get; set; }

    /// <summary>Whether an invalid or expired checkpoint fails its source.</summary>
    public bool StrictCheckpoint { get; set; } = true;

    /// <summary>Remote credential shared by every remote target.</summary>
    public NetworkCredential? Credential { get; set; }

    /// <summary>Remote authentication package.</summary>
    public EventLogAuthentication Authentication { get; set; }

    /// <summary>Maximum time used to establish each remote session.</summary>
    public int RemoteConnectionTimeoutMilliseconds { get; set; } = 5000;

    /// <summary>Maximum time without remote read progress. Zero is unbounded.</summary>
    public int RemoteReadTimeoutMilliseconds { get; set; }

    /// <summary>Detached snapshots buffered by each remote reader.</summary>
    public int BufferCapacity { get; set; } = 64;

    /// <summary>Culture used for provider messages and display names.</summary>
    public CultureInfo? MessageCulture { get; set; }

    /// <summary>Fallback provider-resource culture.</summary>
    public CultureInfo? FallbackMessageCulture { get; set; }

    /// <summary>Continues healthy remote targets after an expected remote-target failure.</summary>
    public bool ContinueOnRemoteFailure { get; set; } = true;
}
