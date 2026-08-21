using EventViewerX.Providers;
using System.Security.Cryptography.X509Certificates;

namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Compiles a portable custom Windows event provider package.</para>
/// <para type="description">Validates the schema, optionally compares a compatibility baseline, compiles the Windows event metadata and localized messages in-process, hashes every file, optionally signs package identity and hashes, and emits one portable .evxprovider file.</para>
/// <para type="description">No Windows SDK, Visual Studio, native compiler, generated source, or external build tool is required.</para>
/// </summary>
[Cmdlet(
    VerbsCommon.New,
    "EVXProviderPackage",
    SupportsShouldProcess = true)]
[OutputType(typeof(EventProviderPackageBuildResult))]
public sealed class CmdletNewEVXProviderPackage : PSCmdlet {
    /// <summary>
    /// <para>Typed definition or friendly PowerShell hashtable.</para>
    /// </summary>
    [Parameter(
        Mandatory = true,
        ValueFromPipeline = true,
        Position = 0,
        ParameterSetName = "Definition")]
    [ValidateNotNull]
    public object Definition { get; set; } = null!;

    /// <summary>
    /// <para>UTF-8 provider definition JSON file.</para>
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ParameterSetName = "DefinitionPath")]
    [ValidateNotNullOrEmpty]
    public string DefinitionPath { get; set; } = string.Empty;

    /// <summary>
    /// <para>Destination .evxprovider package path.</para>
    /// </summary>
    [Parameter(Mandatory = true, Position = 1)]
    [ValidateNotNullOrEmpty]
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    /// <para>Earlier .evxprovider package or definition JSON used to prevent breaking schema changes.</para>
    /// </summary>
    [Parameter]
    public string BaselinePath { get; set; } = string.Empty;

    /// <summary>
    /// <para>RSA certificate with a private key used to sign package identity and file hashes.</para>
    /// </summary>
    [Parameter]
    public X509Certificate2? SigningCertificate { get; set; }

    /// <summary>
    /// <para>Thumbprint resolved from CurrentUser\My or LocalMachine\My for package signing.</para>
    /// </summary>
    [Parameter]
    public string CertificateThumbprint { get; set; } = string.Empty;

    /// <summary>
    /// <para>Replace an existing output package.</para>
    /// </summary>
    [Parameter]
    public SwitchParameter Force { get; set; }

    /// <summary>Builds the verified portable package.</summary>
    protected override void ProcessRecord() {
        string outputPath =
            SessionState.Path.GetUnresolvedProviderPathFromPSPath(
                OutputPath);
        if (!ShouldProcess(
                outputPath,
                "Compile custom event provider package")) {
            return;
        }
        try {
            EventProviderDefinition definition =
                ParameterSetName == "DefinitionPath"
                    ? EventProviderDefinitionJson.Load(
                        SessionState.Path.GetUnresolvedProviderPathFromPSPath(
                            DefinitionPath))
                    : PowerShellEventProviderDefinitionAdapter.Convert(
                        Definition);
            X509Certificate2? certificate =
                PowerShellCertificateResolver.Resolve(
                    SigningCertificate,
                    CertificateThumbprint);
            WriteObject(
                EventProviderPackageBuilder.Build(
                    definition,
                    outputPath,
                    new EventProviderPackageBuildOptions {
                        BaselinePath =
                            string.IsNullOrWhiteSpace(BaselinePath)
                                ? string.Empty
                                : SessionState.Path
                                    .GetUnresolvedProviderPathFromPSPath(
                                        BaselinePath),
                        Overwrite = Force.IsPresent,
                        SigningCertificate = certificate
                    }));
        } catch (Exception exception) {
            WriteError(new ErrorRecord(
                exception,
                "EVXProviderPackageBuildFailed",
                ErrorCategory.InvalidData,
                outputPath));
        }
    }
}
