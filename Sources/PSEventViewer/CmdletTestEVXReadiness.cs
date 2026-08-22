namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Assesses EventViewerX prerequisites without changing Windows configuration.</para>
/// <para type="description">Composes explicit target discovery, native Event Log probes, effective local audit policy, observed-event evidence, and safe provider configuration checks. Permission-limited evidence remains Unknown instead of being guessed.</para>
/// </summary>
/// <example>
///   <summary>Assess NTLMv1 and weak Kerberos monitoring on the local machine</summary>
///   <code>Test-EVXReadiness -Type ADUserLogonNTLMv1, KerberosServiceTicket</code>
///   <para>No Active Directory discovery occurs when a target is omitted.</para>
/// </example>
/// <example>
///   <summary>Assess the current forest explicitly</summary>
///   <code>Test-EVXReadiness -Scenario DailyActiveDirectoryReport -ActiveDirectory CurrentForest</code>
///   <para>Each discovered domain and domain controller retains its own success or failure evidence.</para>
/// </example>
/// <example>
///   <summary>Assess a Windows Event Collector</summary>
///   <code>Test-EVXReadiness -Scenario AuthenticationMonitoring -Collector WEC01 -SubscriptionName EventViewerX-Authentication -ActiveDirectory CurrentForest</code>
///   <para>Queries ForwardedEvents and, when run locally on the collector, compares explicitly discovered DCs with subscription runtime enrollment.</para>
/// </example>
[Cmdlet(VerbsDiagnostic.Test, "EVXReadiness", DefaultParameterSetName = "Type")]
[OutputType(typeof(EventReadinessReport))]
public sealed class CmdletTestEVXReadiness : AsyncPSCmdlet {
    /// <summary>Explicit event types to assess.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Type")]
    public EventType[] Type { get; set; } = Array.Empty<EventType>();

    /// <summary>Built-in workflow scenario to assess.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Scenario")]
    [ValidateNotNull]
    public EventReadinessScenario Scenario { get; set; }

    /// <summary>Explicit directory discovery scope. The default is LocalMachine.</summary>
    [Parameter]
    public EventTargetDiscoveryScope ActiveDirectory { get; set; } = EventTargetDiscoveryScope.LocalMachine;

    /// <summary>DNS name required by Domain or Forest scope.</summary>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>Traverses forest trusts only when forest discovery was explicitly selected.</summary>
    [Parameter]
    public SwitchParameter IncludeTrustedForests { get; set; }

    /// <summary>Windows Event Collector assessed instead of direct source computers.</summary>
    [Parameter]
    public string? Collector { get; set; }

    /// <summary>WEC subscription inspected for configuration and runtime source coverage.</summary>
    [Parameter]
    public string? SubscriptionName { get; set; }

    /// <summary>Additional expected source computers for WEC runtime coverage.</summary>
    [Parameter]
    public string[] ExpectedSource { get; set; } = Array.Empty<string>();

    /// <summary>Credential used only for named directory discovery.</summary>
    [Credential]
    [Parameter]
    public PSCredential? DirectoryCredential { get; set; }

    /// <summary>Credential used only for remote Event Log probes.</summary>
    [Credential]
    [Parameter]
    public PSCredential? EventLogCredential { get; set; }

    /// <summary>Authentication package for remote Event Log probes.</summary>
    [Parameter]
    public EventLogAuthentication Authentication { get; set; }

    /// <summary>Total directory discovery budget in milliseconds.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int DiscoveryTimeoutMs { get; set; } = 30000;

    /// <summary>Maximum domains retained by one explicit discovery.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaximumDomainCount { get; set; } = 64;

    /// <summary>Maximum distinct event-log targets retained by one explicit discovery.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaximumTargetCount { get; set; } = 1024;

    /// <summary>Budget for each native Event Log probe in milliseconds.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int ProbeTimeoutMs { get; set; } = 15000;

    /// <summary>Maximum records inspected by each probe.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaxEventsToScan { get; set; } = 4096;

    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        var request = new EventReadinessRequest {
            Types = ParameterSetName == "Type" ? Type : Array.Empty<EventType>(),
            Scenario = ParameterSetName == "Scenario" ? Scenario : EventReadinessScenario.None,
            TargetDiscovery = new EventTargetDiscoveryRequest {
                Scope = ActiveDirectory,
                Name = Name,
                IncludeTrustedForests = IncludeTrustedForests.IsPresent,
                Credential = DirectoryCredential?.GetNetworkCredential(),
                Timeout = TimeSpan.FromMilliseconds(DiscoveryTimeoutMs),
                MaximumDomainCount = MaximumDomainCount,
                MaximumTargetCount = MaximumTargetCount
            },
            Collector = Collector,
            SubscriptionName = SubscriptionName,
            ExpectedSources = ExpectedSource,
            EventLogCredential = EventLogCredential?.GetNetworkCredential(),
            Authentication = Authentication,
            ProbeTimeout = TimeSpan.FromMilliseconds(ProbeTimeoutMs),
            MaxEventsToScan = MaxEventsToScan
        };
        WriteObject(EventReadinessEngine.Evaluate(request, CancelToken));
        return Task.CompletedTask;
    }
}
