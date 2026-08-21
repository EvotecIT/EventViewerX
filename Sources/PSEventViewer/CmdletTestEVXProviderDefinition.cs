using EventViewerX.Providers;

namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Validates a custom Windows event provider definition.</para>
/// <para type="description">Checks provider identity, channels, event versions, field references, maps, localization, Windows limits, and schema compatibility before any native build tools or machine registration are used.</para>
/// </summary>
[Cmdlet(VerbsDiagnostic.Test, "EVXProviderDefinition")]
[OutputType(typeof(EventProviderValidationResult))]
public sealed class CmdletTestEVXProviderDefinition : PSCmdlet {
    /// <summary>
    /// <para>Definition object or friendly PowerShell hashtable.</para>
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
        ParameterSetName = "Path")]
    [ValidateNotNullOrEmpty]
    public string Path { get; set; } = string.Empty;

    /// <summary>Validates and emits the complete issue collection.</summary>
    protected override void ProcessRecord() {
        try {
            EventProviderDefinition definition =
                ParameterSetName == "Path"
                    ? EventProviderDefinitionJson.Load(
                        SessionState.Path.GetUnresolvedProviderPathFromPSPath(
                            Path))
                    : PowerShellEventProviderDefinitionAdapter.Convert(
                        Definition);
            WriteObject(
                EventProviderDefinitionValidator.Validate(
                    definition));
        } catch (EventProviderValidationException exception) {
            WriteObject(exception.Result);
        } catch (Exception exception) {
            WriteError(new ErrorRecord(
                exception,
                "EVXProviderDefinitionTestFailed",
                ErrorCategory.InvalidData,
                ParameterSetName == "Path"
                    ? Path
                    : Definition));
        }
    }
}
