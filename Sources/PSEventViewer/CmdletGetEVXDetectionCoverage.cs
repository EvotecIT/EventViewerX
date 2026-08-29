namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Gets evidence and readiness requirements for built-in EventViewerX detection packs.</para>
/// <para type="description">Returns channels, providers, event IDs, typed projections, audit policies, target roles, and configuration prerequisites.</para>
/// </summary>
/// <example>
///   <summary>Inspect authentication evidence prerequisites</summary>
///   <code>Get-EVXDetectionCoverage -PackId '*authentication*'</code>
///   <para>Use the result before interpreting an empty detection run as a clean environment.</para>
/// </example>
[Cmdlet(VerbsCommon.Get, "EVXDetectionCoverage")]
[OutputType(typeof(EventDetectionPackCoverage))]
public sealed class CmdletGetEVXDetectionCoverage : PSCmdlet {
    /// <summary>Optional stable pack identifier wildcard.</summary>
    [Parameter(Position = 0)]
    public string[] PackId { get; set; } = Array.Empty<string>();

    /// <inheritdoc />
    protected override void ProcessRecord() {
        WildcardPattern[] patterns = PackId
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => new WildcardPattern(value.Trim(), WildcardOptions.IgnoreCase))
            .ToArray();
        foreach (EventDetectionPack pack in EventDetectionCatalog.GetBuiltInPacks()) {
            if (patterns.Length == 0 || patterns.Any(pattern => pattern.IsMatch(pack.PackId))) {
                WriteObject(pack.GetCoverage(), enumerateCollection: false);
            }
        }
    }
}
