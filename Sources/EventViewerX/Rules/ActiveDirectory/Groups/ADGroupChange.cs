namespace EventViewerX.Rules.ActiveDirectory;

/// <summary>
/// Active Directory Group Change
/// 4735: A security-enabled local group was created
/// 4737: A security-enabled global group was created
/// 4745: A security-enabled universal group was created
/// 4750: A security-enabled universal group was changed
/// 4760: A security-enabled global group was changed
/// 4764: A security-enabled local group was changed
/// 4784: A security-enabled universal group was deleted
/// 4791: A security-enabled global group was deleted
/// </summary>
public class ADGroupChange : EventRuleBase {
    /// <summary>Domain controller that raised the event.</summary>
    public string Computer;

    /// <summary>Short description of the change action.</summary>
    public string Action;

    /// <summary>Fully qualified name of the group being changed.</summary>
    public string GroupName;

    /// <summary>Account that performed the change.</summary>
    public string Who;

    /// <summary>Timestamp of the group change.</summary>
    public DateTime When;

    /// <summary>Indicates how the group type was modified.</summary>
    public string GroupTypeChange;

    /// <summary>SamAccountName of the group.</summary>
    public string SamAccountName;

    /// <summary>SID history entries present on the group.</summary>
    public string SidHistory;

    /// <inheritdoc />
    public override List<int> EventIds => new() { 4735, 4737, 4745, 4750, 4760, 4764, 4784, 4791 };

    /// <inheritdoc />
    public override string LogName => "Security";

    /// <inheritdoc />
    public override EventType Type => EventType.ADGroupChange;

    /// <summary>Skips anonymous noise events and only processes real actor changes.</summary>
    public override bool CanHandle(EventObject eventObject) {
        // Ignore *ANONYMOUS* events as they are not useful and clutter the view
        var who = eventObject.GetSubjectAccountOrEmpty();
        return who != "*ANONYMOUS*";
    }

    /// <summary>Initialises an Active Directory group change wrapper from an event record.</summary>
    public ADGroupChange(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "ADGroupChange";

        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;

        GroupName = SourceEvent.GetTargetAccountOrEmpty();
        GroupTypeChange = SourceEvent.GetValueFromDataDictionary("GroupTypeChange");
        SamAccountName = SourceEvent.GetValueFromDataDictionary("SamAccountName");
        SidHistory = SourceEvent.GetValueFromDataDictionary("SidHistory");

        // common fields
        Who = SourceEvent.GetSubjectAccountOrEmpty();
        When = SourceEvent.TimeCreated;
    }
}


