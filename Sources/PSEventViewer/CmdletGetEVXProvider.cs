using System.Globalization;
using EventViewerX.Providers;

namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Returns registered provider metadata or EventViewerX provider packages.</para>
/// <para type="description">The default set supports local and remote provider discovery. Package sets inspect a portable .evxprovider file or list machine-wide EventViewerX-managed installations.</para>
/// </summary>
/// <example>
///   <summary>Inspect providers and linked channels</summary>
///   <code>Get-EVXProvider -Name '*Security*' | Select-Object Name, LogLinks</code>
///   <para>Returns reusable detached metadata rather than disposable ProviderMetadata handles.</para>
/// </example>
/// <example>
///   <summary>Retain the historical string-only output</summary>
///   <code>Get-EVXProvider -Name '*IIS*' -NameOnly</code>
///   <para>Outputs only provider names for scripts that need strings.</para>
/// </example>
/// <example>
///   <summary>Inspect one portable provider package</summary>
///   <code>Get-EVXProvider -Path .\Contoso.Scanner-1.0.0.evxprovider</code>
///   <para>Verifies the package and returns its typed schema and trust metadata.</para>
/// </example>
/// <example>
///   <summary>List EventViewerX-managed installed packages</summary>
///   <code>Get-EVXProvider -InstalledPackage | Select-Object ProviderName, PackageVersion, IsActive, IsRegistered</code>
///   <para>Uses the package inventory parameter set of the same provider catalog command.</para>
/// </example>
[Cmdlet(VerbsCommon.Get, "EVXProvider", DefaultParameterSetName = "Registered")]
[OutputType(typeof(EventProviderMetadataSnapshot))]
[OutputType(typeof(EventProviderCatalogResult))]
[OutputType(typeof(EventProviderPackage))]
[OutputType(typeof(InstalledEventProviderPackage))]
[OutputType(typeof(string))]
public sealed class CmdletGetEVXProvider : AsyncPSCmdlet {
    /// <summary>Provider names or wildcard patterns.</summary>
    [Parameter(Position = 0, ParameterSetName = "Registered")]
    public string[] Name { get; set; } = new[] { "*" };

    /// <summary>Portable .evxprovider package to verify and inspect.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "Package")]
    [Alias("FullName", "OutputPath", "PackagePath")]
    public string? Path { get; set; }

    /// <summary>Lists providers installed through EventViewerX packages.</summary>
    [Parameter(Mandatory = true, ParameterSetName = "InstalledPackage")]
    public SwitchParameter InstalledPackage { get; set; }

    /// <summary>Remote computer name. Omit for the local computer.</summary>
    [Parameter(ParameterSetName = "Registered")]
    [Alias("ComputerName", "ServerName")]
    public string? MachineName { get; set; }

    /// <summary>Credentials for a remote provider catalog.</summary>
    [Credential]
    [Parameter(ParameterSetName = "Registered")]
    public PSCredential? Credential { get; set; }

    /// <summary>Authentication package for the remote session.</summary>
    [Parameter(ParameterSetName = "Registered")]
    public EventLogAuthentication Authentication { get; set; }

    /// <summary>Maximum time for remote RPC preflight and session establishment.</summary>
    [Parameter(ParameterSetName = "Registered")]
    [ValidateRange(1, int.MaxValue)]
    public int TimeoutMs { get; set; } = 5000;

    /// <summary>Culture used for provider display metadata.</summary>
    [Parameter(ParameterSetName = "Registered")]
    public CultureInfo? Culture { get; set; }

    /// <summary>Includes all provider event definitions and templates.</summary>
    [Parameter(ParameterSetName = "Registered")]
    public SwitchParameter IncludeEvents { get; set; }

    /// <summary>Returns provider names instead of metadata snapshots.</summary>
    [Parameter(ParameterSetName = "Registered")]
    public SwitchParameter NameOnly { get; set; }

    /// <summary>Returns one success/failure result for every matching provider.</summary>
    [Parameter(ParameterSetName = "Registered")]
    public SwitchParameter AsResult { get; set; }

    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        if (ParameterSetName == "Package") {
            try {
                WriteObject(
                    EventProviderPackageReader.Open(
                        SessionState.Path.GetUnresolvedProviderPathFromPSPath(Path!)));
            } catch (Exception exception) {
                WriteError(new ErrorRecord(
                    exception,
                    "EVXProviderPackageReadFailed",
                    ErrorCategory.InvalidData,
                    Path));
            }
            return Task.CompletedTask;
        }
        if (ParameterSetName == "InstalledPackage") {
            WriteObject(
                EventProviderPackageManager.GetInstalled(),
                enumerateCollection: true);
            return Task.CompletedTask;
        }

        if (NameOnly && AsResult) {
            throw new PSArgumentException(
                "NameOnly and AsResult are mutually exclusive.");
        }
        if (NameOnly && IncludeEvents) {
            throw new PSArgumentException(
                "IncludeEvents cannot be combined with NameOnly.");
        }
        var query = new EventLogCatalogQuery {
            MachineName = MachineName,
            Credential = Credential?.GetNetworkCredential(),
            Authentication = Authentication,
            ConnectionTimeoutMilliseconds = TimeoutMs,
            Culture = Culture,
            IncludeEvents = IncludeEvents
        };
        if (NameOnly) {
            foreach (string providerName in
                     EventLogCatalog.GetProviderNames(
                         query,
                         Name,
                         CancelToken)) {
                WriteObject(providerName);
            }
            return Task.CompletedTask;
        }
        foreach (EventProviderCatalogResult result in
                 EventLogCatalog.GetProviders(
                     query,
                     Name,
                     CancelToken)) {
            CancelToken.ThrowIfCancellationRequested();
            if (AsResult) {
                WriteObject(result);
                continue;
            }
            if (!result.Success) {
                WriteError(new ErrorRecord(
                    result.Exception!,
                    "EVXProviderMetadataFailed",
                    ErrorCategory.ReadError,
                    result.ProviderName));
                continue;
            }
            WriteObject(result.Provider);
        }
        return Task.CompletedTask;
    }
}
