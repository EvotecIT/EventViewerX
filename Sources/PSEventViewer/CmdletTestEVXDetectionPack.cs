namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Runs the executable fixture contracts shipped with built-in EventViewerX detection packs.</para>
/// <para type="description">Runs positive, negative, exact-boundary, and known-benign scenarios against each rule in isolation.</para>
/// </summary>
/// <example>
///   <summary>Validate all built-in detection content</summary>
///   <code>Test-EVXDetectionPack</code>
///   <para>Every returned result should have IsMatch set to true.</para>
/// </example>
[Cmdlet(VerbsDiagnostic.Test, "EVXDetectionPack")]
[OutputType(typeof(EventDetectionFixtureResult))]
public sealed class CmdletTestEVXDetectionPack : PSCmdlet {
    /// <summary>Optional stable rule identifier wildcard.</summary>
    [Parameter(Position = 0)]
    public string[] RuleId { get; set; } = Array.Empty<string>();

    /// <inheritdoc />
    protected override void ProcessRecord() {
        WildcardPattern[] patterns = RuleId
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => new WildcardPattern(value.Trim(), WildcardOptions.IgnoreCase))
            .ToArray();
        foreach (EventDetectionFixtureResult result in EventDetectionCatalog.TestBuiltInFixtures()) {
            string ruleId = result.Name.Split(' ')[0];
            if (patterns.Length == 0 || patterns.Any(pattern => pattern.IsMatch(ruleId))) {
                WriteObject(result, enumerateCollection: false);
            }
        }
    }
}
