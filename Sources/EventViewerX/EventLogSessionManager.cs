using System;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Collections.Concurrent;
using System.Threading;
using System.Net;
using EventViewerX.Native;

namespace EventViewerX;

/// <summary>
/// Opens managed Event Log catalog/configuration sessions with bounded connection time
/// and a short shared cache for unreachable remote hosts.
/// </summary>
public static class EventLogSessionManager {
    private static int DefaultSessionTimeoutMs => Settings.SessionTimeoutMs;
    private static int DefaultRpcProbeTimeoutMs => Settings.RpcProbeTimeoutMs;

    /// <summary>How long to remember unreachable hosts to avoid repeated slow probes.</summary>
    private static int NegativeCacheTtlSecondsValue => Settings.NegativeCacheTtlSeconds;

    private static readonly ConcurrentDictionary<string, DateTime> _unreachable = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates an EventLogSession with a timeout and a quick reachability check to avoid long hangs.
    /// Returns null and logs a warning on timeout or failure.
    /// </summary>
    internal static EventLogSession? CreateSession(
        string? machineName,
        string? purpose,
        string? logName,
        int? timeoutMs = null,
        CancellationToken cancellationToken = default) {

        return CreateSessionResult(
            machineName,
            purpose,
            logName,
            timeoutMs,
            cancellationToken: cancellationToken).Session;
    }

