using EventViewerX.Providers;

namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Installs or upgrades a portable custom Windows event provider package.</para>
/// <para type="description">Verifies package hashes and signatures before changing machine state, enforces schema and version compatibility, stages resources under ProgramData, registers the manifest, verifies Windows metadata and channels, and rolls back to the previous provider if activation fails.</para>
/// <para type="description">The target machine does not require the Windows SDK, Visual Studio, a C# compiler, generated source, or package build tools.</para>
/// </summary>
[Cmdlet(
    VerbsLifecycle.Install,
    "EVXProviderPackage",
    SupportsShouldProcess = true,
    ConfirmImpact = ConfirmImpact.High)]
[OutputType(typeof(EventProviderPackageInstallResult))]
public sealed class CmdletInstallEVXProviderPackage : PSCmdlet {
    /// <summary>
    /// <para>Portable .evxprovider package path.</para>
    /// </summary>
    [Parameter(
        Mandatory = true,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        Position = 0)]
    [Alias("FullName", "OutputPath", "PackagePath")]
    [ValidateNotNullOrEmpty]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// <para>Package trust policy. RequireTrustedSignature requires an exact configured signer thumbprint when pins are supplied; otherwise it requires a Windows-trusted certificate with the Code Signing EKU.</para>
    /// </summary>
    [Parameter]
    public EventProviderPackageTrustMode TrustMode { get; set; } =
        EventProviderPackageTrustMode.AllowUnsigned;

    /// <summary>
    /// <para>Optional exact signer-thumbprint allowlist for RequireTrustedSignature. When supplied, certificates that do not match a pin are rejected even when Windows trusts their chain.</para>
    /// </summary>
    [Parameter]
    public string[] TrustedSignerThumbprint { get; set; } =
        Array.Empty<string>();

    /// <summary>
    /// <para>Allow a compatible lower package version to replace the active version.</para>
    /// </summary>
    [Parameter]
    public SwitchParameter AllowDowngrade { get; set; }

    /// <summary>
    /// <para>Allow different package bytes to reuse the active version. Prefer publishing a new immutable version.</para>
    /// </summary>
    [Parameter]
    public SwitchParameter AllowSameVersionReplacement { get; set; }

    /// <summary>Installs the package transactionally.</summary>
    protected override void ProcessRecord() {
        string path =
            SessionState.Path.GetUnresolvedProviderPathFromPSPath(
                Path);
        if (!ShouldProcess(
                path,
                "Install or upgrade custom event provider")) {
            return;
        }
        try {
            WriteObject(
                EventProviderPackageManager.Install(
                    path,
                    new EventProviderPackageInstallOptions {
                        TrustMode = TrustMode,
                        TrustedSignerThumbprints =
                            TrustedSignerThumbprint,
                        AllowDowngrade =
                            AllowDowngrade.IsPresent,
                        AllowSameVersionReplacement =
                            AllowSameVersionReplacement.IsPresent
                    }));
        } catch (Exception exception) {
            WriteError(new ErrorRecord(
                exception,
                "EVXProviderPackageInstallFailed",
                exception is UnauthorizedAccessException
                    ? ErrorCategory.PermissionDenied
                    : ErrorCategory.InvalidOperation,
                path));
        }
    }
}
