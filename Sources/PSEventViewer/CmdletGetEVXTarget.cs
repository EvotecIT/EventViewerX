namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Discovers local or explicitly selected Active Directory event targets.</para>
/// <para type="description">Returns the local machine by default. Current-domain, current-forest, named-domain, named-forest, and trusted-forest discovery are opt-in and preserve per-domain successes and failures.</para>
/// </summary>
/// <example>
///   <summary>Return the local machine</summary>
///   <code>Get-EVXTarget</code>
///   <para>No Active Directory discovery occurs.</para>
/// </example>
/// <example>
///   <summary>Discover the current forest</summary>
///   <code>Get-EVXTarget -ActiveDirectory CurrentForest</code>
///   <para>Returns one bounded result with domain controllers and any per-domain failures.</para>
/// </example>
/// <example>
///   <summary>Discover a named domain</summary>
///   <code>Get-EVXTarget -ActiveDirectory Domain -Name ad.example.com -Credential (Get-Credential)</code>
///   <para>The credential is used only for this explicitly named directory scope.</para>
/// </example>
[Cmdlet(VerbsCommon.Get, "EVXTarget")]
[OutputType(typeof(EventTargetDiscoveryResult))]
public sealed class CmdletGetEVXTarget : AsyncPSCmdlet {
    /// <summary>Explicit discovery scope. The default is LocalMachine.</summary>
    [Parameter(Position = 0)]
    public EventTargetDiscoveryScope ActiveDirectory { get; set; } = EventTargetDiscoveryScope.LocalMachine;

    /// <summary>DNS name required by Domain or Forest scope.</summary>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>Traverses forest trusts only when forest discovery was explicitly selected.</summary>
    [Parameter]
    public SwitchParameter IncludeTrustedForests { get; set; }

    /// <summary>Credential used only for a named domain or forest.</summary>
    [Credential]
    [Parameter]
    public PSCredential? Credential { get; set; }

    /// <summary>Total discovery budget in milliseconds.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int TimeoutMs { get; set; } = 30000;

    /// <summary>Maximum domains retained by one explicit discovery.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaximumDomainCount { get; set; } = 64;

    /// <summary>Maximum distinct event-log targets retained by one explicit discovery.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaximumTargetCount { get; set; } = 1024;

    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        var request = new EventTargetDiscoveryRequest {
            Scope = ActiveDirectory,
            Name = Name,
            IncludeTrustedForests = IncludeTrustedForests.IsPresent,
            Credential = Credential?.GetNetworkCredential(),
            Timeout = TimeSpan.FromMilliseconds(TimeoutMs),
            MaximumDomainCount = MaximumDomainCount,
            MaximumTargetCount = MaximumTargetCount
        };
        EventTargetDiscoveryResult result = EventTargetResolver.Resolve(request, CancelToken);
        WriteObject(result);
        return Task.CompletedTask;
    }
}
