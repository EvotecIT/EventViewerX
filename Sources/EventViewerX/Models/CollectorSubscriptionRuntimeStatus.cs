using System.Globalization;

namespace EventViewerX;

/// <summary>Current Windows Event Collector runtime state for one subscription.</summary>
public sealed class CollectorSubscriptionRuntimeStatus {
    /// <summary>Subscription identifier.</summary>
    public string SubscriptionName { get; set; } = string.Empty;
    /// <summary>Overall Active, Inactive, Trying, or unknown status.</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>Total events processed when Windows reports the counter.</summary>
    public long? EventsProcessed { get; set; }
    /// <summary>Overall Windows error code.</summary>
    public uint? LastErrorCode { get; set; }
    /// <summary>Overall error message.</summary>
    public string? ErrorMessage { get; set; }
    /// <summary>Per-source status returned by Windows.</summary>
    public IReadOnlyList<CollectorSubscriptionSourceRuntimeStatus> Sources { get; set; } =
        Array.Empty<CollectorSubscriptionSourceRuntimeStatus>();
    /// <summary>Unmodified wecutil runtime output retained for diagnostics.</summary>
    public string RawStatus { get; set; } = string.Empty;

    /// <summary>True when the subscription and all reported sources are active with no errors.</summary>
    public bool IsHealthy =>
        string.Equals(Status, "Active", StringComparison.OrdinalIgnoreCase) &&
        (!LastErrorCode.HasValue || LastErrorCode.Value == 0) &&
        Sources.All(static source => source.IsHealthy);
}

/// <summary>Current runtime state for one source participating in a WEC subscription.</summary>
public sealed class CollectorSubscriptionSourceRuntimeStatus {
    /// <summary>Source address reported by Windows.</summary>
    public string Address { get; set; } = string.Empty;
    /// <summary>Active, Inactive, Trying, or unknown status.</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>Events processed from this source when reported.</summary>
    public long? EventsProcessed { get; set; }
    /// <summary>Windows error code.</summary>
    public uint? LastErrorCode { get; set; }
    /// <summary>Windows error or provider message.</summary>
    public string? ErrorMessage { get; set; }
    /// <summary>Last heartbeat timestamp when reported.</summary>
    public DateTimeOffset? LastHeartbeatTime { get; set; }
    /// <summary>True when the source is active with no Windows error.</summary>
    public bool IsHealthy =>
        string.Equals(Status, "Active", StringComparison.OrdinalIgnoreCase) &&
        (!LastErrorCode.HasValue || LastErrorCode.Value == 0);
}

/// <summary>Readiness assessment for the local Windows Event Collector host.</summary>
public sealed class CollectorReadinessStatus {
    /// <summary>Collector machine name.</summary>
    public string MachineName { get; set; } = string.Empty;
    /// <summary>Whether the current process is elevated.</summary>
    public bool IsAdministrator { get; set; }
    /// <summary>Whether Wecsvc is installed.</summary>
    public bool CollectorServiceInstalled { get; set; }
    /// <summary>Whether Wecsvc is running.</summary>
    public bool CollectorServiceRunning { get; set; }
    /// <summary>Configured Wecsvc start mode.</summary>
    public string CollectorServiceStartMode { get; set; } = string.Empty;
    /// <summary>Whether WinRM is running.</summary>
    public bool WinRmServiceRunning { get; set; }
    /// <summary>Whether an enabled HTTP or HTTPS WinRM listener is registered.</summary>
    public bool WinRmListenerAvailable { get; set; }
    /// <summary>Whether ForwardedEvents exists.</summary>
    public bool ForwardedEventsExists { get; set; }
    /// <summary>Whether ForwardedEvents is enabled.</summary>
    public bool ForwardedEventsEnabled { get; set; }
    /// <summary>Actionable readiness findings.</summary>
    public IReadOnlyList<string> Issues { get; set; } = Array.Empty<string>();
    /// <summary>True when the collector prerequisites are ready for subscription activation.</summary>
    public bool IsReady => Issues.Count == 0;
}
