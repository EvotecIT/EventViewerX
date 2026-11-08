namespace EventViewerX.Rules.IIS;

/// <summary>
/// IIS WWW service stopped after an error.
/// 1005: The World Wide Web Publishing Service is stopping because it encountered an error.
/// </summary>
public class IISSiteStopped : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 1005 };
    /// <inheritdoc />
    public override string LogName => "System";
    /// <inheritdoc />
    public override IReadOnlyList<string> ProviderNames => new[] { "Microsoft-Windows-IIS-W3SVC", "W3SVC" };
    /// <inheritdoc />
    public override EventType Type => EventType.IISSiteStopped;

    /// <summary>Accepts IIS WWW service failure events (1005).</summary>
    public override bool CanHandle(EventObject eventObject) {
        return RuleHelpers.IsProvider(eventObject, "Microsoft-Windows-IIS-W3SVC", "W3SVC");
    }

    /// <summary>Machine hosting IIS.</summary>
    public string Computer;
    /// <summary>Service reported by IIS event 1005.</summary>
    public string ServiceName;
    /// <summary>Optional site name, when explicitly supplied by a source.</summary>
    public string SiteName;
    /// <summary>Optional operator, when explicitly supplied by a source.</summary>
    public string User;
    /// <summary>Event timestamp.</summary>
    public DateTime When;

    /// <summary>Initialises an IIS WWW service failure wrapper from an event record.</summary>
    public IISSiteStopped(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "IISSiteStopped";
        Computer = SourceEvent.ComputerName;
        ServiceName = "World Wide Web Publishing Service";
        // The native event does not identify an individual site or operator.
        SiteName = Value("SiteName", "Name");
        User = Value("User", "UserName");
        When = SourceEvent.TimeCreated;
    }
}


