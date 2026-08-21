using EventViewerX;
using System;
using System.Collections.Generic;
using System.Management.Automation;

namespace PSEventViewer;

/// <summary>Provides shared parameters and completion reporting for PowerShell script-log cmdlets.</summary>
public abstract class PowerShellScriptQueryCmdletBase : AsyncPSCmdlet {
    /// <summary>PowerShell edition whose operational log should be queried.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [Alias("Type")]
    public PowerShellEdition Edition { get; set; }

    /// <summary>
    /// Computer names to query. When omitted, the local machine is used.
    /// This cannot be combined with <see cref="EventLogPath"/>.
    /// </summary>
    [Alias("ComputerName")]
    [Parameter]
    public string?[]? MachineName { get; set; }

    /// <summary>
    /// Exported EVTX file to query locally instead of a live operational log.
    /// This cannot be combined with <see cref="MachineName"/>.
    /// </summary>
    [Parameter]
    public string? EventLogPath { get; set; }

    /// <summary>Only reads events logged after this date.</summary>
    [Alias("DateFrom")]
    [Parameter]
    public DateTime? StartTime { get; set; }

    /// <summary>Only reads events logged before this date.</summary>
    [Alias("DateTo")]
    [Parameter]
    public DateTime? EndTime { get; set; }

    /// <summary>Reusable relative time window. This cannot be combined with StartTime or EndTime.</summary>
    [Parameter]
    public TimePeriod? TimePeriod { get; set; }

    /// <summary>Maximum native records to scan per computer. Zero scans the complete query.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int MaxEventsScanned { get; set; }

    /// <summary>Emits a machine-readable completion record after each computer.</summary>
    [Parameter]
    public SwitchParameter IncludeQueryInfo { get; set; }

    /// <summary>
    /// Returns the live targets or the single local offline-file target after
    /// validating that mutually exclusive source models were not combined.
    /// </summary>
    protected string?[] GetQueryTargets() {
        (StartTime, EndTime) = EventTimeRange.Resolve(
            StartTime,
            EndTime,
            TimePeriod);
        if (!string.IsNullOrWhiteSpace(
                EventLogPath)) {
            if (MachineName is { Length: > 0 }) {
                throw new PSArgumentException(
                    "EventLogPath reads a local EVTX file and cannot be combined with MachineName.",
                    nameof(MachineName));
            }
            return new string?[] { null };
        }
        return MachineName is { Length: > 0 }
            ? MachineName
            : new string?[] { null };
    }

    /// <summary>Writes incomplete-query diagnostics and optionally emits the completion record.</summary>
    protected void WriteQueryCompletion(PowerShellScriptQueryExecutionInfo queryInfo) {
        if (queryInfo.MayBeIncomplete) {
            string target = !string.IsNullOrWhiteSpace(queryInfo.EventLogPath)
                ? $"'{queryInfo.EventLogPath}'"
                : string.IsNullOrWhiteSpace(queryInfo.MachineName) ? "the local computer" : queryInfo.MachineName;
            WriteWarning(
                $"PowerShell script results from {target} may be incomplete: " +
                $"scanLimitReached={queryInfo.ScanLimitReached}, " +
                $"outputLimitReached={queryInfo.OutputLimitReached}, " +
                $"evictedIncompleteScripts={queryInfo.EvictedIncompleteScripts}, " +
                $"invalidFragmentMetadataEvents={queryInfo.InvalidFragmentMetadataEvents}, " +
                $"incompleteScriptsReturned={queryInfo.IncompleteScriptsReturned}, " +
                $"failureKind={queryInfo.FailureKind}, " +
                $"failureMessage='{queryInfo.FailureMessage}'.");
        }
        if (IncludeQueryInfo.IsPresent) {
            WriteObject(queryInfo);
        }
    }

    /// <summary>Moves a script-log iterator while isolating only expected failures from the current remote target.</summary>
    protected static bool TryMoveNextRemote<T>(
        IEnumerator<T> enumerator,
        string? machine,
        PowerShellScriptQueryExecutionInfo queryInfo) {

        try {
            return enumerator.MoveNext();
        } catch (Exception ex) when (EventLogRemoteQueryFailureClassifier.TryClassify(machine, ex, out EventLogRemoteQueryFailureKind failureKind)) {
            queryInfo.RecordFailure(failureKind, ex.Message);
            return false;
        }
    }
}
