namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Gets versioned EventViewerX analysis JSON contracts.</para>
/// <para type="description">Returns Draft 2020-12 schemas for observations, findings, coverage, plans, packs, and rule traces.</para>
/// </summary>
/// <example>
///   <summary>Export the finding schema</summary>
///   <code>Get-EVXAnalysisContract -Kind Finding | Select-Object -ExpandProperty JsonSchema</code>
///   <para>Use the schema to validate downstream JSON integrations.</para>
/// </example>
[Cmdlet(VerbsCommon.Get, "EVXAnalysisContract")]
[OutputType(typeof(EventAnalysisContractDescriptor))]
public sealed class CmdletGetEVXAnalysisContract : PSCmdlet {
    /// <summary>Optional contract kinds. The default returns every supported contract.</summary>
    [Parameter(Position = 0)]
    public EventAnalysisContractKind[] Kind { get; set; } = Array.Empty<EventAnalysisContractKind>();

    /// <inheritdoc />
    protected override void ProcessRecord() {
        IReadOnlyList<EventAnalysisContractDescriptor> contracts = Kind.Length == 0
            ? EventAnalysisContractCatalog.GetContracts()
            : Kind.Distinct().Select(EventAnalysisContractCatalog.Get).ToArray();
        foreach (EventAnalysisContractDescriptor contract in contracts) {
            WriteObject(contract, enumerateCollection: false);
        }
    }
}
