namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Clears Windows Event Log channels through the native engine.</para>
/// <para type="description">Supports local or remote channels, explicit credentials, and an optional native EVTX backup. Failures are terminating and retain their Windows error code.</para>
/// </summary>
/// <example>
///   <summary>Back up and clear Application</summary>
///   <code>Clear-EVXLog -LogName Application -BackupPath C:\Backups\Application.evtx</code>
///   <para>Windows writes the backup before clearing the channel.</para>
/// </example>
[Cmdlet(
    VerbsCommon.Clear,
    "EVXLog",
    SupportsShouldProcess = true,
    ConfirmImpact = ConfirmImpact.High)]
[OutputType(typeof(EventLogClearResult))]
public sealed class CmdletClearEVXLog : AsyncPSCmdlet {
    /// <summary>Channel names to clear.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
    public string[] LogName { get; set; } = Array.Empty<string>();

    /// <summary>Remote computer. Omit for local.</summary>
    [Alias("ComputerName", "ServerName")]
    [Parameter]
    public string[] MachineName { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Optional backup EVTX path. This requires exactly one LogName.
    /// </summary>
    [Parameter]
    public string? BackupPath { get; set; }

    /// <summary>Credentials for the remote session.</summary>
    [Credential]
    [Parameter]
    public PSCredential? Credential { get; set; }

    /// <summary>Authentication package for the remote session.</summary>
    [Parameter]
    public EventLogAuthentication Authentication { get; set; }

    /// <summary>Maximum time for remote RPC preflight and session establishment.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int TimeoutMs { get; set; } = 5000;

    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        string[] logs = LogName
            .Select(static log => log?.Trim() ?? string.Empty)
            .Where(static log => log.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (logs.Length == 0) {
            throw new PSArgumentException(
                "LogName requires at least one non-empty channel.");
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
        if (!string.IsNullOrWhiteSpace(BackupPath) &&
            checked(logs.Length * machines.Length) != 1) {
            throw new PSArgumentException(
                "BackupPath requires exactly one LogName and one target computer.");
        }

        foreach (string log in logs) {
            foreach (string? machine in machines) {
                string target = string.IsNullOrWhiteSpace(machine)
                    ? log
                    : $"{machine}\\{log}";
                if (!ShouldProcess(
                        target,
                        "Back up and clear event log channel")) {
                    continue;
                }
                WriteObject(EventLogMaintenance.ClearChannel(
                    log,
                    machine,
                    BackupPath,
                    Credential?.GetNetworkCredential(),
                    Authentication,
                    TimeoutMs,
                    CancelToken));
            }
        }
        return Task.CompletedTask;
    }
}
