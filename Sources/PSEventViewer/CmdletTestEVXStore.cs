namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Validates an EventViewerX history store.</para>
/// <para type="description">Runs SQLite integrity checks and verifies the supported base, identity, and finding schema contracts.</para>
/// </summary>
/// <example>
///   <summary>Check a store before backup or analysis</summary>
///   <code>Test-EVXStore -Path C:\Data\events.db</code>
///   <para>Returns health, schema versions, row counts, database size, and any diagnostics.</para>
/// </example>
[Cmdlet(VerbsDiagnostic.Test, "EVXStore")]
[OutputType(typeof(EventStoreIntegrityResult))]
public sealed class CmdletTestEVXStore : AsyncPSCmdlet {
    /// <summary>EventStore SQLite path.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    public string Path { get; set; } = string.Empty;

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        string resolved = SessionState.Path.GetUnresolvedProviderPathFromPSPath(Path);
        EventStoreIntegrityResult result = await new EventStore(resolved)
            .CheckIntegrityAsync(CancelToken).ConfigureAwait(false);
        WriteObject(result, enumerateCollection: false);
    }
}
