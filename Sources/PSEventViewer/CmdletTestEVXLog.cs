namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Runs a bounded Windows Event Log connectivity and query probe.</para>
/// <para type="description">Executes a metadata-only query through the same owned native reader used by Get-EVXEvent, within a fixed budget, and returns the newest matching timestamp plus optional channel metadata.</para>
/// </summary>
/// <example>
///   <summary>Probe the local System log</summary>
///   <code>Test-EVXLog -LogName System</code>
///   <para>Returns a typed status, duration, record count, and newest event timestamp.</para>
/// </example>
/// <example>
///   <summary>Probe a remote Security log with an indexed filter</summary>
///   <code>Test-EVXLog -LogName Security -MachineName DC1 -XPath '*[System[EventID=4624]]' -TimeoutMs 5000</code>
///   <para>Bounds session setup and event reading while distinguishing access, timeout, query, and no-event outcomes.</para>
/// </example>
[Cmdlet(VerbsDiagnostic.Test, "EVXLog")]
[OutputType(typeof(EventLogProbeResult))]
public sealed class CmdletTestEVXLog : AsyncPSCmdlet {
    /// <summary>Channels to probe.</summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true)]
    public string[] LogName { get; set; } = Array.Empty<string>();

    /// <summary>Target computers. Omit for the local computer.</summary>
    [Parameter(ValueFromPipelineByPropertyName = true)]
    [Alias("ComputerName", "ServerName")]
    public string[] MachineName { get; set; } = Array.Empty<string>();

    /// <summary>Optional native XPath expression used to select the newest matching event.</summary>
    [Parameter]
    public string? XPath { get; set; }

    /// <summary>Credentials for remote Event Log sessions.</summary>
    [Credential]
    [Parameter]
    public PSCredential? Credential { get; set; }

    /// <summary>Authentication package for remote Event Log sessions.</summary>
    [Parameter]
    public EventLogAuthentication Authentication { get; set; }

    /// <summary>Total probe budget in milliseconds.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int TimeoutMs { get; set; } = 15000;

    /// <summary>Maximum records inspected before reporting LimitReached.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaxEventsToScan { get; set; } = 4096;

    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        string?[] machines = MachineName.Length == 0
            ? new string?[] { null }
            : EventLogTarget
                .NormalizeMachineNames(MachineName)
                .ToArray();
        if (Credential != null &&
            machines.Any(EventLogTarget.IsLocalMachine)) {
            throw new PSArgumentException(
                "Credential can only be used when every MachineName is remote.");
        }

        foreach (string logName in LogName
                     .Select(static value => value?.Trim() ?? string.Empty)
                     .Where(static value => value.Length > 0)
                     .Distinct(StringComparer.OrdinalIgnoreCase)) {
            foreach (string? machineName in machines) {
                CancelToken.ThrowIfCancellationRequested();
                EventLogProbeResult result =
                    EventLogProbe.ProbeLatestEvent(
                        logName,
                        XPath,
                        machineName,
                        TimeSpan.FromMilliseconds(TimeoutMs),
                        MaxEventsToScan,
                        Credential?.GetNetworkCredential(),
                        Authentication,
                        CancelToken);
                WriteObject(result);
            }
        }
        return Task.CompletedTask;
    }
}
