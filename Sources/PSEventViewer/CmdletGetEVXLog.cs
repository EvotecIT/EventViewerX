using System.Management.Automation;
using System.Threading.Tasks;
using EventViewerX;

namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Retrieves event log details by name.</para>
/// <para type="description">Lists log metadata (size, record count, status) on local or remote machines; supports wildcards.</para>
/// </summary>
/// <example>
///   <summary>List security log</summary>
///   <code>Get-EVXLog -LogName Security</code>
///   <para>Shows details for the Security log on the local computer.</para>
/// </example>
/// <example>
///   <summary>Query remote logs</summary>
///   <code>Get-EVXLog -LogName Application,System -MachineName SRV01</code>
///   <para>Retrieves Application and System log info from SRV01.</para>
/// </example>
/// <example>
///   <summary>Use wildcards</summary>
///   <code>Get-EVXLog -LogName "Microsoft-Windows-*"</code>
///   <para>Lists all Microsoft-Windows prefixed logs.</para>
/// </example>
[Cmdlet(
    VerbsCommon.Get,
    "EVXLog",
    DefaultParameterSetName = "Channel")]
[OutputType(typeof(EventLogDetails))]
[OutputType(typeof(EventLogDetailsResult))]
[OutputType(typeof(EventLogFileInformation))]
public sealed class CmdletGetEVXLog : AsyncPSCmdlet {
    /// <summary>
    /// Name of the log to retrieve. Wildcards supported.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ParameterSetName = "Channel")]
    public string[] LogName { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Offline EVTX files whose native archive metadata should be read.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ParameterSetName = "Path")]
    [Alias("FilePath")]
    public string[] Path { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Target machines to query.
    /// </summary>
    [Parameter(ParameterSetName = "Channel")]
    [Alias("ComputerName", "ServerName")]
    public string[] MachineName { get; set; } = Array.Empty<string>();

    /// <summary>Credentials for remote channel enumeration.</summary>
    [Credential]
    [Parameter(ParameterSetName = "Channel")]
    public PSCredential? Credential { get; set; }

    /// <summary>Authentication package for remote channel enumeration.</summary>
    [Parameter(ParameterSetName = "Channel")]
    public EventLogAuthentication Authentication { get; set; }

    /// <summary>
    /// Returns typed diagnostic results for successful, inaccessible, missing, and partially readable logs.
    /// </summary>
    [Parameter(ParameterSetName = "Channel")]
    public SwitchParameter AsResult { get; set; }

    /// <summary>
    /// Session-open timeout in milliseconds.
    /// </summary>
    [Parameter(ParameterSetName = "Channel")]
    [ValidateRange(1, int.MaxValue)]
    public int TimeoutMs { get; set; } = 3000;

    /// <summary>
    /// Reads the oldest and newest event timestamps using the same session. This adds two indexed reads per log.
    /// </summary>
    [Parameter(ParameterSetName = "Channel")]
    public SwitchParameter IncludeEventTimes { get; set; }

    /// <summary>Includes analytic and debug channels when LogName contains wildcards.</summary>
    [Parameter(ParameterSetName = "Channel")]
    public SwitchParameter Force { get; set; }

    /// <summary>
    /// Queries the log information.
    /// </summary>
    protected override Task ProcessRecordAsync() {
        if (ParameterSetName == "Path") {
            foreach (string path in Path
                         .Select(static value => value?.Trim() ?? string.Empty)
                         .Where(static value => value.Length > 0)
                         .Distinct(StringComparer.OrdinalIgnoreCase)) {
                WriteObject(EventLogArchive.GetInformation(path));
            }
            return Task.CompletedTask;
        }

        string?[] machines = MachineName.Length == 0
            ? new string?[] { null }
            : MachineName
                .Select(static value =>
                    string.IsNullOrWhiteSpace(value)
                        ? null
                        : value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        if (Credential != null &&
            machines.Any(EventLogTarget.IsLocalMachine)) {
            throw new PSArgumentException(
                "Credential can only be used when every MachineName is remote.");
        }
        foreach (string? machine in machines) {
            foreach (EventLogDetailsResult result in
                     EventLogCatalog.DisplayEventLogResults(
                         LogName,
                         machine,
                         TimeoutMs,
                         IncludeEventTimes,
                         Credential?.GetNetworkCredential(),
                         Authentication,
                         Force,
                         CancelToken)) {
                if (AsResult) {
                    WriteObject(result);
                } else {
                    if (result.HasDiagnosticFailure) {
                        WriteWarning(result.DiagnosticMessage);
                    }
                    if (result.Details != null) {
                        WriteObject(result.Details);
                    }
                }
            }
        }

        return Task.CompletedTask;
    }
}
