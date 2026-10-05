namespace PSEventViewer;

/// <summary>Replays a verified investigation without rewriting its original evidence or generated outputs.</summary>
[Cmdlet(VerbsLifecycle.Invoke, "EVXInvestigation")]
[OutputType(typeof(EventDetectionExecutionResult))]
public sealed class CmdletInvokeEVXInvestigation : AsyncPSCmdlet {
    /// <para>Session returned by Open-EVXInvestigation or Export-EVXInvestigation.</para>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    public EventInvestigationSession Session { get; set; } = null!;
    /// <para>Allows comparative replay with a different engine build and marks resulting coverage incomplete.</para>
    [Parameter]
    public SwitchParameter AllowDifferentEngine { get; set; }
    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        WriteObject(Session.Replay(AllowDifferentEngine, CancelToken), false);
        return Task.CompletedTask;
    }
}
