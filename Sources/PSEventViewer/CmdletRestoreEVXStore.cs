namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Restores an EventViewerX store from a validated backup.</para>
/// <para type="description">Validates the backup before replacement, uses an atomic recovery file, and restores the original database automatically when post-replacement validation fails. Stop active readers and writers first.</para>
/// </summary>
/// <example>
///   <summary>Restore a known backup</summary>
///   <code>Restore-EVXStore -Path C:\Data\events.db -BackupPath C:\Backups\events.db</code>
///   <para>Prompts because the live history database is replaced.</para>
/// </example>
[Cmdlet(VerbsData.Restore, "EVXStore", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
[OutputType(typeof(EventStoreIntegrityResult))]
public sealed class CmdletRestoreEVXStore : AsyncPSCmdlet {
    /// <summary>Live EventStore SQLite path to replace.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    public string Path { get; set; } = string.Empty;

    /// <summary>Validated backup database path.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string BackupPath { get; set; } = string.Empty;

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        string target = SessionState.Path.GetUnresolvedProviderPathFromPSPath(Path);
        string backup = SessionState.Path.GetUnresolvedProviderPathFromPSPath(BackupPath);
        if (!ShouldProcess(target, $"Replace EventStore from '{backup}'")) {
            return;
        }
        EventStoreIntegrityResult result = await new EventStore(target)
            .RestoreAsync(backup, CancelToken).ConfigureAwait(false);
        WriteObject(result, enumerateCollection: false);
    }
}
