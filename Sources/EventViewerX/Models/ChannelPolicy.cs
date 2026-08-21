namespace EventViewerX;

using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Net;

/// <summary>
/// Strongly typed policy for a Windows Event Log channel (modern wevt or classic).
/// </summary>
public sealed class ChannelPolicy {
    /// <summary>Log name to apply policy to.</summary>
    public string LogName { get; set; } = string.Empty;
    /// <summary>Optional remote computer name; <c>null</c> targets the local host.</summary>
    public string? MachineName { get; set; }
    /// <summary>Optional credentials for a remote channel-policy session.</summary>
    public NetworkCredential? Credential { get; set; }
    /// <summary>Authentication package for a remote channel-policy session.</summary>
    public EventLogAuthentication Authentication { get; set; }
    /// <summary>Maximum time for RPC preflight and session establishment.</summary>
    public int ConnectionTimeoutMilliseconds { get; set; } = 5000;
    /// <summary>Enables or disables the channel when set.</summary>
    public bool? IsEnabled { get; set; }
    /// <summary>Maximum log size in bytes.</summary>
    public long? MaximumSizeInBytes { get; set; }
    /// <summary>Full file path for the log.</summary>
    public string? LogFilePath { get; set; }
    /// <summary>Isolation level (application/system/custom) reported by Windows. This property is read-only because Windows does not expose a supported channel-policy setter for it.</summary>
    public EventLogIsolation? Isolation { get; internal set; }
    /// <summary>Retention mode for the channel.</summary>
    public EventLogMode? Mode { get; set; }
    /// <summary>Canonical retention mode name for callers that should not bind directly to <see cref="EventLogMode"/>.</summary>
    public string? ModeName => ChannelPolicyModeNames.Normalize(Mode);
    /// <summary>SDDL security descriptor controlling access.</summary>
    public string? SecurityDescriptor { get; set; }

    /// <summary>
    /// Applies a canonical mode name to <see cref="Mode"/>.
    /// </summary>
    public bool TrySetModeName(string? value, out string? error) {
        if (!ChannelPolicyModeNames.TryParse(value, out var mode, out error)) {
            return false;
        }

        Mode = mode;
        return true;
    }

    /// <summary>Serializes the policy into a key/value dictionary for diagnostics or JSON output.</summary>
    public IReadOnlyDictionary<string, object?> ToDictionary() => new Dictionary<string, object?> {
        [nameof(LogName)] = LogName,
        [nameof(MachineName)] = MachineName,
        ["CredentialUserName"] = Credential == null
            ? null
            : string.IsNullOrWhiteSpace(Credential.Domain)
                ? Credential.UserName
                : $"{Credential.Domain}\\{Credential.UserName}",
        [nameof(Authentication)] = Authentication.ToString(),
        [nameof(ConnectionTimeoutMilliseconds)] =
            ConnectionTimeoutMilliseconds,
        [nameof(IsEnabled)] = IsEnabled,
        [nameof(MaximumSizeInBytes)] = MaximumSizeInBytes,
        [nameof(LogFilePath)] = LogFilePath,
        [nameof(Isolation)] = Isolation?.ToString(),
        [nameof(Mode)] = ModeName,
        [nameof(SecurityDescriptor)] = SecurityDescriptor,
    };
}
