namespace EventViewerX.Rules.ActiveDirectory;

/// <summary>
/// Represents a deleted group policy object event.
/// </summary>
public class GpoDeleted : EventRuleBase {
    /// <summary>Computer where the GPO was deleted.</summary>
    public string Computer;
    /// <summary>Description of the action.</summary>
    public string Action;
    /// <summary>Distinguished name of the deleted GPO.</summary>
    public string GpoName;
    /// <summary>User that deleted the GPO.</summary>
    public string Who;
    /// <summary>Time the GPO was deleted.</summary>
    public DateTime When;
    /// <inheritdoc />
    public override List<int> EventIds => new() { 5141 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.GpoDeleted;

    /// <summary>Processes only GPO container objects for deletion events.</summary>
    public override bool CanHandle(EventObject eventObject) {
        // Check if this is a group policy container object
        return eventObject.TryGetDataValue("ObjectClass", out var objectClass) &&
               objectClass.Equals("groupPolicyContainer", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Initialises a GPO deletion wrapper from an event record.</summary>
    public GpoDeleted(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "GpoDeleted";
        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;
        GpoName = SourceEvent.GetValueFromDataDictionary("ObjectDN");
        Who = SourceEvent.GetSubjectAccountOrEmpty();
        When = SourceEvent.TimeCreated;
    }
}



