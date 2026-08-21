namespace EventViewerX.Rules.ActiveDirectory;

/// <summary>
/// Active Directory User Changes detailed
/// 5136: A directory service object was modified
/// 5137: A directory service object was created
/// 5139: A directory service object was deleted
/// 5141: A directory service object was moved
/// </summary>
public class ADUserChangeDetailed : EventRuleBase {
    /// <summary>
    /// Computer where the change occurred.
    /// </summary>
    public string Computer;

    /// <summary>
    /// Description of the action.
    /// </summary>
    public string Action;

    /// <summary>
    /// Class of the changed object.
    /// </summary>
    public string ObjectClass;

    /// <summary>
    /// Operation type description.
    /// </summary>
    public string OperationType;

    /// <summary>
    /// User performing the change.
    /// </summary>
    public string Who;

    /// <summary>
    /// Time when the change happened.
    /// </summary>
    public DateTime When;

    /// <summary>
    /// Affected user object.
    /// </summary>
    public string User; // 'User Object'

    /// <summary>
    /// Name of the field that was changed.
    /// </summary>
    public string FieldChanged; // 'Field Changed'

    /// <summary>
    /// New value of the changed field.
    /// </summary>
    public string FieldValue; // 'Field Value'
    /// <inheritdoc />
    public override List<int> EventIds => new() { 5136, 5137, 5139, 5141 };

    /// <inheritdoc />
    public override string LogName => "Security";

    /// <inheritdoc />
    public override EventType Type => EventType.ADUserChangeDetailed;

    /// <summary>Handles only directory events where the object class is <c>user</c>.</summary>
    public override bool CanHandle(EventObject eventObject) {
        // Check if this is a user object change
        return eventObject.TryGetDataValue("ObjectClass", out var objectClass) &&
               objectClass.Equals("user", StringComparison.OrdinalIgnoreCase);
    }


    /// <summary>Initialises a detailed user change wrapper from an event record.</summary>
    public ADUserChangeDetailed(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;

        TypeName = "ADUserChangeDetailed";
        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;
        ObjectClass = SourceEvent.GetValueFromDataDictionary("ObjectClass");
        OperationType = ConvertFromOperationType(SourceEvent.GetDataValueOrEmpty("OperationType"));
        User = SourceEvent.GetValueFromDataDictionary("ObjectDN");
        FieldChanged = SourceEvent.GetValueFromDataDictionary("AttributeLDAPDisplayName");
        FieldValue = SourceEvent.GetValueFromDataDictionary("AttributeValue");
        // common fields
        Who = SourceEvent.GetSubjectAccountOrEmpty();
        When = SourceEvent.TimeCreated;

        // OverwriteByField logic
        User = OverwriteByField(Action, "A directory service object was moved.", User, SourceEvent.GetValueFromDataDictionary("OldObjectDN"));
        FieldValue = OverwriteByField(Action, "A directory service object was moved.", FieldValue, SourceEvent.GetValueFromDataDictionary("NewObjectDN"));
    }
}



