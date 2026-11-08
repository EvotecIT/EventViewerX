namespace EventViewerX.Rules.AADConnect;

/// <summary>
/// Azure AD Connect run profile completed
/// Event 906 is general error logging, not a run-profile completion contract.
/// </summary>
public class AADConnectRunProfile : EventRuleBase {
    /// <summary>Historical identifiers retained for compatibility; this built-in source is unavailable.</summary>
    public override List<int> EventIds => new() { 906 };

    /// <summary>Historical channel retained for compatibility; see the catalog definition's UnavailableReason.</summary>
    public override string LogName => "Application";

    /// <inheritdoc />
    public override IReadOnlyList<string> ProviderNames => new[] { "Directory Synchronization" };

    /// <inheritdoc />
    public override EventType Type => EventType.AADConnectRunProfile;

    /// <summary>Rejects the historical source because it does not establish the advertised event.</summary>
    public override bool CanHandle(EventObject eventObject) {
        return false;
    }

    /// <summary>Name of the connector that executed.</summary>
    public string Connector;

    /// <summary>Run profile that was executed.</summary>
    public string RunProfile;

    /// <summary>Time when the run profile finished.</summary>
    public DateTime When;

    /// <summary>Initialises a run profile completion wrapper from an event record.</summary>
    public AADConnectRunProfile(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "AADConnectRunProfile";
        Connector = SourceEvent.GetValueFromDataDictionary("Connector", "Connector Name");
        RunProfile = SourceEvent.GetValueFromDataDictionary("RunProfileName", "Run Profile Name", "", false);
        When = SourceEvent.TimeCreated;
    }
}
