namespace PSEventViewer;

/// <summary>
/// <para type="synopsis">Summarizes observed KDCsvc RC4 enforcement impact by domain controller, account, and service.</para>
/// <para type="description">Distinguishes audit warnings, requests already blocked, and explicit insecure defaults. Event evidence does not certify complete domain coverage.</para>
/// </summary>
/// <example>
///   <summary>Assess stored KDC observations</summary>
///   <code>Get-EVXKerberosImpact -FromStore C:\Data\events.db -StartTime (Get-Date).AddDays(-7)</code>
/// </example>
[Cmdlet(VerbsCommon.Get, "EVXKerberosImpact", DefaultParameterSetName = "Store")]
[OutputType(typeof(KerberosRc4ImpactReport))]
public sealed class CmdletGetEVXKerberosImpact : AsyncPSCmdlet {
    /// <summary>EventStore SQLite database path.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Store")]
    public string? FromStore { get; set; }

    /// <summary>Existing report of typed KDCsvc events.</summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "Report")]
    public EventReport? Report { get; set; }

    /// <summary>Inclusive lower event-time boundary for stored history.</summary>
    [Parameter(ParameterSetName = "Store")]
    public DateTime? StartTime { get; set; }

    /// <summary>Inclusive upper event-time boundary for stored history.</summary>
    [Parameter(ParameterSetName = "Store")]
    public DateTime? EndTime { get; set; }

    /// <summary>Maximum stored rows examined; zero selects every match.</summary>
    [Parameter(ParameterSetName = "Store")]
    public long MaxEvents { get; set; }

    /// <summary>Maximum distinct impact groups retained.</summary>
    [Parameter]
    public int MaximumGroups { get; set; } = 10_000;

    /// <summary>Maximum evidence identities retained per impact group.</summary>
    [Parameter]
    public int MaximumEvidencePerGroup { get; set; } = 25;

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        if (ParameterSetName == "Report") {
            WriteObject(KerberosRc4ImpactEngine.Analyze(Report!, MaximumGroups, MaximumEvidencePerGroup), enumerateCollection: false);
            return;
        }
        string resolved = SessionState.Path.GetUnresolvedProviderPathFromPSPath(FromStore!);
        var query = new EventStoreQuery {
            Types = new[] { EventType.KerberosKdcRc4Audit },
            StartTime = StartTime,
            EndTime = EndTime,
            MaxEvents = MaxEvents
        };
        KerberosRc4ImpactAccumulator accumulator = KerberosRc4ImpactEngine.CreateAccumulator(
            MaximumGroups, MaximumEvidencePerGroup);
        EventStoreRowReadResult read = await new EventStore(resolved)
            .StreamRowsAsync(query, (row, _) => {
                accumulator.Add(row);
                return Task.CompletedTask;
            }, CancelToken).ConfigureAwait(false);
        WriteObject(accumulator.Complete(read.IsComplete, read.CompletenessDiagnostic), enumerateCollection: false);
    }
}
