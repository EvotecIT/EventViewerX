namespace EventViewerX.Rules.Windows;

/// <summary>
/// System audit policy was changed
/// 4719: System audit policy was changed
/// </summary>
public class AuditPolicyChange : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 4719 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.AuditPolicyChange;

    /// <summary>Accepts system audit policy change events.</summary>
    public override bool CanHandle(EventObject eventObject) {
        return true;
    }
    /// <summary>Machine where the audit policy was changed.</summary>
    public string Computer;
    /// <summary>High-level audit policy category identifier.</summary>
    public string CategoryId;
    /// <summary>Specific subcategory identifier affected by the change.</summary>
    public string SubcategoryId;
    /// <summary>GUID of the audit policy subcategory.</summary>
    public string SubcategoryGuid;
    /// <summary>Text describing the applied audit policy change.</summary>
    public string AuditPolicyChanges;
    /// <summary>Account that performed the change.</summary>
    public string Who;
    /// <summary>Timestamp when the change occurred.</summary>
    public DateTime When;

    /// <summary>Initialises an audit policy change wrapper from an event record.</summary>
    public AuditPolicyChange(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "AuditPolicyChange";
        Computer = SourceEvent.ComputerName;
        CategoryId = SourceEvent.GetValueFromDataDictionary("CategoryId");
        SubcategoryId = SourceEvent.GetValueFromDataDictionary("SubcategoryId");
        SubcategoryGuid = SourceEvent.GetValueFromDataDictionary("SubcategoryGuid");
        AuditPolicyChanges = SourceEvent.GetValueFromDataDictionary("AuditPolicyChanges");
        Who = SourceEvent.GetSubjectAccountOrEmpty();
        When = SourceEvent.TimeCreated;
    }
}



