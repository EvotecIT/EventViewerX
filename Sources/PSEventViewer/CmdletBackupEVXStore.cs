namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Creates a consistent validated EventViewerX store backup.</para>
/// <para type="description">Uses SQLite snapshot semantics, validates the generated database, and returns its size and SHA-256 checksum.</para>
/// </summary>
/// <example>
///   <summary>Create a portable backup</summary>
///   <code>Backup-EVXStore -Path C:\Data\events.db -Destination C:\Backups\events.db</code>
///   <para>Fails if the destination exists unless Force is supplied.</para>
/// </example>
[Cmdlet(VerbsData.Backup, "EVXStore")]
[OutputType(typeof(EventStoreBackupResult))]
public sealed class CmdletBackupEVXStore : AsyncPSCmdlet {
    /// <summary>Live EventStore SQLite path.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    public string Path { get; set; } = string.Empty;

    /// <summary>Destination backup database path.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string Destination { get; set; } = string.Empty;

    /// <summary>Atomically replaces an existing destination backup.</summary>
    [Parameter]
    public SwitchParameter Force { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        string source = SessionState.Path.GetUnresolvedProviderPathFromPSPath(Path);
        string destination = SessionState.Path.GetUnresolvedProviderPathFromPSPath(Destination);
        EventStoreBackupResult result = await new EventStore(source)
            .BackupAsync(destination, Force, CancelToken).ConfigureAwait(false);
        WriteObject(result, enumerateCollection: false);
    }
}
