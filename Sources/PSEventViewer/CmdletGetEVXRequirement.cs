namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Returns event channels, IDs, audit policies, and configuration requirements.</para>
/// <para type="description">Uses the same compiled requirement catalog intended for readiness checks and generated onboarding guidance.</para>
/// </summary>
/// <example>
///   <summary>Inspect weak-authentication requirements</summary>
///   <code>Get-EVXRequirement -Type ADUserLogonNTLMv1, KerberosServiceTicket</code>
///   <para>Returns the required channels, event IDs, audit outcomes, volume guidance, and Microsoft references.</para>
/// </example>
/// <example>
///   <summary>Inspect every built-in type</summary>
///   <code>Get-EVXRequirement</code>
///   <para>Returns one requirement object for each leaf and composite definition.</para>
/// </example>
[Cmdlet(VerbsCommon.Get, "EVXRequirement")]
[OutputType(typeof(EventTypeRequirement))]
public sealed class CmdletGetEVXRequirement : AsyncPSCmdlet {
    /// <summary>Built-in event types to inspect. Omit to return every type.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public EventType[] Type { get; set; } = Array.Empty<EventType>();

    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        IReadOnlyList<EventTypeRequirement> requirements = Type.Length == 0
            ? EventRequirementCatalog.GetRequirements()
            : Type.Distinct().Select(EventRequirementCatalog.GetRequirement).ToArray();
        foreach (EventTypeRequirement requirement in requirements) {
            CancelToken.ThrowIfCancellationRequested();
            WriteObject(requirement);
        }
        return Task.CompletedTask;
    }
}
