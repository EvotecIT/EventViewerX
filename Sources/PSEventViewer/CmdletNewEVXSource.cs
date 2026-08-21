namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Registers a classic Windows Event Log source explicitly.</para>
/// <para type="description">Creates only the requested source registration and supports provider message, parameter, and category resource files. The command reports whether it created anything.</para>
/// </summary>
/// <example>
///   <summary>Register a source for an application log</summary>
///   <code>New-EVXSource -SourceName MyApp -LogName Application</code>
///   <para>Registers MyApp explicitly so later Write-EVXEvent calls do not need administrative configuration behavior.</para>
/// </example>
[Cmdlet(VerbsCommon.New, "EVXSource", SupportsShouldProcess = true)]
[OutputType(typeof(bool))]
public sealed class CmdletNewEVXSource : AsyncPSCmdlet {
    /// <summary>Source name to register.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [Alias("Source", "Provider", "ProviderName")]
    public string SourceName { get; set; } = null!;

    /// <summary>Classic log that owns the source.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string LogName { get; set; } = null!;

    /// <summary>Optional remote target.</summary>
    [Parameter]
    [Alias("ComputerName", "ServerName")]
    public string? MachineName { get; set; }

    /// <summary>Optional provider message resource DLL.</summary>
    [Parameter]
    public string? MessageResourceFile { get; set; }

    /// <summary>Optional provider parameter resource DLL.</summary>
    [Parameter]
    public string? ParameterResourceFile { get; set; }

    /// <summary>Optional provider category resource DLL.</summary>
    [Parameter]
    public string? CategoryResourceFile { get; set; }

    /// <summary>Number of categories in CategoryResourceFile.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int CategoryCount { get; set; }

    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        string target = string.IsNullOrWhiteSpace(MachineName)
            ? $"{LogName}/{SourceName}"
            : $"{MachineName}/{LogName}/{SourceName}";
        if (!ShouldProcess(target, "Register event source")) {
            return Task.CompletedTask;
        }
        bool created = ClassicEventLogManager.EnsureSource(
            new ClassicEventSourceConfiguration {
                SourceName = SourceName,
                LogName = LogName,
                MachineName = MachineName,
                MessageResourceFile = MessageResourceFile,
                ParameterResourceFile = ParameterResourceFile,
                CategoryResourceFile = CategoryResourceFile,
                CategoryCount = CategoryCount
            });
        WriteObject(created);
        return Task.CompletedTask;
    }
}