    internal static EventLogSessionOpenResult CreateSessionResult(
        string? machineName,
        string? purpose,
        string? logName,
        int? timeoutMs = null,
        Func<EventLogSession>? localSessionFactory = null,
        Func<string, int, bool>? rpcProbeOverride = null,
        Func<string, int, RpcEndpointProbeStatus>?
            rpcProbeStatusOverride = null,
        Func<string, EventLogSession>? remoteSessionFactory = null,
        bool emitDiagnostics = true,
        NetworkCredential? credential = null,
        EventLogAuthentication authentication =
            EventLogAuthentication.Default,
        CancellationToken cancellationToken = default) {

        cancellationToken.ThrowIfCancellationRequested();
        int budget = timeoutMs ?? DefaultSessionTimeoutMs;
        if (budget <= 0) {
            throw new ArgumentOutOfRangeException(nameof(timeoutMs), "Session timeout must be positive.");
        }
        string operation = purpose ?? "Session";
        string channel = logName ?? string.Empty;
        var stopwatch = Stopwatch.StartNew();
        if (!Enum.IsDefined(
                typeof(EventLogAuthentication),
                authentication)) {
            throw new ArgumentOutOfRangeException(
                nameof(authentication),
                "The event-log session authentication value is not supported.");
        }

        // Local is fast; avoid ping/RPC probes (many CI agents block 135)
        if (EventLogTarget.IsLocalMachine(machineName)) {
            if (credential != null) {
                throw new ArgumentException(
                    "Credentials can only be used with a remote event-log session.",
                    nameof(credential));
            }
            try {
                EventLogSession session = BoundedNativeOperation.Execute(
                    localSessionFactory ?? (static () => new EventLogSession()),
                    budget,
                    $"Timed out opening the local Event Log session for '{channel}' after {budget} ms.",
                    cancellationToken,
                    static lateSession => lateSession.Dispose());
                return SessionSuccess(machineName, EventLogTarget.LocalMachineName, operation, channel, budget, session);
            } catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch (TimeoutException ex) {
                if (emitDiagnostics) {
                    Settings._logger.WriteWarning($"{operation}: {ex.Message}");
                }
                return SessionFailure(
                    machineName,
                    EventLogTarget.LocalMachineName,
                    operation,
                    channel,
                    EventLogSessionOpenStatus.Timeout,
                    ex.Message,
                    budget,
                    ex.GetType().Name);
            } catch (Exception ex) {
                if (emitDiagnostics) {
                    Settings._logger.WriteWarning($"{operation}: failed to open local session for '{channel}': {ex.Message}");
                }
                return SessionFailure(
                    machineName,
                    EventLogTarget.LocalMachineName,
                    operation,
                    channel,
                    EventLogSessionOpenStatus.LocalSessionUnavailable,
                    $"Failed to open local Event Log session for '{channel}': {ex.Message}",
                    budget,
                    ex.GetType().Name);
            }
        }

        var normalizedHost = machineName?.Trim() ?? string.Empty;
        var targetHost = string.IsNullOrWhiteSpace(normalizedHost) ? EventLogTarget.LocalMachineName : normalizedHost;
        if (credential == null &&
            authentication != EventLogAuthentication.Default) {
            throw new ArgumentException(
                "An explicit remote authentication package requires a credential because the managed EventLogSession current-identity overload cannot enforce an authentication package.",
                nameof(authentication));
        }
        if (TryGetHostNegativeCacheExpiry(normalizedHost, out DateTime cachedUntilUtc)) {
            if (emitDiagnostics) {
                Settings._logger.WriteVerbose($"{operation}: skipping {normalizedHost} (cached unreachable)");
            }
            return SessionFailure(
                machineName,
                targetHost,
                operation,
                channel,
                EventLogSessionOpenStatus.NegativeCache,
                $"Host '{targetHost}' is temporarily cached as unreachable until {cachedUntilUtc:u}.",
                budget,
                nameof(EventLogSessionOpenStatus.NegativeCache),
                cachedUntilUtc);
        }

        // RPC (135) preflight to avoid EventLogSession hangs on dead/filtered hosts
        int rpcBudget = Math.Min(DefaultRpcProbeTimeoutMs, RemainingSessionBudget(budget, stopwatch));
        if (rpcBudget <= 0) {
            return SessionFailure(
                machineName,
                targetHost,
                operation,
                channel,
                EventLogSessionOpenStatus.Timeout,
                $"Timed out opening Event Log session to '{targetHost}' for '{channel}' after {budget} ms.",
                budget,
                nameof(EventLogSessionOpenStatus.Timeout));
        }
        RpcEndpointProbeStatus rpcStatus;
        try {
            if (rpcProbeStatusOverride != null) {
                rpcStatus = BoundedNativeOperation.Execute(
                    () => rpcProbeStatusOverride(
                        normalizedHost,
                        rpcBudget),
                    rpcBudget,
                    $"Timed out probing RPC on '{targetHost}' after {rpcBudget} ms.",
                    cancellationToken);
            } else if (rpcProbeOverride != null) {
                bool rpcAvailable = BoundedNativeOperation.Execute(
                    () => rpcProbeOverride(
                        normalizedHost,
                        rpcBudget),
                    rpcBudget,
                    $"Timed out probing RPC on '{targetHost}' after {rpcBudget} ms.",
                    cancellationToken);
                rpcStatus = rpcAvailable
                    ? RpcEndpointProbeStatus.Connected
                    : RpcEndpointProbeStatus.Failed;
            } else {
                rpcStatus = RpcProbe(
                    normalizedHost,
                    rpcBudget,
                    emitDiagnostics,
                    cancellationToken);
            }
        } catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (BoundedNativeOperationAdmissionTimeoutException exception) {
            if (emitDiagnostics) {
                Settings._logger.WriteWarning(
                    $"{operation}: {exception.Message}");
            }
            return SessionFailure(
                machineName,
                targetHost,
                operation,
                channel,
                EventLogSessionOpenStatus.Timeout,
                exception.Message,
                budget,
                exception.GetType().Name);
        } catch (TimeoutException exception) {
            if (emitDiagnostics) {
                Settings._logger.WriteWarning(
                    $"{operation}: {exception.Message}");
            }
            return SessionFailure(
                machineName,
                targetHost,
                operation,
                channel,
                EventLogSessionOpenStatus.Timeout,
                exception.Message,
                budget,
                exception.GetType().Name);
        }
        if (rpcStatus == RpcEndpointProbeStatus.TimedOut) {
            return SessionFailure(
                machineName,
                targetHost,
                operation,
                channel,
                EventLogSessionOpenStatus.Timeout,
                $"Timed out probing RPC on '{targetHost}' after {rpcBudget} ms.",
                budget,
                nameof(TimeoutException));
        }
        if (rpcStatus == RpcEndpointProbeStatus.Failed) {
            if (emitDiagnostics) {
                Settings._logger.WriteVerbose($"{operation}: RPC preflight failed for '{machineName}'");
            }
            MarkHostUnreachable(normalizedHost);
            TryGetHostNegativeCacheExpiry(normalizedHost, out DateTime rpcCachedUntilUtc);
            return SessionFailure(
                machineName,
                targetHost,
                operation,
                channel,
                EventLogSessionOpenStatus.RpcUnavailable,
                $"RPC preflight to '{targetHost}' on port {Settings.RpcProbePort} failed within the {budget} ms session budget.",
                budget,
                nameof(EventLogSessionOpenStatus.RpcUnavailable),
                rpcCachedUntilUtc);
        }

        try {
            int sessionBudget = RemainingSessionBudget(budget, stopwatch);
            if (sessionBudget <= 0) {
                return SessionFailure(
                    machineName,
                    targetHost,
                    operation,
                    channel,
                    EventLogSessionOpenStatus.Timeout,
                    $"Timed out opening Event Log session to '{targetHost}' for '{channel}' after {budget} ms.",
                    budget,
                    nameof(EventLogSessionOpenStatus.Timeout));
            }

            Func<string, EventLogSession> sessionFactory =
                remoteSessionFactory ??
                (host => credential == null
                    ? new EventLogSession(host)
                    : new EventLogSession(
                        host,
                        credential.Domain,
                        credential.UserName,
                        credential.SecurePassword,
                        MapSessionAuthentication(authentication)));
            EventLogSession session = BoundedNativeOperation.Execute(
                () => sessionFactory(normalizedHost),
                sessionBudget,
                $"Timed out opening Event Log session to '{targetHost}' for '{channel}' after {budget} ms.",
                cancellationToken,
                static lateSession => lateSession.Dispose());
            // Success: clear any stale negative entry
            ClearNegativeCache(normalizedHost);
            return SessionSuccess(machineName, targetHost, operation, channel, budget, session);
        } catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (BoundedNativeOperationAdmissionTimeoutException ex) {
            if (emitDiagnostics) {
                Settings._logger.WriteWarning($"{operation}: {ex.Message}");
            }
            return SessionFailure(
                machineName,
                targetHost,
                operation,
                channel,
                EventLogSessionOpenStatus.Timeout,
                ex.Message,
                budget,
                ex.GetType().Name);
        } catch (TimeoutException ex) {
            if (emitDiagnostics) {
                Settings._logger.WriteWarning($"{operation}: {ex.Message}");
            }
            return SessionFailure(
                machineName,
                targetHost,
                operation,
                channel,
                EventLogSessionOpenStatus.Timeout,
                ex.Message,
                budget,
                ex.GetType().Name);
        } catch (UnauthorizedAccessException ex) {
            if (emitDiagnostics) {
                Settings._logger.WriteWarning($"{operation}: access denied opening session to '{machineName}' for '{channel}': {ex.Message}");
            }
            return SessionFailure(
                machineName,
                targetHost,
                operation,
                channel,
                EventLogSessionOpenStatus.AccessDenied,
                ex.Message,
                budget,
                ex.GetType().Name);
        } catch (EventLogException ex) {
            if (emitDiagnostics) {
                Settings._logger.WriteWarning($"{operation}: failed opening session to '{machineName}' for '{channel}': {ex.Message}");
            }
            return SessionFailure(
                machineName,
                targetHost,
                operation,
                channel,
                EventLogSessionOpenStatus.EventLogSessionUnavailable,
                ex.Message,
                budget,
                ex.GetType().Name);
        } catch (Exception ex) {
            if (emitDiagnostics) {
                Settings._logger.WriteWarning($"{operation}: unexpected error opening session to '{machineName}' for '{channel}': {ex.Message}");
            }
            return SessionFailure(
                machineName,
                targetHost,
                operation,
                channel,
                EventLogSessionOpenStatus.Error,
                ex.Message,
                budget,
                ex.GetType().Name);
        }
    }

