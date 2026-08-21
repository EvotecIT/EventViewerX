using System.Management.Automation;
using System.Threading.Tasks;
using EventViewerX;

namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Removes an event log from the system.</para>
/// <para type="description">Supports local or remote removal with ShouldProcess confirmation; useful for cleanup of custom logs.</para>
/// </summary>
/// <example>
///   <summary>Remove local custom log</summary>
///   <code>Remove-EVXLog -LogName MyApp</code>
///   <para>Deletes the MyApp log from the local computer.</para>
/// </example>
/// <example>
///   <summary>Remove log on remote host</summary>
///   <code>Remove-EVXLog -LogName MyApp -MachineName SRV01</code>
///   <para>Deletes the log on SRV01.</para>
/// </example>
/// <example>
///   <summary>Prompt before removal</summary>
///   <code>Remove-EVXLog -LogName MyApp -Confirm</code>
///   <para>Asks for confirmation prior to deletion.</para>
/// </example>
[Cmdlet(VerbsCommon.Remove, "EVXLog", SupportsShouldProcess = true)]
[OutputType(typeof(bool))]
public sealed class CmdletRemoveEVXLog : AsyncPSCmdlet {
    /// <summary>
    /// Name of the log to remove.
    /// </summary>
    [Parameter(Mandatory = true, Position = 0)]
    public string LogName { get; set; } = null!;

    /// <summary>
    /// Target machine from which to remove the log.
    /// </summary>
    [Parameter]
    [Alias("ComputerName", "ServerName")]
    public string? MachineName { get; set; }

    /// <summary>
    /// Removes the specified log.
    /// </summary>
    protected override Task ProcessRecordAsync() {
        try {
            if (ShouldProcess($"{LogName} on {MachineName ?? "localhost"}", "Remove event log")) {
                bool result = ClassicEventLogManager.RemoveLog(
                    LogName,
                    MachineName);
                WriteObject(result);
            }
        } catch (Exception ex) {
            WriteError(new ErrorRecord(ex, "RemoveEVXLogFailed", ErrorCategory.InvalidOperation, LogName));
        }
        return Task.CompletedTask;
    }
}
