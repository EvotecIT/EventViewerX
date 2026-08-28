namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Gets the built-in versioned EventViewerX detection packs.</para>
/// <para type="description">Returns signed-content-ready pack manifests with rule provenance, versions, hashes, licenses, and ATT&amp;CK tags.</para>
/// </summary>
/// <example>
///   <summary>List every built-in detection pack</summary>
///   <code>Get-EVXDetectionPack</code>
///   <para>Returns the native EventViewerX detection packs without loading Storage or Reporting.</para>
/// </example>
/// <example>
///   <summary>Select authentication packs</summary>
///   <code>Get-EVXDetectionPack -PackId '*authentication*'</code>
///   <para>Uses PowerShell wildcard matching against stable pack identifiers.</para>
/// </example>
[Cmdlet(VerbsCommon.Get, "EVXDetectionPack")]
[OutputType(typeof(EventDetectionPack))]
public sealed class CmdletGetEVXDetectionPack : PSCmdlet {
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
                WriteObject(pack, enumerateCollection: false);
            }
        }
    }
}
