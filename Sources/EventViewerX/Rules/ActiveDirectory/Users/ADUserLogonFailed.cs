namespace EventViewerX.Rules.ActiveDirectory;

//public enum FailureReason {
//    UnknownUserNameOrBadPassword,
//    AccountRestrictions,
//    AccountLockedOut,
//    AccountExpired,
//    LogonTypeNotGranted,
//    PasswordExpired
//}

// public enum FailureReason1 {
//     // Define failure reasons based on status codes
//     UnknownUserNameOrBadPassword = 0xC000006D,
//     AccountRestrictions = 0xC000006E,
//     AccountLockedOut = 0xC0000234,
//     AccountExpired = 0xC0000193,
//     LogonTypeNotGranted = 0xC000015B,
//     PasswordExpired = 0xC0000071
// }

/// <summary>
/// Active Directory User Logon Failed (NTLM)
///
/// Event ID: 4625
/// </summary>
public class ADUserLogonFailed : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 4625 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.ADUserLogonFailed;

    /// <summary>Accepts any 4625 logon failure events in the Security log.</summary>
    public override bool CanHandle(EventObject eventObject) {
        return true;
    }
    /// <summary>
    /// Computer on which the failed logon occurred.
    /// </summary>
    public string Computer;

    /// <summary>
    /// Description of the failure.
    /// </summary>
    public string Action;

    /// <summary>
    /// Authentication package name.
    /// </summary>
    public string PackageName;

    /// <summary>
    /// IP address of the source.
    /// </summary>
    public string IpAddress;

    /// <summary>
    /// Source port number.
    /// </summary>
    public string IpPort;
    //public string WorkstationName;
    /// <summary>
    /// Account that attempted the logon.
    /// </summary>
    public string ObjectAffected;

    /// <summary>
    /// Machine initiating the logon attempt.
    /// </summary>
    public string Who;

    /// <summary>
    /// Time when the logon failed.
    /// </summary>
    public DateTime When;

    /// <summary>
    /// Logon process name.
    /// </summary>
    public string LogonProcessName;

    /// <summary>
    /// Logon type value.
    /// </summary>
    public LogonType? LogonType;

    /// <summary>
    /// Status code returned by authentication.
    /// </summary>
    public StatusCode? Status { get; private set; }

    /// <summary>
    /// Sub status code for the failure.
    /// </summary>
    public SubStatusCode? SubStatus { get; private set; }

    /// <summary>
    /// Reason of the failure if available.
    /// </summary>
    public FailureReason? FailureReason { get; private set; }

    /// <summary>
    /// LM package name used.
    /// </summary>
    public string LmPackageName;

    /// <summary>
    /// Length of the key.
    /// </summary>
    public string KeyLength;

    /// <summary>
    /// Process identifier of the caller.
    /// </summary>
    public string ProcessId;

    /// <summary>
    /// Name of the process.
    /// </summary>
    public string ProcessName;

    /// <summary>
    /// Services requested during authentication.
    /// </summary>
    public string TransmittedServices;


    /// <summary>Initialises a failed logon wrapper from an event record.</summary>
    public ADUserLogonFailed(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "ADUserLogonFailed";
        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;
        //Who = SourceEvent.GetSubjectAccountOrEmpty();
        Who = SourceEvent.GetDataValueOrEmpty(KnownEventField.WorkstationName);
        ObjectAffected = SourceEvent.GetTargetAccountOrEmpty();
        IpAddress = SourceEvent.GetDataValueOrEmpty(KnownEventField.IpAddress);
        IpPort = SourceEvent.GetDataValueOrEmpty(KnownEventField.IpPort);
        //WorkstationName = SourceEvent.GetDataValueOrEmpty(KnownEventField.WorkstationName);
        LogonProcessName = SourceEvent.GetDataValueOrEmpty(KnownEventField.LogonProcessName);
        LogonType = SourceEvent.TryGetDataEnum(KnownEventField.LogonType, out EventViewerX.LogonType parsedLogonType, EventFieldNumericBase.Decimal)
            ? parsedLogonType
            : null;
        Status = SourceEvent.TryGetDataEnum(KnownEventField.Status, out StatusCode parsedStatus, EventFieldNumericBase.Hexadecimal)
            ? parsedStatus
            : null;
        SubStatus = SourceEvent.TryGetDataEnum(KnownEventField.SubStatus, out SubStatusCode parsedSubStatus, EventFieldNumericBase.Hexadecimal)
            ? parsedSubStatus
            : null;
        FailureReason = SourceEvent.TryGetDataEnum(KnownEventField.FailureReason, out EventViewerX.FailureReason parsedFailureReason, EventFieldNumericBase.Decimal, "%%")
            ? parsedFailureReason
            : null;

        LmPackageName = SourceEvent.GetDataValueOrEmpty(KnownEventField.LmPackageName);
        KeyLength = SourceEvent.GetDataValueOrEmpty(KnownEventField.KeyLength);
        ProcessId = SourceEvent.GetDataValueOrEmpty(KnownEventField.ProcessId);
        ProcessName = SourceEvent.GetDataValueOrEmpty(KnownEventField.ProcessName);
        TransmittedServices = SourceEvent.GetDataValueOrEmpty(KnownEventField.TransmittedServices);
        PackageName = SourceEvent.GetDataValueOrEmpty(KnownEventField.AuthenticationPackageName);
        When = SourceEvent.TimeCreated;
    }

    /// <summary>
    /// Gets a human-readable description of the failure
    /// </summary>
    public string GetFailureDescription() {
        var description = new System.Text.StringBuilder();

        if (Status.HasValue)
            description.AppendLine($"Status: {Status} (0x{(uint)Status:X8})");

        if (SubStatus.HasValue)
            description.AppendLine($"SubStatus: {SubStatus} (0x{(uint)SubStatus:X8})");

        if (FailureReason.HasValue)
            description.AppendLine($"Reason: {FailureReason}");

        return description.ToString().TrimEnd();
    }
}


