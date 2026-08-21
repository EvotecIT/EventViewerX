namespace EventViewerX.Rules.ActiveDirectory;

/// <summary>
/// Active Directory Group Membership Changes
/// 4728: A member was added to a security-enabled global group
/// 4729: A member was removed from a security-enabled global group
/// 4732: A member was added to a security-enabled local group
/// 4733: A member was removed from a security-enabled local group
/// 4746: A member was added to a security-enabled universal group
/// 4747: A member was removed from a security-enabled universal group
/// 4751: A member was added to a distribution group
/// 4752: A member was removed from a distribution group
/// 4756: A member was added to a security-enabled universal group
/// 4757: A member was removed from a security-enabled universal group
/// 4761: A member was added to a security-enabled global group
/// 4762: A member was removed from a security-enabled global group
/// 4785: A member was added to a security-enabled universal group
/// 4786: A member was removed from a security-enabled universal group
/// 4787: A member was added to a security-enabled universal group
/// 4788: A member was removed from a security-enabled universal group
/// </summary>
public class ADGroupMembershipChange : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 4728, 4729, 4732, 4733, 4746, 4747, 4751, 4752, 4756, 4757, 4761, 4762, 4785, 4786, 4787, 4788 };

    /// <inheritdoc />
    public override string LogName => "Security";

    /// <inheritdoc />
    public override EventType Type => EventType.ADGroupMembershipChange;

    /// <summary>Handles all membership add/remove events in the Security log.</summary>
    public override bool CanHandle(EventObject eventObject) {
        // Simple rule - always handle if event ID and log name match
        return true;
    }

    /// <summary>Domain controller where the membership change was recorded.</summary>
    public string Computer;

    /// <summary>Action describing whether a member was added or removed.</summary>
    public string Action;

    /// <summary>Name of the group being modified.</summary>
    public string GroupName;

    /// <summary>Member account added or removed.</summary>
    public string MemberName;

    /// <summary>Account that executed the change.</summary>
    public string Who;

    /// <summary>Time at which the change occurred.</summary>
    public DateTime When;

    /// <summary>Initialises a group membership change wrapper from an event record.</summary>
    public ADGroupMembershipChange(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "ADGroupMembershipChange";

        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;

        GroupName = SourceEvent.GetTargetAccountOrEmpty();
        MemberName = SourceEvent.GetValueFromDataDictionary("MemberNameWithoutCN");

        // common fields
        Who = SourceEvent.GetSubjectAccountOrEmpty();
        When = SourceEvent.TimeCreated;
    }
}