    internal static EventLogSession OpenRequiredSession(
        string? machineName,
        string purpose,
        string? logName,
        int timeoutMilliseconds,
        NetworkCredential? credential = null,
        EventLogAuthentication authentication =
            EventLogAuthentication.Default,
        CancellationToken cancellationToken = default) {

        using EventLogSessionOpenResult result =
            CreateSessionResult(
                machineName,
                purpose,
                logName,
                timeoutMilliseconds,
                emitDiagnostics: false,
                credential: credential,
                authentication: authentication,
                cancellationToken: cancellationToken);
        if (!result.Success ||
            result.Session == null) {
            throw new EventLogSessionException(
                result,
                string.IsNullOrWhiteSpace(
                    result.ErrorMessage)
                    ? $"Failed to open an Event Log session to '{result.TargetHost}'."
                    : result.ErrorMessage);
        }
        EventLogSession session = result.Session;
        result.Session = null;
        return session;
    }

    private static SessionAuthentication MapSessionAuthentication(
        EventLogAuthentication authentication) {

        return authentication switch {
            EventLogAuthentication.Default => SessionAuthentication.Default,
            EventLogAuthentication.Negotiate => SessionAuthentication.Negotiate,
            EventLogAuthentication.Kerberos => SessionAuthentication.Kerberos,
            EventLogAuthentication.Ntlm => SessionAuthentication.Ntlm,
            _ => throw new ArgumentOutOfRangeException(nameof(authentication))
        };
    }

    private static int RemainingSessionBudget(int budget, Stopwatch stopwatch) {
        long remaining = budget - stopwatch.ElapsedMilliseconds;
        return remaining > 0 ? (int)Math.Min(int.MaxValue, remaining) : 0;
    }

