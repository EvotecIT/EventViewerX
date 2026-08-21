using System.Management.Automation;
using System.Threading.Tasks;

namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Resets persisted event-query checkpoint progress safely.</para>
/// <para type="description">Starts a new checkpoint generation under the shared file lock so an in-flight query from the previous generation cannot restore stale progress. Use this cmdlet instead of deleting only the RecordIdFile compatibility file because generation state is stored in a visible companion .state.json file.</para>
/// </summary>
/// <example>
///   <summary>Reset every checkpoint in a file</summary>
///   <code>Reset-EVXEventCheckpoint -Path C:\State\security.json</code>
///   <para>Starts a new generation for every checkpoint key stored in the file.</para>
/// </example>
/// <example>
///   <summary>Reset one checkpoint key and inspect the persisted paths</summary>
///   <code>Reset-EVXEventCheckpoint -Path C:\State\security.json -Key security-failures -PassThru</code>
///   <para>Resets only the selected key and returns a snapshot containing CheckpointPath, StatePath, and LockPath.</para>
/// </example>
[Cmdlet(VerbsCommon.Reset, "EVXEventCheckpoint", SupportsShouldProcess = true)]
[OutputType(typeof(EventCheckpointSnapshot))]
public sealed class CmdletResetEVXEventCheckpoint : AsyncPSCmdlet {
    /// <summary>Compatibility checkpoint path supplied to Get-EVXEvent as RecordIdFile.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [Alias("RecordIdFile")]
    [ValidateNotNullOrEmpty]
    public string Path { get; set; } = string.Empty;

    /// <summary>Optional checkpoint key. The exact key and its existing per-source derived keys start new generations. When omitted, every existing key starts a new generation.</summary>
    [Parameter(Position = 1)]
    [Alias("RecordIdKey")]
    [ValidateNotNullOrEmpty]
    public string? Key { get; set; }

    /// <summary>Returns the persisted checkpoint snapshot, including companion state and lock paths.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }

    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        string target = Key == null ? Path : $"{Path} [{Key}]";
        if (!ShouldProcess(target, "Reset event checkpoint generation")) {
            return Task.CompletedTask;
        }

        try {
            EventCheckpointSnapshot snapshot = EventCheckpointStore.Reset(Path, Key);
            if (PassThru) {
                WriteObject(snapshot);
            }
        } catch (Exception ex) {
            WriteError(new ErrorRecord(
                ex,
                "ResetEVXEventCheckpointFailed",
                ex is UnauthorizedAccessException ? ErrorCategory.PermissionDenied : ErrorCategory.WriteError,
                target));
        }
        return Task.CompletedTask;
    }
}
