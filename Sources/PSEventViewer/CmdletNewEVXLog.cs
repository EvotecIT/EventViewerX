using System.Diagnostics;
using System.Management.Automation;
using System.Threading.Tasks;
using EventViewerX;

namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Creates a new Windows event log with optional size and retention settings.</para>
/// <para type="description">Applies explicit desired state through ClassicEventLogManager and reports exactly what changed.</para>
/// </summary>
/// <example>
///   <summary>Create custom log</summary>
///   <code>New-EVXLog -LogName MyApp -ProviderName MyApp</code>
///   <para>Creates a new log and provider for application events.</para>
/// </example>
/// <example>
///   <summary>Set size and overwrite policy</summary>
///   <code>New-EVXLog -LogName MyApp -MaximumKilobytes 102400 -OverflowAction OverwriteOlder -RetentionDays 30</code>
///   <para>Limits the log to ~100 MB and retains events for 30 days.</para>
/// </example>
/// <example>
///   <summary>Create log on remote server</summary>
///   <code>New-EVXLog -LogName MyApp -ProviderName MyApp -MachineName SRV01</code>
///   <para>Creates the log on SRV01.</para>
/// </example>
[Cmdlet(VerbsCommon.New, "EVXLog", SupportsShouldProcess = true)]
[OutputType(typeof(ClassicEventLogEnsureResult))]
public sealed class CmdletNewEVXLog : AsyncPSCmdlet {
    /// <summary>
    /// Name of the log to create.
    /// </summary>
    [Parameter(Mandatory = true, Position = 0)]
    public string LogName { get; set; } = null!;

    /// <summary>
    /// Name of the provider associated with the log.
    /// </summary>
    [Alias("Source", "Provider")]
    [Parameter(Position = 1)]
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Target machine on which to create the log.
    /// </summary>
    [Alias("ComputerName", "ServerName")]
    [Parameter]
    public string? MachineName { get; set; }

    /// <summary>
    /// Maximum log size in kilobytes.
    /// </summary>
    [Parameter]
    public long? MaximumKilobytes { get; set; }

    /// <summary>
    /// Overflow behavior when the log is full.
    /// </summary>
    [Parameter]
    public OverflowAction? OverflowAction { get; set; }

    /// <summary>
    /// Minimum days to retain events when using OverwriteOlder policy.
    /// </summary>
    [Parameter]
    public int? RetentionDays { get; set; }

    /// <summary>
    /// Creates the event log with the specified options.
    /// </summary>
    protected override Task ProcessRecordAsync() {
        if (string.IsNullOrEmpty(ProviderName)) {
            ProviderName = LogName;
        }

        try {
            if (ShouldProcess($"{LogName} on {MachineName ?? "localhost"}", "Create event log")) {
                ClassicEventLogEnsureResult result =
                    ClassicEventLogManager.EnsureLog(
                        new ClassicEventLogConfiguration {
                            LogName = LogName,
                            SourceName = ProviderName,
                            MachineName = MachineName,
                            MaximumKilobytes = MaximumKilobytes,
                            OverflowAction = OverflowAction,
                            RetentionDays = RetentionDays
                        });
                WriteObject(result);
            }
        } catch (Exception ex) {
            WriteError(new ErrorRecord(ex, "NewEVXLogFailed", ErrorCategory.InvalidOperation, LogName));
        }

        return Task.CompletedTask;
    }
}
