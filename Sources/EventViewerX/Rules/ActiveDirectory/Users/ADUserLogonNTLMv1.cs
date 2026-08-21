namespace EventViewerX.Rules.ActiveDirectory;

/// <summary>
/// Handles NTLMv1 logon events (4624).
/// </summary>
public class ADUserLogonNTLMv1 : EventRuleBase {
    /// <summary>Machine where the logon occurred.</summary>
    public string Computer;
    /// <summary>Description of the action.</summary>
    public string Action;
    /// <summary>Source IP address.</summary>
    public string IpAddress;
    /// <summary>Source port number.</summary>
    public string IpPort;
    /// <summary>Account that logged on.</summary>
    public string ObjectAffected;
    /// <summary>User initiating the logon.</summary>
    public string Who;
    /// <summary>Time of the logon.</summary>
    public DateTime When;
    /// <summary>Logon process name.</summary>
    public string LogonProcessName;
    /// <summary>Impersonation level used.</summary>
    public ImpersonationLevel? ImpersonationLevel;
    /// <summary>Whether a virtual account was used.</summary>
    public VirtualAccount? VirtualAccount;
    /// <summary>Whether an elevated token was used.</summary>
    public ElevatedToken? ElevatedToken;
    /// <summary>Logon type value.</summary>
    public LogonType? LogonType;
    /// <summary>LM package name.</summary>
    public string LmPackageName;
    /// <summary>Authentication package name.</summary>
    public string PackageName;
    /// <summary>Length of the key.</summary>
    public string KeyLength;
    /// <summary>Caller process identifier.</summary>
    public string ProcessId;
    /// <summary>Caller process name.</summary>
    public string ProcessName;

    /// <inheritdoc />
    public override List<int> EventIds => new() { 4624 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.ADUserLogonNTLMv1;

    /// <summary>Handles only logon events that include the NTLM V1 package.</summary>
    public override bool CanHandle(EventObject eventObject) {
        // Only handle NTLMv1 logons
        return eventObject.ValueMatches("LmPackageName", "NTLM V1");
    }

    /// <summary>Initialises an NTLMv1 logon wrapper from an event record.</summary>
    public ADUserLogonNTLMv1(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "ADUserLogonNTLMv1";

        // Copy all the base ADUserLogon fields
        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;
        IpAddress = SourceEvent.GetDataValueOrEmpty(KnownEventField.IpAddress);
        IpPort = SourceEvent.GetDataValueOrEmpty(KnownEventField.IpPort);
        LogonProcessName = SourceEvent.GetDataValueOrEmpty(KnownEventField.LogonProcessName);
        ImpersonationLevel = EventsHelper.GetImpersonationLevel(SourceEvent.GetDataValueOrEmpty("ImpersonationLevel"));
        VirtualAccount = EventsHelper.GetVirtualAccount(SourceEvent.GetDataValueOrEmpty("VirtualAccount"));
        ElevatedToken = EventsHelper.GetElevatedToken(SourceEvent.GetDataValueOrEmpty("ElevatedToken"));
        LogonType = SourceEvent.TryGetDataEnum(KnownEventField.LogonType, out EventViewerX.LogonType parsedLogonType, EventFieldNumericBase.Decimal)
            ? parsedLogonType
            : EventsHelper.GetLogonType(SourceEvent.GetDataValueOrEmpty(KnownEventField.LogonType));
        ObjectAffected = SourceEvent.GetTargetAccountOrEmpty();
        Who = SourceEvent.GetSubjectAccountOrEmpty();
        When = SourceEvent.TimeCreated;

        // Additional NTLMv1-specific fields
        LmPackageName = SourceEvent.GetDataValueOrEmpty(KnownEventField.LmPackageName);
        PackageName = SourceEvent.GetDataValueOrEmpty(KnownEventField.AuthenticationPackageName);
        KeyLength = SourceEvent.GetDataValueOrEmpty(KnownEventField.KeyLength);
        ProcessId = SourceEvent.GetDataValueOrEmpty(KnownEventField.ProcessId);
        ProcessName = SourceEvent.GetDataValueOrEmpty(KnownEventField.ProcessName);
    }
}