    /// <summary>
    /// Public helper that exposes the fast session creation logic (shared reachability cache + RPC probe).
    /// </summary>
    public static EventLogSession? OpenSession(
        string? machineName,
        int? timeoutMs = null,
        string? purpose = null,
        string? logName = null,
        CancellationToken cancellationToken = default) {

        return CreateSession(
            machineName,
            purpose,
            logName,
            timeoutMs,
            cancellationToken);
    }

    /// <summary>
    /// Opens an Event Log session and returns diagnostic details when the session cannot be created.
    /// </summary>
    public static EventLogSessionOpenResult OpenSessionResult(
        string? machineName,
        int? timeoutMs = null,
        string? purpose = null,
        string? logName = null,
        CancellationToken cancellationToken = default) {

        return CreateSessionResult(
            machineName,
            purpose,
            logName,
            timeoutMs,
            emitDiagnostics: false,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Clears any cached unreachable state for a specific host.
    /// </summary>
    public static void ClearHostCache(string host) {
        ClearNegativeCache(host);
    }

    /// <summary>
    /// Clears all cached unreachable hosts.
    /// </summary>
    public static void ClearAllHostCache() {
        _unreachable.Clear();
    }

    private static RpcEndpointProbeStatus RpcProbe(
        string host,
        int timeoutMs,
        bool emitDiagnostics,
        CancellationToken cancellationToken) {

        RpcEndpointProbeStatus status = RpcEndpointProbe.Probe(
            host,
            Settings.RpcProbePort,
            timeoutMs,
            cancellationToken);
        if (status != RpcEndpointProbeStatus.Connected &&
            emitDiagnostics) {
            Settings._logger.WriteVerbose($"Session: RPC probe failed for '{host}'.");
        }
        return status;
    }

    private static bool IsHostNegativeCached(string host) {
        try {
            return TryGetHostNegativeCacheExpiry(host, out _);
        }
        catch (Exception ex) {
            Settings._logger.WriteVerbose($"Negative cache check failed for '{host}': {ex.Message}");
            return false;
        }
    }

    internal static bool TryGetHostNegativeCacheExpiry(string host, out DateTime until) {
        until = default;
        try {
            if (string.IsNullOrWhiteSpace(host)) return false;
            string lower = host.ToLowerInvariant();
            if (_unreachable.TryGetValue(lower, out until)) {
                if (until > DateTime.UtcNow) return true;
                // Older frameworks don't expose TryRemove(KeyValuePair<,>), fall back to key+out.
                _unreachable.TryRemove(lower, out _);
            }
            until = default;
            return false;
        }
        catch (Exception ex) {
            Settings._logger.WriteVerbose($"Negative cache check failed for '{host}': {ex.Message}");
            until = default;
            return false;
        }
    }

    internal static void MarkHostUnreachable(string host) {
        try {
            if (string.IsNullOrWhiteSpace(host)) return;
            string lower = host.ToLowerInvariant();
            _unreachable[lower] = DateTime.UtcNow.AddSeconds(NegativeCacheTtlSecondsValue);
        }
        catch (Exception ex) {
            Settings._logger.WriteVerbose($"Failed to mark '{host}' unreachable: {ex.Message}");
        }
    }

    internal static void ClearNegativeCache(string host) {
        try {
            if (string.IsNullOrWhiteSpace(host)) return;
            string lower = host.ToLowerInvariant();
            _unreachable.TryRemove(lower, out _);
        }
        catch (Exception ex) {
            Settings._logger.WriteVerbose($"Failed clearing negative cache for '{host}': {ex.Message}");
        }
    }

    private static EventLogSessionOpenResult SessionSuccess(
        string? machineName,
        string targetHost,
        string purpose,
        string logName,
        int timeoutMs,
        EventLogSession session) =>
        new()
        {
            MachineName = machineName,
            TargetHost = targetHost,
            Purpose = purpose,
            LogName = logName,
            Status = EventLogSessionOpenStatus.Success,
            Session = session,
            TimeoutMs = timeoutMs
        };

    private static EventLogSessionOpenResult SessionFailure(
        string? machineName,
        string targetHost,
        string purpose,
        string logName,
        EventLogSessionOpenStatus status,
        string errorMessage,
        int timeoutMs,
        string? errorType = null,
        DateTime? cachedUntilUtc = null) =>
        new()
        {
            MachineName = machineName,
            TargetHost = targetHost,
            Purpose = purpose,
            LogName = logName,
            Status = status,
            ErrorMessage = errorMessage,
            ErrorType = errorType ?? status.ToString(),
            TimeoutMs = timeoutMs,
            CachedUntilUtc = cachedUntilUtc
        };
}
