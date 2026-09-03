namespace EventViewerX.Rules.AzureAD;

/// <summary>
/// Azure AD Connect staging mode disabled
/// 351: Staging mode has been disabled
/// </summary>
public class AADConnectStagingDisabled : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 351 };

    /// <inheritdoc />
    public override string LogName => "Application";

    /// <inheritdoc />
    public override IReadOnlyList<string> ProviderNames => new[] { "Directory Synchronization" };

    /// <inheritdoc />
    public override EventType Type => EventType.AADConnectStagingDisabled;

    /// <summary>Accepts matching staging mode disablement events.</summary>
    public override bool CanHandle(EventObject eventObject) {
        // Simple rule - always handle if event ID and log name match
        return true;
    }

    /// <summary>Server where staging mode was disabled.</summary>
    public string Computer;

    /// <summary>Operator account that disabled staging.</summary>
    public string Operator;

    /// <summary>Time when staging was disabled.</summary>
    public DateTime When;

    /// <summary>Initialises a staging-disabled wrapper from an event record.</summary>
    public AADConnectStagingDisabled(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "AADConnectStagingDisabled";
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
