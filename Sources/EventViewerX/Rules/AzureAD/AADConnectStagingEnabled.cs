namespace EventViewerX.Rules.AzureAD;

/// <summary>
/// Azure AD Connect staging mode enabled
/// 350: Staging mode has been enabled
/// </summary>
public class AADConnectStagingEnabled : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 350 };

    /// <inheritdoc />
    public override string LogName => "Application";

    /// <inheritdoc />
    public override IReadOnlyList<string> ProviderNames => new[] { "Directory Synchronization" };

    /// <inheritdoc />
    public override EventType Type => EventType.AADConnectStagingEnabled;

    /// <summary>Accepts matching staging mode enablement events.</summary>
    public override bool CanHandle(EventObject eventObject) {
        // Simple rule - always handle if event ID and log name match
        return true;
    }

    /// <summary>Server where staging mode was enabled.</summary>
    public string Computer;

    /// <summary>Operator account that enabled staging mode.</summary>
    public string Operator;

    /// <summary>Time when staging was enabled.</summary>
    public DateTime When;

    /// <summary>Initialises a staging-enabled wrapper from an event record.</summary>
    public AADConnectStagingEnabled(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "AADConnectStagingEnabled";
        Computer = SourceEvent.ComputerName;
        Operator = SourceEvent.GetValueFromDataDictionary("Operator");
        if (string.IsNullOrEmpty(Operator)) {
            Operator = SourceEvent.GetValueFromDataDictionary(
                "SubjectUserName",
                "SubjectDomainName",
                "\\",
                true);
        }
        When = SourceEvent.TimeCreated;
    }
}
