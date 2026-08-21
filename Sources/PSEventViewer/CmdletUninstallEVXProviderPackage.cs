using EventViewerX.Providers;

namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Unregisters an EventViewerX-managed custom event provider.</para>
/// <para type="description">Removes the active manifest registration. Package and schema files are retained by default so archived EVTX records remain renderable and the provider can be restored; use RemoveFiles only when that history is no longer required.</para>
/// </summary>
[Cmdlet(
    VerbsLifecycle.Uninstall,
    "EVXProviderPackage",
    SupportsShouldProcess = true,
    ConfirmImpact = ConfirmImpact.High)]
[OutputType(typeof(EventProviderPackageUninstallResult))]
public sealed class CmdletUninstallEVXProviderPackage : PSCmdlet {
    /// <summary>
    /// <para>Name of an EventViewerX-managed provider.</para>
    /// </summary>
    [Parameter(
        Mandatory = true,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        Position = 0)]
    [Alias("Name")]
    [ValidateNotNullOrEmpty]
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// <para>Delete retained packages and schemas after unregistering. Old EVTX messages may no longer render on this machine.</para>
    /// </summary>
    [Parameter]
    public SwitchParameter RemoveFiles { get; set; }

    /// <summary>Unregisters the provider and optionally removes retained files.</summary>
    protected override void ProcessRecord() {
        if (!ShouldProcess(
                ProviderName,
                RemoveFiles.IsPresent
                    ? "Unregister provider and remove retained schema files"
                    : "Unregister provider and retain schema files")) {
            return;
        }
        try {
            WriteObject(
                EventProviderPackageManager.Uninstall(
                    ProviderName,
                    RemoveFiles.IsPresent));
        } catch (Exception exception) {
            WriteError(new ErrorRecord(
                exception,
                "EVXProviderPackageUninstallFailed",
                exception is UnauthorizedAccessException
                    ? ErrorCategory.PermissionDenied
                    : ErrorCategory.InvalidOperation,
                ProviderName));
        }
    }
}
