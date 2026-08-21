namespace EventViewerX.Rules.ActiveDirectory;

/// <summary>
/// Represents detailed Active Directory computer object changes.
/// Handles events 5136, 5137, 5139 and 5141.
/// </summary>
public class ADComputerChangeDetailed : EventRuleBase {
    /// <summary>Machine where the change occurred.</summary>
    public string Computer;
    /// <summary>Short description of the change.</summary>
    public string Action;
    /// <summary>LDAP object class.</summary>
    public string ObjectClass;
    /// <summary>Translated operation type.</summary>
    public string OperationType;
    /// <summary>User performing the change.</summary>
    public string Who;
    /// <summary>Time of the change.</summary>
    public DateTime When;
    /// <summary>Distinguished name of the computer object.</summary>
    public string ComputerObject; // 'Computer Object'
    /// <summary>Attribute modified.</summary>
    public string FieldChanged;
    /// <summary>Value after modification.</summary>
    public string FieldValue;

    //public string ClientDNSName;

    /// <inheritdoc />
    public override List<int> EventIds => new() { 5136, 5137, 5139, 5141 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.ADComputerChangeDetailed;

    /// <summary>
    /// Determines whether the event represents a computer object change.
    /// </summary>
    /// <param name="eventObject">Event to evaluate.</param>
    /// <returns><c>true</c> when the ObjectClass indicates a computer; otherwise <c>false</c>.</returns>
    public override bool CanHandle(EventObject eventObject) {
        // Check if this is a computer object change
        return eventObject.TryGetDataValue("ObjectClass", out var objectClass) &&
               objectClass.Equals("computer", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates a detailed computer change wrapper when the event matches, otherwise returns <c>null</c>.
    /// </summary>
    public static EventTypeRecord? Create(EventObject eventObject) {
        var rule = new ADComputerChangeDetailed(eventObject);
        return rule.CanHandle(eventObject) ? rule : null;
    }

    /// <summary>
    /// Active Directory Computer Change Detailed
    /// </summary>
    /// <param name="eventObject">Underlying event record.</param>
    public ADComputerChangeDetailed(EventObject eventObject) : base(eventObject) {
        // common fields
        SourceEvent = eventObject;
        TypeName = "ADComputerChangeDetailed";
        Computer = SourceEvent.ComputerName;
        ObjectClass = SourceEvent.GetValueFromDataDictionary("ObjectClass");
        Action = SourceEvent.MessageSubject;
        Who = SourceEvent.GetSubjectAccountOrEmpty();
        When = SourceEvent.TimeCreated;
        //
        OperationType = ConvertFromOperationType(SourceEvent.GetDataValueOrEmpty("OperationType"));
        ComputerObject = SourceEvent.GetValueFromDataDictionary("ObjectDN");
        FieldChanged = SourceEvent.GetValueFromDataDictionary("AttributeLDAPDisplayName");
        FieldValue = SourceEvent.GetValueFromDataDictionary("AttributeValue");
        // OverwriteByField logic
        ComputerObject = OverwriteByField(Action, "A directory service object was moved.", ComputerObject, SourceEvent.GetValueFromDataDictionary("OldObjectDN"));
        FieldValue = OverwriteByField(Action, "A directory service object was moved.", FieldValue, SourceEvent.GetValueFromDataDictionary("NewObjectDN"));
    }
}



