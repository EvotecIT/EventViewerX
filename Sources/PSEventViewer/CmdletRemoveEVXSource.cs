namespace PSEventViewer;

using System.Diagnostics;
using System.Management.Automation;
using System.Threading.Tasks;
using EventViewerX;

/// <summary>
/// <para type="synopsis">Removes an event source from Windows Event Log.</para>
/// <para type="description">Deletes the provider registration locally or on a remote machine with optional log scoping.</para>
/// </summary>
/// <example>
///   <summary>Remove local source</summary>
///   <code>Remove-EVXSource -SourceName MyApp</code>
///   <para>Unregisters the MyApp event source on the local computer.</para>
/// </example>
/// <example>
///   <summary>Remove source on remote server</summary>
///   <code>Remove-EVXSource -SourceName MyApp -MachineName SRV01</code>
///   <para>Targets the specified remote machine.</para>
/// </example>
/// <example>
///   <summary>Specify log scope</summary>
///   <code>Remove-EVXSource -SourceName MyApp -LogName Application</code>
///   <para>Limits the lookup to the Application log when removing the source.</para>
/// </example>
[Cmdlet(VerbsCommon.Remove, "EVXSource", SupportsShouldProcess = true)]
[OutputType(typeof(bool))]
public sealed class CmdletRemoveEVXSource : AsyncPSCmdlet {
    /// <summary>
    /// Name of the event source to remove.
    /// </summary>
    [Parameter(Mandatory = true, Position = 0)]
    [Alias("Source", "Provider")]
    public string SourceName { get; set; } = null!;

    /// <summary>
    /// Optional log name to scope source checks (avoids probing Security/State). Defaults to Application when specified.
    /// </summary>
    [Parameter]
    public string? LogName { get; set; }

    /// <summary>
    /// Target computer where the source resides.
    /// </summary>
    [Parameter]
    [Alias("ComputerName", "ServerName")]
    public string? MachineName { get; set; }

    /// <summary>
    /// Removes the specified event source from the system.
    /// </summary>
    protected override Task ProcessRecordAsync() {
        try {
            string target = string.IsNullOrEmpty(MachineName)
                ? SourceName
                : $"{SourceName} on {MachineName}";

            if (!ShouldProcess(target, "Delete event source")) {
                return Task.CompletedTask;
            }

            bool removed = ClassicEventLogManager.RemoveSource(
                SourceName,
                MachineName,
                LogName);
            if (!removed) {
                WriteWarning(string.IsNullOrEmpty(MachineName)
                    ? $"Remove-EVXSource - Source {SourceName} was not found."
                    : $"Remove-EVXSource - Source {SourceName} was not found on {MachineName}.");
            }

            WriteObject(removed);
        } catch (Exception ex) {
            WriteError(new ErrorRecord(ex, "RemoveEVXSourceFailed", ErrorCategory.InvalidOperation, SourceName));
        }
        return Task.CompletedTask;
    }
}
