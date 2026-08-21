using EventViewerX;
using System.Collections.Generic;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSEventViewer;

/// <summary>Retrieves reconstructed PowerShell scripts or execution-context records from event logs.</summary>
/// <example>
///   <summary>Recover script blocks from two computers</summary>
///   <code>Get-EVXPowerShellScript -Edition WindowsPowerShell -MachineName DC01,DC02 -OutputPath C:\RecoveredScripts -MaxScripts 100</code>
///   <para>Reconstructs scripts and streams the saved paths.</para>
/// </example>
/// <example>
///   <summary>Read execution context instead of script text</summary>
///   <code>Get-EVXPowerShellScript -Execution -Edition WindowsPowerShell -MachineName DC01 -MaxEvents 100</code>
///   <para>Selects the execution parameter set without introducing another cmdlet.</para>
/// </example>
[Cmdlet(VerbsCommon.Get, "EVXPowerShellScript", DefaultParameterSetName = "Script")]
[OutputType(typeof(RestoredPowerShellScript), typeof(PowerShellScriptExecutionInfo), typeof(PowerShellScriptQueryExecutionInfo), typeof(string))]
public sealed class CmdletGetEVXPowerShellScript : PowerShellScriptQueryCmdletBase {
    /// <summary>Returns execution-context records instead of reconstructed script text.</summary>
    [Parameter(Mandatory = true, ParameterSetName = "Execution")]
    public SwitchParameter Execution { get; set; }

    /// <summary>Destination directory where retrieved scripts should be saved.</summary>
    [Alias("Path")]
    [Parameter(ParameterSetName = "Script")]
    public string? OutputPath { get; set; }

    /// <summary>When set, converts scripts back to their original formatting.</summary>
    [Parameter(ParameterSetName = "Script")]
    public SwitchParameter Format { get; set; }

    /// <summary>Filters scripts to those containing the specified text.</summary>
    [Parameter(ParameterSetName = "Script")]
    public string[]? ContainsText { get; set; }

    /// <summary>Maximum reconstructed scripts to return per computer. Zero returns every matching script.</summary>
    [Parameter(ParameterSetName = "Script")]
    [ValidateRange(0, int.MaxValue)]
    public int MaxScripts { get; set; }

    /// <summary>Maximum execution-context records to return per computer. Zero returns every match.</summary>
    [Parameter(ParameterSetName = "Execution")]
    [ValidateRange(0, int.MaxValue)]
    public int MaxEvents { get; set; }

    /// <summary>Maximum incomplete script groups retained while scanning.</summary>
    [Parameter(ParameterSetName = "Script")]
    [ValidateRange(1, int.MaxValue)]
    public int MaxPendingScripts { get; set; } = PowerShellEventEngine.DefaultPowerShellScriptPendingLimit;

    /// <summary>Maximum event snapshots retained across incomplete script groups.</summary>
    [Parameter(ParameterSetName = "Script")]
    [ValidateRange(1, int.MaxValue)]
    public int MaxCachedEvents { get; set; } = PowerShellEventEngine.DefaultPowerShellScriptEventCacheLimit;

    /// <summary>Retrieves matching scripts and writes each result or saved path to the pipeline.</summary>
    protected override Task ProcessRecordAsync() {
        string?[] machines = GetQueryTargets();
        if (Execution.IsPresent) {
            WriteExecutions(machines);
            return Task.CompletedTask;
        }

        foreach (string? machine in machines) {
            CancelToken.ThrowIfCancellationRequested();
            var queryInfo = new PowerShellScriptQueryExecutionInfo();
            using IEnumerator<RestoredPowerShellScript> scripts = PowerShellEventEngine.GetPowerShellScripts(
                type: Edition,
                machineName: machine,
                eventLogPath: EventLogPath,
                dateFrom: StartTime,
                dateTo: EndTime,
                format: Format.IsPresent,
                containsText: ContainsText,
                maxScripts: MaxScripts,
                maxEventsScanned: MaxEventsScanned,
                maxPendingScripts: MaxPendingScripts,
                maxCachedEvents: MaxCachedEvents,
                cancellationToken: CancelToken,
                executionInfo: queryInfo).GetEnumerator();
            try {
                while (TryMoveNextRemote(scripts, machine, queryInfo)) {
                    RestoredPowerShellScript script = scripts.Current;
                    if (!string.IsNullOrEmpty(OutputPath)) {
                        string path = script.Save(OutputPath!);
                        WriteObject(path);
                    } else {
                        WriteObject(script);
                    }
                }
            } finally {
                WriteQueryCompletion(queryInfo);
            }
        }

        return Task.CompletedTask;
    }

    private void WriteExecutions(IEnumerable<string?> machines) {
        foreach (string? machine in machines) {
            CancelToken.ThrowIfCancellationRequested();
            var queryInfo = new PowerShellScriptQueryExecutionInfo();
            using IEnumerator<PowerShellScriptExecutionInfo> executions =
                PowerShellEventEngine.GetPowerShellScriptExecution(
                    type: Edition,
                    machineName: machine,
                    eventLogPath: EventLogPath,
                    dateFrom: StartTime,
                    dateTo: EndTime,
                    maxEvents: MaxEvents,
                    maxEventsScanned: MaxEventsScanned,
                    executionInfo: queryInfo,
                    cancellationToken: CancelToken).GetEnumerator();
            try {
                while (TryMoveNextRemote(executions, machine, queryInfo)) {
                    WriteObject(executions.Current);
                }
            } finally {
                WriteQueryCompletion(queryInfo);
            }
        }
    }
}
