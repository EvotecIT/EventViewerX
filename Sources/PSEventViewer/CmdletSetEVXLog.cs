using System.Diagnostics.Eventing.Reader;

namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Updates Windows Event Log channel policy.</para>
/// <para type="description">Configures enabled state, maximum size, retention mode, file path, or security descriptor and returns a detailed per-log result.</para>
/// </summary>
/// <example>
///   <summary>Increase Security and System logs to 1 GB</summary>
///   <code>Set-EVXLog -LogName Security,System -MaximumSizeMB 1024</code>
///   <para>Applies the same policy through the shared EventViewerX channel-policy service.</para>
/// </example>
/// <example>
///   <summary>Enable an operational channel</summary>
///   <code>Set-EVXLog -LogName 'Microsoft-Windows-TaskScheduler/Operational' -Enabled $true</code>
///   <para>Returns which properties were applied, skipped, or failed.</para>
/// </example>
[Cmdlet(VerbsCommon.Set, "EVXLog", SupportsShouldProcess = true)]
[OutputType(typeof(ChannelPolicyApplyResult))]
public sealed class CmdletSetEVXLog : AsyncPSCmdlet {
    /// <summary>Channel names to update.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
    public string[] LogName { get; set; } = Array.Empty<string>();

    /// <summary>Target computers. Omit for the local computer.</summary>
    [Parameter(ValueFromPipelineByPropertyName = true)]
    [Alias("ComputerName", "ServerName")]
    public string[] MachineName { get; set; } = Array.Empty<string>();

    /// <summary>Credentials for remote channel-policy sessions.</summary>
    [Credential]
    [Parameter]
    public PSCredential? Credential { get; set; }

    /// <summary>Authentication package for remote channel-policy sessions.</summary>
    [Parameter]
    public EventLogAuthentication Authentication { get; set; }

    /// <summary>Maximum time for remote RPC preflight and session establishment.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int TimeoutMs { get; set; } = 5000;

    /// <summary>Enables or disables the channel.</summary>
    [Parameter]
    public bool? Enabled { get; set; }

    /// <summary>Maximum channel size in megabytes.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int? MaximumSizeMB { get; set; }

    /// <summary>Maximum channel size in bytes.</summary>
    [Parameter]
    [Alias("MaximumSizeInBytes")]
    [ValidateRange(1, long.MaxValue)]
    public long? MaximumSizeBytes { get; set; }

    /// <summary>Circular, AutoBackup, or Retain channel mode.</summary>
    [Parameter]
    [Alias("LogMode")]
    public EventLogMode? Mode { get; set; }

    /// <summary>Backing log file path.</summary>
    [Parameter]
    public string? LogFilePath { get; set; }

    /// <summary>Channel access-control descriptor in SDDL form.</summary>
    [Parameter]
    public string? SecurityDescriptor { get; set; }

    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        if (MaximumSizeMB.HasValue &&
            MaximumSizeBytes.HasValue) {
            throw new PSArgumentException(
                "MaximumSizeMB and MaximumSizeBytes are mutually exclusive.");
        }
        if (!Enabled.HasValue &&
            !MaximumSizeMB.HasValue &&
            !MaximumSizeBytes.HasValue &&
            !Mode.HasValue &&
            string.IsNullOrWhiteSpace(LogFilePath) &&
            string.IsNullOrWhiteSpace(SecurityDescriptor)) {
            throw new PSArgumentException(
                "Specify at least one channel policy property to update.");
        }

        string?[] machines = MachineName.Length == 0
            ? new string?[] { null }
            : MachineName
                .Select(static machine =>
                    string.IsNullOrWhiteSpace(machine)
                        ? null
                        : machine.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        if (Credential != null &&
            machines.Any(EventLogTarget.IsLocalMachine)) {
            throw new PSArgumentException(
                "Credential can only be used when every MachineName is remote.");
        }
        foreach (string logName in LogName
                     .Select(static log => log?.Trim() ?? string.Empty)
                     .Where(static log => log.Length > 0)
                     .Distinct(StringComparer.OrdinalIgnoreCase)) {
            foreach (string? machine in machines) {
                CancelToken.ThrowIfCancellationRequested();
                string target = string.IsNullOrWhiteSpace(machine)
                    ? logName
                    : $"{machine}\\{logName}";
                if (!ShouldProcess(target, "Update event log channel policy")) {
                    continue;
                }
                long? bytes = MaximumSizeBytes ??
                              (MaximumSizeMB.HasValue
                                  ? checked(
                                      (long)MaximumSizeMB.Value *
                                      1024L *
                                      1024L)
                                  : null);
                var policy = new ChannelPolicy {
                    LogName = logName,
                    MachineName = machine,
                    Credential = Credential?.GetNetworkCredential(),
                    Authentication = Authentication,
                    ConnectionTimeoutMilliseconds =
                        TimeoutMs,
                    IsEnabled = Enabled,
                    MaximumSizeInBytes = bytes,
                    Mode = Mode,
                    LogFilePath = string.IsNullOrWhiteSpace(LogFilePath)
                        ? null
                        : LogFilePath,
                    SecurityDescriptor =
                        string.IsNullOrWhiteSpace(SecurityDescriptor)
                            ? null
                            : SecurityDescriptor
                };
                ChannelPolicyApplyResult result =
                    EventLogChannelPolicyService.ApplyDetailed(
                        policy,
                        cancellationToken: CancelToken);
                WriteObject(result);
                if (!result.Success) {
                    foreach (string error in result.Errors) {
                        WriteError(new ErrorRecord(
                            new InvalidOperationException(error),
                            "EVXLogPolicyUpdateFailed",
                            ErrorCategory.WriteError,
                            target));
                    }
                }
            }
        }
        return Task.CompletedTask;
    }
}
