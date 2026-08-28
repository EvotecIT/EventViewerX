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
    /// <summary>One or more Sigma YAML files to validate.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [Alias("FullName")]
    public string[] Path { get; set; } = Array.Empty<string>();

    /// <inheritdoc />
    protected override void ProcessRecord() {
        foreach (string path in Path) {
            SigmaCompilationResult result = SigmaRuleCompiler.Load(GetUnresolvedProviderPathFromPSPath(path));
            WriteObject(result, enumerateCollection: false);
        }
    }
}
