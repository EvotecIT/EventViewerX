using System;
using System.Diagnostics.Eventing.Reader;

namespace EventViewerX;

/// <summary>
/// Status returned when opening a Windows Event Log session.
/// </summary>
public enum EventLogSessionOpenStatus {
    /// <summary>The Event Log session was opened successfully.</summary>
    Success,
    /// <summary>The local Event Log session could not be opened.</summary>
    LocalSessionUnavailable,
    /// <summary>The host was skipped because it is temporarily cached as unreachable.</summary>
    NegativeCache,
    /// <summary>The Event Log RPC endpoint preflight did not succeed.</summary>
    RpcUnavailable,
    /// <summary>The Event Log session open attempt exceeded the timeout budget.</summary>
    Timeout,
    /// <summary>The caller does not have permission to open the Event Log session.</summary>
    AccessDenied,
    /// <summary>The Event Log session constructor failed.</summary>
    EventLogSessionUnavailable,
    /// <summary>An unexpected error occurred while opening the Event Log session.</summary>
    Error
}

/// <summary>
/// Result of opening a Windows Event Log session, including diagnostics for failure cases.
/// </summary>
public sealed class EventLogSessionOpenResult : IDisposable {
    /// <summary>Input machine name used for the session request.</summary>
    public string? MachineName { get; set; }

    /// <summary>Resolved target host used for reachability checks.</summary>
    public string TargetHost { get; set; } = string.Empty;

    /// <summary>Logical caller or operation name.</summary>
    public string Purpose { get; set; } = string.Empty;

    /// <summary>Event log channel associated with the session request.</summary>
    public string LogName { get; set; } = string.Empty;

    /// <summary>Session open status.</summary>
    public EventLogSessionOpenStatus Status { get; set; }

    /// <summary>True when the session was opened successfully.</summary>
    public bool Success => Status == EventLogSessionOpenStatus.Success && Session != null;

    /// <summary>Opened Event Log session when successful.</summary>
    public EventLogSession? Session { get; set; }

    /// <summary>Diagnostic message when opening the session did not succeed.</summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>Underlying exception or diagnostic type when available.</summary>
    public string ErrorType { get; set; } = string.Empty;

    /// <summary>Timeout budget used for the session open attempt, in milliseconds.</summary>
    public int TimeoutMs { get; set; }

    /// <summary>UTC timestamp until which the target is cached as unreachable.</summary>
    public DateTime? CachedUntilUtc { get; set; }

    /// <inheritdoc />
    public void Dispose()
    {
        Session?.Dispose();
        Session = null;
    }
}
