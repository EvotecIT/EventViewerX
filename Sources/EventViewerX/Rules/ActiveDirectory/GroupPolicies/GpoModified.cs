namespace EventViewerX.Rules.ActiveDirectory;

/// <summary>
/// Represents a modified group policy object event.
/// </summary>
public class GpoModified : EventRuleBase {
    /// <inheritdoc />
    public override int MatchPriority => 250;

    /// <summary>Computer where the modification occurred.</summary>
    public string Computer;
    /// <summary>Description of the action.</summary>
    public string Action;
    /// <summary>Distinguished name of the modified GPO.</summary>
    public string GpoName;
    /// <summary>LDAP display name of the changed attribute.</summary>
    public string AttributeLDAPDisplayName;
    /// <summary>New value of the attribute.</summary>
    public string AttributeValue;
    /// <summary>User responsible for the modification.</summary>
    public string Who;
    /// <summary>Time of the modification.</summary>
    public DateTime When;
    /// <inheritdoc />
    public override List<int> EventIds => new() { 5136 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.GpoModified;

    /// <summary>Processes only GPO container modifications.</summary>
    public override bool CanHandle(EventObject eventObject) {
        // Check if this is a group policy container object
        return eventObject.TryGetDataValue("ObjectClass", out var objectClass) &&
               objectClass.Equals("groupPolicyContainer", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Initialises a GPO modification wrapper from an event record.</summary>
    public GpoModified(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "GpoModified";
        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;
        GpoName = SourceEvent.GetValueFromDataDictionary("ObjectDN");
        AttributeLDAPDisplayName = SourceEvent.GetValueFromDataDictionary("AttributeLDAPDisplayName");
        AttributeValue = SourceEvent.GetValueFromDataDictionary("AttributeValue");
        Who = SourceEvent.GetSubjectAccountOrEmpty();
        When = SourceEvent.TimeCreated;
    }
}



