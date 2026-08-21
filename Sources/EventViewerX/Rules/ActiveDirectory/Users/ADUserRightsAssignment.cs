namespace EventViewerX.Rules.ActiveDirectory;

/// <summary>
/// Handles assignment or removal of user rights (events 4704 and 4705).
/// </summary>
public class ADUserRightsAssignment : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 4704, 4705 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.ADUserRightsAssignment;

    /// <summary>Accepts matching user rights assignment events without extra filters.</summary>
    public override bool CanHandle(EventObject eventObject) {
        // Simple rule - always handle if event ID and log name match
        return true;
    }
    /// <summary>Machine where the change occurred.</summary>
    public string Computer;
    /// <summary>Description of the action.</summary>
    public string Action;
    /// <summary>User account affected.</summary>
    public string UserAffected;
    /// <summary>User performing the action.</summary>
    public string Who;
    /// <summary>Time of the change.</summary>
    public DateTime When;
    /// <summary>List of user rights assigned or removed.</summary>
    public List<string> Rights;
    /// <summary>Translated user rights.</summary>
    public List<string> RightsTranslated;

    /// <summary>Initialises a user rights assignment wrapper from an event record.</summary>
    public ADUserRightsAssignment(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "ADUserRightsAssignment";

        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;

        UserAffected = SourceEvent.GetTargetAccountOrEmpty();
        if (string.IsNullOrEmpty(UserAffected)) {
            var sid = SourceEvent.GetDataValueOrEmpty(KnownEventField.TargetSid);
            if (!string.IsNullOrEmpty(sid)) {
                try {
                    var identifier = new System.Security.Principal.SecurityIdentifier(sid);
                    UserAffected = identifier.Translate(typeof(System.Security.Principal.NTAccount)).ToString();
                } catch {
                    UserAffected = sid;
                }
            }
        }

        var privileges = SourceEvent.GetDataValueOrEmpty(KnownEventField.PrivilegeList);
        if (!string.IsNullOrEmpty(privileges)) {
            Rights = privileges.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            RightsTranslated = Rights.Select(EventsHelper.TranslatePrivilege).ToList();
        } else {
            Rights = new List<string>();
            RightsTranslated = new List<string>();
        }

        Who = SourceEvent.GetSubjectAccountOrEmpty();
        When = SourceEvent.TimeCreated;
    }
}



