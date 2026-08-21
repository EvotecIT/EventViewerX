namespace EventViewerX.Rules.ActiveDirectory;

/// <summary>
/// Special privileges assigned to new logon
/// 4672: Special privileges assigned to new logon
/// </summary>
public class ADUserPrivilegeUse : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 4672 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.ADUserPrivilegeUse;

    /// <summary>Accepts special-privilege logon events (4672).</summary>
    public override bool CanHandle(EventObject eventObject) {
        return true;
    }
    /// <summary>
    /// Computer where the privileges were assigned.
    /// </summary>
    public string Computer;

    /// <summary>
    /// Description of the event action.
    /// </summary>
    public string Action;

    /// <summary>
    /// Account receiving the privileges.
    /// </summary>
    public string Who;

    /// <summary>
    /// Time when privileges were granted.
    /// </summary>
    public DateTime When;

    /// <summary>
    /// List of privilege names.
    /// </summary>
    public List<string> Privileges;

    /// <summary>
    /// Translated privilege descriptions.
    /// </summary>
    public List<string> PrivilegesTranslated;

    /// <summary>Initialises a privilege-assignment wrapper from an event record.</summary>
    public ADUserPrivilegeUse(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "ADUserPrivilegeUse";

        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;

        Who = SourceEvent.GetSubjectAccountOrEmpty();
        When = SourceEvent.TimeCreated;

        var privilegeList = SourceEvent.GetDataValueOrEmpty(KnownEventField.PrivilegeList);
        if (!string.IsNullOrEmpty(privilegeList)) {
            Privileges = privilegeList.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            PrivilegesTranslated = Privileges.Select(EventsHelper.TranslatePrivilege).ToList();
        } else {
            Privileges = new List<string>();
            PrivilegesTranslated = new List<string>();
        }
    }
}



