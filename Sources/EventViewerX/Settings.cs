using System.Threading;

namespace EventViewerX;

/// <summary>
/// Provides logging verbosity switches and default timeouts used throughout EventViewerX.
/// </summary>
public static class Settings {
    private static readonly AsyncLocal<InternalLogger?> ContextLogger = new();
    private static readonly InternalLogger DefaultLogger = new();

    /// <summary>
    /// Logger for the current asynchronous execution context.
    /// </summary>
    /// <remarks>
    /// The value flows to child tasks without allowing concurrent PowerShell runspaces or callers to overwrite
    /// one another's logging callbacks.
    /// </remarks>
    internal static InternalLogger _logger {
        get => ContextLogger.Value ?? DefaultLogger;
        set => ContextLogger.Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Logger for the current asynchronous execution context. Setting it affects
    /// the caller's context and flows to child tasks without changing unrelated callers.
    /// </summary>
    public static InternalLogger Logger {
        get => _logger;
        set => _logger = value;
    }

    /// <summary>TTL (seconds) for negative host reachability cache; adjust for slower/faster networks.</summary>
    public static int NegativeCacheTtlSeconds { get; set; } = 15;

    /// <summary>RPC reachability probe port; defaults to 135 but can be changed for locked-down environments.</summary>
    public static int RpcProbePort { get; set; } = 135;

    /// <summary>Default timeout (ms) when opening EventLogSession connections.</summary>
    public static int SessionTimeoutMs { get; set; } = 5000;

    /// <summary>Timeout (ms) for RPC probe before attempting a session.</summary>
    public static int RpcProbeTimeoutMs { get; set; } = 2500;

    /// <summary>
    /// Stall timeout (ms) while reading events from a log. Values less than or equal to zero disable the stall timeout (unbounded reads).
    /// Session establishment still respects <see cref="SessionTimeoutMs"/>.
    /// </summary>
    public static int QuerySessionTimeoutMs { get; set; } = 0;

}
