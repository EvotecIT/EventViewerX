namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Applies explicit EventViewerX event and finding retention.</para>
/// <para type="description">Prunes source events and durable findings independently and can compact free SQLite pages after deletion.</para>
/// </summary>
/// <example>
///   <summary>Retain 30 days of events and 90 days of findings</summary>
///   <code>Invoke-EVXStoreRetention -Path C:\Data\events.db -EventRetention 30.00:00:00 -FindingRetention 90.00:00:00 -Vacuum</code>
///   <para>Returns deleted row counts and database sizes before and after maintenance.</para>
/// </example>
[Cmdlet(VerbsLifecycle.Invoke, "EVXStoreRetention", SupportsShouldProcess = true)]
[OutputType(typeof(EventStoreRetentionResult))]
public sealed class CmdletInvokeEVXStoreRetention : AsyncPSCmdlet {
    /// <summary>EventStore SQLite path.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    public string Path { get; set; } = string.Empty;

    /// <summary>Maximum source-event age.</summary>
    [Parameter]
    public TimeSpan? EventRetention { get; set; }

    /// <summary>Maximum durable-finding age.</summary>
    [Parameter]
    public TimeSpan? FindingRetention { get; set; }

    /// <summary>Compacts free SQLite pages after rows are removed.</summary>
    [Parameter]
    public SwitchParameter Vacuum { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        string resolved = SessionState.Path.GetUnresolvedProviderPathFromPSPath(Path);
        if (!ShouldProcess(resolved, "Apply EventStore retention policy")) {
            return;
        }
        EventStoreRetentionResult result = await new EventStore(resolved)
            .ApplyRetentionAsync(
                new EventStoreRetentionPolicy {
                    EventRetention = EventRetention,
                    FindingRetention = FindingRetention,
                    VacuumAfterPrune = Vacuum
                },
                cancellationToken: CancelToken).ConfigureAwait(false);
        WriteObject(result, enumerateCollection: false);
    }
}
