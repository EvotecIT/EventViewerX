namespace EventViewerX.Rules.ActiveDirectory;

/// <summary>
/// Handles user account lockout events (4740).
/// </summary>
public class ADUserLockouts : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 4740 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.ADUserLockouts;

    /// <summary>Accepts account lockout events (4740).</summary>
    public override bool CanHandle(EventObject eventObject) {
        return true;
    }
    /// <summary>Machine where the lockout occurred.</summary>
    public string Computer;
    /// <summary>Description of the action.</summary>
    public string Action;
    /// <summary>
    /// Caller computer name reported by the event (machine where bad-password attempts were observed).
    /// </summary>
    public string CallerComputerName;

    /// <summary>
    /// Back-compat alias for <see cref="CallerComputerName"/>.
    /// </summary>
    public string ComputerLockoutOn;
    /// <summary>Locked out account.</summary>
    public string UserAffected;
    /// <summary>User who performed the action.</summary>
    public string Who;
    /// <summary>Time of the lockout.</summary>
    public DateTime When;

    /// <summary>Initialises an account lockout wrapper from an event record.</summary>
    public ADUserLockouts(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "ADUserLockouts";

        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;

        var caller = SourceEvent.GetValueFromDataDictionary("CallerComputerName");
        CallerComputerName = string.IsNullOrWhiteSpace(caller) ? string.Empty : caller.Trim();
        ComputerLockoutOn = CallerComputerName;

        UserAffected = SourceEvent.GetTargetAccountOrEmpty();

        Who = SourceEvent.GetSubjectAccountOrEmpty();
        When = SourceEvent.TimeCreated;
    }
}


