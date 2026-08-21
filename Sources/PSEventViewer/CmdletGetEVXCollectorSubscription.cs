namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Returns normalized Windows Event Collector subscription configuration.</para>
/// <para type="description">Reads local or remote WEC subscription inventory and returns detached snapshots with normalized XML details and query definitions. Remote access uses the caller's Windows identity.</para>
/// </summary>
/// <example>
///   <summary>List enabled local subscriptions</summary>
///   <code>Get-EVXCollectorSubscription -EnabledOnly</code>
///   <para>Returns only enabled subscriptions from the local collector.</para>
/// </example>
/// <example>
///   <summary>Find subscriptions on a remote collector</summary>
///   <code>Get-EVXCollectorSubscription -Name '*Domain Controllers*' -MachineName WEC01</code>
///   <para>Uses Remote Registry access under the current Windows identity and applies wildcard matching to detached snapshots.</para>
/// </example>
/// <example>
///   <summary>Check collector prerequisites</summary>
///   <code>Get-EVXCollectorSubscription -Readiness</code>
///   <para>Reports the WEC service, WinRM listener, ForwardedEvents channel, elevation, and actionable readiness issues.</para>
/// </example>
/// <example>
///   <summary>Inspect live source health</summary>
///   <code>Get-EVXCollectorSubscription -Name 'Domain controller authentication' -IncludeRuntimeStatus</code>
///   <para>Adds processed-event counters, source heartbeat timestamps, and native Windows errors to the local snapshot.</para>
/// </example>
[Cmdlet(VerbsCommon.Get, "EVXCollectorSubscription", DefaultParameterSetName = "Subscriptions")]
[OutputType(typeof(CollectorSubscriptionSnapshot), typeof(CollectorReadinessStatus))]
public sealed class CmdletGetEVXCollectorSubscription : AsyncPSCmdlet {
    /// <summary>Subscription names or wildcard patterns.</summary>
    [Parameter(Position = 0, ParameterSetName = "Subscriptions")]
    public string[] Name { get; set; } = new[] { "*" };

    /// <summary>Collector computers. Omit for the local computer.</summary>
    [Parameter(ValueFromPipelineByPropertyName = true, ParameterSetName = "Subscriptions")]
    [Alias("ComputerName", "ServerName")]
    public string[] MachineName { get; set; } = Array.Empty<string>();

    /// <summary>Returns only enabled subscriptions.</summary>
    [Parameter(ParameterSetName = "Subscriptions")]
    public SwitchParameter EnabledOnly { get; set; }

    /// <summary>Includes current per-source runtime state and Windows error details. Runtime status is local-only.</summary>
    [Parameter(ParameterSetName = "Subscriptions")]
    public SwitchParameter IncludeRuntimeStatus { get; set; }

    /// <summary>Returns local WEC, WinRM listener, and ForwardedEvents readiness instead of subscription inventory.</summary>
    [Parameter(Mandatory = true, ParameterSetName = "Readiness")]
    public SwitchParameter Readiness { get; set; }

    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        if (ParameterSetName == "Readiness") {
            WriteObject(CollectorSubscriptionManager.GetCollectorReadiness(CancelToken));
            return Task.CompletedTask;
        }
        WildcardPattern[] patterns = Name
            .Select(static value => value?.Trim() ?? string.Empty)
            .Where(static value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static value => new WildcardPattern(
                value,
                WildcardOptions.IgnoreCase |
                WildcardOptions.CultureInvariant))
            .ToArray();
        if (patterns.Length == 0) {
            throw new PSArgumentException(
                "Name requires at least one non-empty wildcard pattern.");
        }

        string?[] machines = MachineName.Length == 0
            ? new string?[] { null }
            : EventLogTarget
                .NormalizeMachineNames(MachineName)
                .ToArray();
        if (IncludeRuntimeStatus.IsPresent && machines.Any(static machine => machine != null)) {
            throw new PSArgumentException("IncludeRuntimeStatus is available only for the local collector. Run the command on a remote collector through PowerShell remoting when runtime status is required.");
        }
        foreach (string? machineName in machines) {
            CancelToken.ThrowIfCancellationRequested();
            IReadOnlyList<CollectorSubscriptionSnapshot> snapshots =
                CollectorSubscriptionManager
                    .GetCollectorSubscriptionSnapshots(
                        machineName,
                        enabledOnly: EnabledOnly.IsPresent);
            foreach (CollectorSubscriptionSnapshot snapshot in snapshots) {
                CancelToken.ThrowIfCancellationRequested();
                if (patterns.Any(pattern =>
                        pattern.IsMatch(
                            snapshot.SubscriptionName))) {
                    if (IncludeRuntimeStatus.IsPresent) {
                        snapshot.RuntimeStatus = CollectorSubscriptionManager
                            .GetCollectorSubscriptionRuntimeStatus(snapshot.SubscriptionName, CancelToken);
                    }
                    WriteObject(snapshot);
                }
            }
        }
        return Task.CompletedTask;
    }
}
