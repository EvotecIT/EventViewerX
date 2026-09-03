namespace EventViewerX.Rules.AADConnect;

/// <summary>
/// Azure AD Connect run profile completed
/// 906: The run profile finished executing
/// </summary>
public class AADConnectRunProfile : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 906 };

    /// <inheritdoc />
    public override string LogName => "Application";

    /// <inheritdoc />
    public override IReadOnlyList<string> ProviderNames => new[] { "Directory Synchronization" };

    /// <inheritdoc />
    public override EventType Type => EventType.AADConnectRunProfile;

    /// <summary>Accepts matching Azure AD Connect run profile completion events.</summary>
    public override bool CanHandle(EventObject eventObject) {
        // Simple rule - always handle if event ID and log name match
        return true;
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
