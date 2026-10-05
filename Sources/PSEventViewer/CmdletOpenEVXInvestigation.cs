namespace PSEventViewer;

/// <summary>Opens an investigation after verifying every retained input and output artifact.</summary>
[Cmdlet(VerbsCommon.Open, "EVXInvestigation")]
[OutputType(typeof(EventInvestigationSession))]
public sealed class CmdletOpenEVXInvestigation : AsyncPSCmdlet {
    /// <para>Directory containing manifest.json and its retained evidence.</para>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    public string Path { get; set; } = string.Empty;
    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        WriteObject(EventInvestigationSession.Open(SessionState.Path.GetUnresolvedProviderPathFromPSPath(Path), CancelToken), false);
        return Task.CompletedTask;
    }
}
