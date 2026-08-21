using System.Globalization;

namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Archives provider resources into exported EVTX files.</para>
/// <para type="description">Makes a Windows-native EVTX export self-contained for message rendering on computers that do not have the source provider installed.</para>
/// </summary>
/// <example>
///   <summary>Archive English provider resources</summary>
///   <code>Update-EVXLogArchive -Path C:\Exports\Security.evtx -Culture en-US</code>
///   <para>Updates the exported log in place through EvtArchiveExportedLog.</para>
/// </example>
[Cmdlet(
    VerbsData.Update,
    "EVXLogArchive",
    SupportsShouldProcess = true)]
[OutputType(typeof(FileInfo))]
public sealed class CmdletUpdateEVXLogArchive : AsyncPSCmdlet {
    /// <summary>Exported EVTX files to update.</summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true)]
    [Alias("FullName")]
    public string[] Path { get; set; } =
        Array.Empty<string>();

    /// <summary>Provider resource culture. Windows chooses a locale when omitted.</summary>
    [Parameter]
    public CultureInfo? Culture { get; set; }

    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        foreach (string path in Path
                     .Select(static value =>
                         value?.Trim() ?? string.Empty)
                     .Where(static value =>
                         value.Length > 0)
                     .Distinct(
                         StringComparer.OrdinalIgnoreCase)) {
            string absolutePath =
                System.IO.Path.GetFullPath(
                    path.Trim('"', '\''));
            if (!ShouldProcess(
                    absolutePath,
                    "Archive provider resources into EVTX")) {
                continue;
            }
            EventLogArchive.ArchiveResources(
                absolutePath,
                Culture,
                CancelToken);
            WriteObject(new FileInfo(absolutePath));
        }
        return Task.CompletedTask;
    }
}
