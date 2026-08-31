namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Imports supported Sigma YAML as native EventViewerX detection rules.</para>
/// <para type="description">The YAML adapter is separate from the detection engine because it adds a YAML dependency. Imported rules execute in the same bounded native engine as built-in rules.</para>
/// </summary>
/// <example>
///   <summary>Run imported Sigma rules over queried events</summary>
///   <code>$rules = Import-EVXSigmaRule -Path .\rules\*.yml; Get-EVXEvent -LogName Security | Invoke-EVXDetection -Rule $rules</code>
///   <para>Compiles each file once and evaluates it with the shared EventViewerX detection engine.</para>
/// </example>
/// <example>
///   <summary>Create a versioned EventViewerX pack</summary>
///   <code>Import-EVXSigmaRule -Path .\rule.yml -AsPack -PackId contoso.windows -Version 1.0.0</code>
///   <para>Wraps supported Sigma rules in an integrity-protected native pack.</para>
/// </example>
[Cmdlet(VerbsData.Import, "EVXSigmaRule", DefaultParameterSetName = "Rule")]
[OutputType(typeof(IEventDetectionRule), ParameterSetName = new[] { "Rule" })]
[OutputType(typeof(EventDetectionPack), ParameterSetName = new[] { "Pack" })]
public sealed class CmdletImportEVXSigmaRule : PSCmdlet {
    private readonly List<IEventDetectionRule> _packRules = new();

    /// <summary>One or more Sigma YAML files to import.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [Alias("FullName")]
    [SupportsWildcards]
    public string[] Path { get; set; } = Array.Empty<string>();

    /// <summary>Returns one versioned EventViewerX pack instead of individual rules.</summary>
    [Parameter(Mandatory = true, ParameterSetName = "Pack")]
    public SwitchParameter AsPack { get; set; }

    /// <summary>Stable pack identifier used with AsPack.</summary>
    [Parameter(Mandatory = true, ParameterSetName = "Pack")]
    public string? PackId { get; set; }

    /// <summary>Semantic pack version used with AsPack.</summary>
    [Parameter(Mandatory = true, ParameterSetName = "Pack")]
    public string? Version { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        var rules = new List<IEventDetectionRule>();
        foreach (string path in ResolvePaths()) {
            SigmaCompilationResult result = SigmaRuleCompiler.Load(path);
            foreach (SigmaDiagnostic diagnostic in result.Diagnostics) {
                if (diagnostic.Severity == SigmaDiagnosticSeverity.Warning) {
                    WriteWarning($"{diagnostic.Code}: {diagnostic.Message}");
                } else if (diagnostic.Severity == SigmaDiagnosticSeverity.Information) {
                    WriteVerbose($"{diagnostic.Code}: {diagnostic.Message}");
                }
            }
            if (!result.IsSupported) {
                string errors = string.Join(" ", result.Diagnostics
                    .Where(static item => item.Severity == SigmaDiagnosticSeverity.Error)
                    .Select(static item => $"{item.Code}: {item.Message}"));
                throw new PSArgumentException($"Sigma import failed for '{path}'. {errors}", nameof(Path));
            }
            rules.AddRange(result.Rules);
        }

        if (AsPack) {
            _packRules.AddRange(rules);
        } else {
            foreach (IEventDetectionRule rule in rules) {
                WriteObject(rule, enumerateCollection: false);
            }
        }
    }

    private IReadOnlyList<string> ResolvePaths() {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string pattern in Path) {
            try {
                foreach (string path in SessionState.Path.GetResolvedProviderPathFromPSPath(
                             pattern,
                             out ProviderInfo provider)) {
                    if (!string.Equals(provider.Name, "FileSystem", StringComparison.OrdinalIgnoreCase)) {
                        throw new PSArgumentException(
                            $"Sigma path '{pattern}' must use the FileSystem provider.",
                            nameof(Path));
                    }
                    paths.Add(System.IO.Path.GetFullPath(path));
                }
            } catch (ItemNotFoundException) {
                throw new PSArgumentException(
                    $"No Sigma rule files match path '{pattern}'.",
                    nameof(Path));
            }
        }
        if (paths.Count == 0) {
            throw new PSArgumentException("At least one Sigma rule file is required.", nameof(Path));
        }
        return paths.OrderBy(static path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <inheritdoc />
    protected override void EndProcessing() {
        if (!AsPack) {
            return;
        }
        EventDetectionPack pack = EventDetectionPack.Create(
            PackId!,
            Version!,
            _packRules.Select(static rule => rule.Definition));
        WriteObject(pack, enumerateCollection: false);
    }
}
