namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Validates and compiles Sigma YAML against the EventViewerX supported subset.</para>
/// <para type="description">Returns structured diagnostics and native rules without executing them. Unsupported behavior is reported explicitly and is never weakened silently.</para>
/// </summary>
/// <example>
///   <summary>Validate a Sigma rule file</summary>
///   <code>Test-EVXSigmaRule -Path .\rules\suspicious-logon.yml</code>
///   <para>Returns the compiled rules, diagnostics, and IsSupported status.</para>
/// </example>
[Cmdlet(VerbsDiagnostic.Test, "EVXSigmaRule")]
[OutputType(typeof(SigmaCompilationResult))]
public sealed class CmdletTestEVXSigmaRule : PSCmdlet {
    private readonly List<string> _resolvedPaths = new();
    private readonly HashSet<string> _resolvedPathIdentities = new(FileSystemPathIdentity.Comparer);

    /// <summary>One or more Sigma YAML files to validate.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [Alias("FullName")]
    [SupportsWildcards]
    public string[] Path { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Explicit telemetry assumptions used for category-only Sigma log sources.
    /// Strict is lossless and rejects categories without exact native selectors.
    /// </summary>
    [Parameter]
    [ValidateSet("Strict", "WindowsSysmonAndPowerShell")]
    public string TelemetryProfile { get; set; } = "Strict";

    /// <inheritdoc />
    protected override void ProcessRecord() {
        foreach (string path in SigmaPathResolver.Resolve(SessionState, Path, nameof(Path))) {
            if (_resolvedPathIdentities.Add(path)) {
                _resolvedPaths.Add(path);
            }
        }
    }

    /// <inheritdoc />
    protected override void EndProcessing() {
        SigmaCompilationResult result = SigmaRuleCompiler.Load(
            _resolvedPaths,
            ResolveCompilationOptions());
        WriteObject(result, enumerateCollection: false);
    }

    private SigmaCompilationOptions? ResolveCompilationOptions() =>
        string.Equals(
            TelemetryProfile,
            "WindowsSysmonAndPowerShell",
            StringComparison.OrdinalIgnoreCase)
            ? new SigmaCompilationOptions {
                LogSourceProfile = SigmaLogSourceProfile.WindowsSysmonAndPowerShell
            }
            : null;
}
