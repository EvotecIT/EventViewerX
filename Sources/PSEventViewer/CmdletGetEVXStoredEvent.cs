namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Streams filtered rows from an EventViewerX history store.</para>
/// <para type="description">Emits each normalized row as it is read, keeping memory bounded for large histories. MaxEvents and MaxCandidates report when a result is incomplete.</para>
/// </summary>
/// <example>
///   <summary>Read recent stored authentication events</summary>
///   <code>Get-EVXStoredEvent -Path C:\Data\events.db -Type ActiveDirectoryAuthentication -StartTime (Get-Date).AddDays(-1) -MaxEvents 1000</code>
/// </example>
[Cmdlet(VerbsCommon.Get, "EVXStoredEvent")]
[OutputType(typeof(EventReportRow))]
public sealed class CmdletGetEVXStoredEvent : AsyncPSCmdlet {
    /// <summary>EventStore SQLite database path.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    public string Path { get; set; } = string.Empty;

    /// <summary>Built-in event definitions to select.</summary>
    [Parameter]
    public EventType[]? Type { get; set; }

    /// <summary>Stable built-in or custom definition names.</summary>
    [Parameter]
    public string[]? DefinitionName { get; set; }

    /// <summary>Inclusive lower event-time boundary.</summary>
    [Parameter]
    public DateTime? StartTime { get; set; }

    /// <summary>Inclusive upper event-time boundary.</summary>
    [Parameter]
    public DateTime? EndTime { get; set; }

    /// <summary>Exact event identifiers.</summary>
    [Parameter]
    public int[]? EventId { get; set; }

    /// <summary>Original source channels.</summary>
    [Parameter]
    public string[]? LogName { get; set; }

    /// <summary>Exact typed or provider payload predicate.</summary>
    [Parameter]
    public EventPredicate? Where { get; set; }

    /// <summary>Maximum rows emitted; zero streams every match.</summary>
    [Parameter]
    public long MaxEvents { get; set; }

    /// <summary>Maximum candidate rows evaluated when managed filtering is needed.</summary>
    [Parameter]
    public long MaxCandidates { get; set; } = 100_000;

    /// <summary>Emit oldest rows first.</summary>
    [Parameter]
    public SwitchParameter Oldest { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        string resolved = SessionState.Path.GetUnresolvedProviderPathFromPSPath(Path);
        var query = new EventStoreQuery {
            Types = Type,
            DefinitionNames = DefinitionName,
            StartTime = StartTime,
            EndTime = EndTime,
            EventIds = EventId,
            SourceLogs = LogName,
            Predicate = Where,
            MaxEvents = MaxEvents,
            MaxCandidates = MaxCandidates,
            Oldest = Oldest.IsPresent
        };
        EventStoreRowReadResult result = await new EventStore(resolved)
            .StreamRowsAsync(query, (row, _) => {
                WriteObject(row, enumerateCollection: false);
                return Task.CompletedTask;
            }, CancelToken).ConfigureAwait(false);
        if (!result.IsComplete) {
            WriteWarning(result.CompletenessDiagnostic ?? "The stored history read was incomplete.");
        }
    }
}
