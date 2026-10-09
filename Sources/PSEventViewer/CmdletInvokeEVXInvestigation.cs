namespace PSEventViewer;

/// <summary>Replays a verified investigation without rewriting its original evidence or generated outputs.</summary>
[Cmdlet(VerbsLifecycle.Invoke, "EVXInvestigation", DefaultParameterSetName = "Events")]
[OutputType(typeof(EventDetectionExecutionResult), typeof(EventEndpointAnalysis))]
public sealed class CmdletInvokeEVXInvestigation : AsyncPSCmdlet {
    /// <para>Session returned by Open-EVXInvestigation or Export-EVXInvestigation.</para>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    public EventInvestigationSession Session { get; set; } = null!;
    /// <para>Allows comparative replay with a different engine build and marks resulting coverage incomplete.</para>
    [Parameter]
    public SwitchParameter AllowDifferentEngine { get; set; }
    /// <para>Replays captured endpoint evidence. The default replays native Windows-event detection.</para>
    [Parameter(Mandatory = true, ParameterSetName = "Endpoint")]
    public SwitchParameter Endpoint { get; set; }
    /// <para>New HTML presentation export outside the immutable session directory. Existing files are rejected.</para>
    [Parameter(ParameterSetName = "Endpoint")]
    public string? HtmlPath { get; set; }
    /// <para>Includes raw messages and DSRegCmd fields in the presentation export. The report remains sensitive even when raw evidence is omitted.</para>
    [Parameter(ParameterSetName = "Endpoint")]
    public SwitchParameter IncludeSensitiveEvidence { get; set; }
    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        if (Endpoint) {
            EventEndpointAnalysis analysis = Session.ReplayEndpoint(AllowDifferentEngine, CancelToken);
            if (HtmlPath != null) {
                string destination = SessionState.Path.GetUnresolvedProviderPathFromPSPath(HtmlPath);
                if (destination.StartsWith(Session.DirectoryPath.TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) {
                    throw new PSArgumentException("Write presentation exports outside the immutable investigation directory.");
                }
                using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using var writer = new StreamWriter(output, new UTF8Encoding(false));
                writer.Write(EventEndpointHtmlRenderer.Render(analysis, IncludeSensitiveEvidence));
            }
            WriteObject(analysis, false);
        } else { WriteObject(Session.Replay(AllowDifferentEngine, CancelToken), false); }
        return Task.CompletedTask;
    }
}
