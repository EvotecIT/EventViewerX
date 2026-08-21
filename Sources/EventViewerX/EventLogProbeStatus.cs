namespace EventViewerX;

/// <summary>Status of a bounded event-log probe.</summary>
public enum EventLogProbeStatus {
    /// <summary>Probe succeeded and returned a timestamp.</summary>
    Ok,
    /// <summary>No event matched the query.</summary>
    NoEvent,
    /// <summary>Overall probe timeout was hit.</summary>
    Timeout,
    /// <summary>Scan limit reached without finding a timestamped event.</summary>
    LimitReached,
    /// <summary>The caller does not have permission to read the target.</summary>
    AccessDenied,
    /// <summary>The requested event log does not exist.</summary>
    LogNotFound,
    /// <summary>The supplied XPath query is invalid.</summary>
    InvalidQuery,
    /// <summary>The target host or Event Log RPC endpoint is unavailable.</summary>
    HostUnavailable,
    /// <summary>Probe failed due to another error.</summary>
    Error,
    /// <summary>The complete matching result set contained no usable event timestamp.</summary>
    NoUsableTimestamp
}
