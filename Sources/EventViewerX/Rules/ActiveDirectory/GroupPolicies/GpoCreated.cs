namespace EventViewerX.Rules.ActiveDirectory;

/// <summary>
/// Represents a newly created group policy object event.
/// </summary>
public class GpoCreated : EventRuleBase {
    /// <inheritdoc />
    public override int MatchPriority => 250;

    /// <summary>Computer on which the GPO was created.</summary>
    public string Computer;
    /// <summary>Description of the action.</summary>
    public string Action;
    /// <summary>Distinguished name of the new GPO.</summary>
    public string GpoName;
    /// <summary>User that created the GPO.</summary>
    public string Who;
    /// <summary>Time the GPO was created.</summary>
    public DateTime When;
    /// <inheritdoc />
    public override List<int> EventIds => new() { 5137 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.GpoCreated;

    /// <summary>Processes only groupPolicyContainer object creations.</summary>
    public override bool CanHandle(EventObject eventObject) {
        // Check if this is a group policy container object
        return eventObject.TryGetDataValue("ObjectClass", out var objectClass) &&
               objectClass.Equals("groupPolicyContainer", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Initialises a GPO creation wrapper from an event record.</summary>
    public GpoCreated(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "GpoCreated";
        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;
        GpoName = SourceEvent.GetValueFromDataDictionary("ObjectDN");
        Who = SourceEvent.GetSubjectAccountOrEmpty();
        When = SourceEvent.TimeCreated;
    }
}



