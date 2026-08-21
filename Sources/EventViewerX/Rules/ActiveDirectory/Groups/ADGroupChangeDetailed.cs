namespace EventViewerX.Rules.ActiveDirectory;

/// <summary>
/// Active Directory Group Change Detailed
/// 5136: A directory service object was modified
/// 5137: A directory service object was created
/// 5139: A directory service object was deleted
/// 5141: A directory service object was moved
/// </summary>
public class ADGroupChangeDetailed : EventRuleBase {
    /// <summary>Domain controller where the change was captured.</summary>
    public string Computer;

    /// <summary>Action description from the event record.</summary>
    public string Action;

    /// <summary>LDAP object class (should be <c>group</c>).</summary>
    public string ObjectClass;

    /// <summary>Operation type translated to human-readable text.</summary>
    public string OperationType;

    /// <summary>Account that performed the modification.</summary>
    public string Who;

    /// <summary>Timestamp of the modification.</summary>
    public DateTime When;

    /// <summary>Distinguished name of the group affected.</summary>
    public string Group; // 'User Object'

    /// <summary>LDAP attribute that was changed.</summary>
    public string FieldChanged; // 'Field Changed'

    /// <summary>New value written to the attribute.</summary>
    public string FieldValue; // 'Field Value'

    /// <inheritdoc />
    public override List<int> EventIds => new() { 5136, 5137, 5139, 5141 };

    /// <inheritdoc />
    public override string LogName => "Security";

    /// <inheritdoc />
    public override EventType Type => EventType.ADGroupChangeDetailed;

    /// <summary>Processes only directory object events where the object class is <c>group</c>.</summary>
    public override bool CanHandle(EventObject eventObject) {
        // Check if this is a group object change
        return eventObject.TryGetDataValue("ObjectClass", out var objectClass) &&
               objectClass.Equals("group", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Initialises a detailed group change wrapper from an event record.</summary>
    public ADGroupChangeDetailed(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "ADGroupChangeDetailed";

        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;

        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;
        ObjectClass = SourceEvent.GetValueFromDataDictionary("ObjectClass");
        OperationType = ConvertFromOperationType(SourceEvent.GetDataValueOrEmpty("OperationType"));
        Group = SourceEvent.GetValueFromDataDictionary("ObjectDN");
        FieldChanged = SourceEvent.GetValueFromDataDictionary("AttributeLDAPDisplayName");
        FieldValue = SourceEvent.GetValueFromDataDictionary("AttributeValue");

        // OverwriteByField logic
        Group = OverwriteByField(Action, "A directory service object was moved.", Group, SourceEvent.GetValueFromDataDictionary("OldObjectDN"));
        FieldValue = OverwriteByField(Action, "A directory service object was moved.", FieldValue, SourceEvent.GetValueFromDataDictionary("NewObjectDN"));

        // common fields
        Who = SourceEvent.GetSubjectAccountOrEmpty();
        When = SourceEvent.TimeCreated;
    }
}



