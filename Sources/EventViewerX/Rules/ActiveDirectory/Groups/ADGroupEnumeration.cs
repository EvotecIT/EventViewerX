namespace EventViewerX.Rules.ActiveDirectory;

/// <summary>
/// Active Directory Group Enumeration
/// 4798: A user's local group membership was enumerated
/// 4799: A security-enabled local group membership was enumerated
/// </summary>
public class ADGroupEnumeration : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 4798, 4799 };

    /// <inheritdoc />
    public override string LogName => "Security";

    /// <inheritdoc />
    public override EventType Type => EventType.ADGroupEnumeration;

    /// <summary>Accepts matching enumeration events without additional filtering.</summary>
    public override bool CanHandle(EventObject eventObject) {
        // Simple rule - always handle if event ID and log name match
        return true;
    }

    /// <summary>Domain controller where enumeration occurred.</summary>
    public string Computer;

    /// <summary>Short description of the enumeration action.</summary>
    public string Action;

    /// <summary>Group being enumerated.</summary>
    public string GroupName;

    /// <summary>Account performing the enumeration.</summary>
    public string Who;

    /// <summary>Time when enumeration happened.</summary>
    public DateTime When;

    /// <summary>Initialises a group enumeration wrapper from an event record.</summary>
    public ADGroupEnumeration(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "ADGroupEnumeration";

        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;

        GroupName = SourceEvent.GetTargetAccountOrEmpty();

        // common fields
        Who = SourceEvent.GetSubjectAccountOrEmpty();
        When = SourceEvent.TimeCreated;
    }
}


