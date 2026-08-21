namespace EventViewerX.Rules.ActiveDirectory;

/// <summary>
/// Represents creation or modification of computer accounts.
/// Handles events 4741 and 4742.
/// </summary>
public class ADComputerCreateChange : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 4741, 4742 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.ADComputerCreateChange;

    /// <summary>Accepts matching computer create/change events without extra filtering.</summary>
    public override bool CanHandle(EventObject eventObject) {
        // Simple rule - always handle if event ID and log name match
        return true;
    }
    /// <summary>Machine where the change occurred.</summary>
    public string Computer;
    /// <summary>Short description of the action.</summary>
    public string Action;
    /// <summary>Computer affected by the event.</summary>
    public string ComputerAffected;
    /// <summary>SamAccountName of the computer.</summary>
    public string SamAccountName;
    /// <summary>Display name of the computer.</summary>
    public string DisplayName;
    /// <summary>UPN of the computer account.</summary>
    public string UserPrincipalName;
    /// <summary>Home directory path.</summary>
    public string HomeDirectory;
    /// <summary>Home path.</summary>
    public string HomePath;
    /// <summary>Logon script path.</summary>
    public string ScriptPath;
    /// <summary>Profile path.</summary>
    public string ProfilePath;
    /// <summary>Allowed workstations.</summary>
    public string UserWorkstations;
    /// <summary>Password last set timestamp.</summary>
    public string PasswordLastSet;
    /// <summary>Account expiration timestamp.</summary>
    public string AccountExpires;
    /// <summary>Primary group identifier.</summary>
    public string PrimaryGroupId;
    /// <summary>Delegation targets.</summary>
    public string AllowedToDelegateTo;
    /// <summary>Old user account control value.</summary>
    public string OldUacValue;
    /// <summary>New user account control value.</summary>
    public string NewUacValue;
    /// <summary>Translated user account control.</summary>
    public string UserAccountControl;
    /// <summary>User parameters string.</summary>
    public string UserParameters;
    /// <summary>SID history list.</summary>
    public string SidHistory;
    /// <summary>Logon hours configuration.</summary>
    public string LogonHours;
    /// <summary>DNS hostname of the computer.</summary>
    public string DnsHostName;
    /// <summary>Service principal names assigned.</summary>
    public string ServicePrincipalNames;
    /// <summary>User performing the change.</summary>
    public string Who;
    /// <summary>Time of the change.</summary>
    public DateTime When;

    /// <summary>Initialises a computer create/change wrapper from an event record.</summary>
    public ADComputerCreateChange(EventObject eventObject) : base(eventObject) {
        // common fields
        SourceEvent = eventObject;
        TypeName = "ADComputerChange";

        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;
        ComputerAffected = SourceEvent.GetTargetAccountOrEmpty();
        SamAccountName = SourceEvent.GetValueFromDataDictionary("SamAccountName");
        DisplayName = SourceEvent.GetValueFromDataDictionary("DisplayName");
        UserPrincipalName = SourceEvent.GetValueFromDataDictionary("UserPrincipalName");
        HomeDirectory = SourceEvent.GetValueFromDataDictionary("HomeDirectory");
        HomePath = SourceEvent.GetValueFromDataDictionary("HomePath");
        ScriptPath = SourceEvent.GetValueFromDataDictionary("ScriptPath");
        ProfilePath = SourceEvent.GetValueFromDataDictionary("ProfilePath");
        UserWorkstations = SourceEvent.GetValueFromDataDictionary("UserWorkstations");
        PasswordLastSet = SourceEvent.GetValueFromDataDictionary("PasswordLastSet");
        AccountExpires = SourceEvent.GetValueFromDataDictionary("AccountExpires");
        PrimaryGroupId = SourceEvent.GetValueFromDataDictionary("PrimaryGroupId");
        AllowedToDelegateTo = SourceEvent.GetValueFromDataDictionary("AllowedToDelegateTo");
        OldUacValue = SourceEvent.GetValueFromDataDictionary("OldUacValue");
        NewUacValue = SourceEvent.GetValueFromDataDictionary("NewUacValue");
        UserAccountControl = SourceEvent.GetValueFromDataDictionary("UserAccountControl");
        UserParameters = SourceEvent.GetValueFromDataDictionary("UserParameters");
        SidHistory = SourceEvent.GetValueFromDataDictionary("SidHistory");
        LogonHours = SourceEvent.GetValueFromDataDictionary("LogonHours");
        DnsHostName = SourceEvent.GetValueFromDataDictionary("DnsHostName");
        ServicePrincipalNames = SourceEvent.GetValueFromDataDictionary("ServicePrincipalNames");
        Who = SourceEvent.GetSubjectAccountOrEmpty();
        When = SourceEvent.TimeCreated;

        // let's try to translate them
        OldUacValue = TranslateUacValue(OldUacValue);
        NewUacValue = TranslateUacValue(NewUacValue);
        UserAccountControl = TranslateUacValue(UserAccountControl);
    }
}


