namespace EventViewerX.Rules.ActiveDirectory;

/// <summary>
/// Handles user account unlock events (4767).
/// </summary>
public class ADUserUnlocked : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 4767 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.ADUserUnlocked;

    /// <summary>Accepts account unlock events (4767).</summary>
    public override bool CanHandle(EventObject eventObject) {
        return true;
    }
    /// <summary>Machine where the unlock occurred.</summary>
    public string Computer;
    /// <summary>Description of the action.</summary>
    public string Action;
    /// <summary>Domain controller processing the unlock.</summary>
    public string ComputerLockoutOn;
    /// <summary>User account that was unlocked.</summary>
    public string UserAffected;
    /// <summary>User performing the unlock.</summary>
    public string Who;
    /// <summary>Time of the unlock.</summary>
    public DateTime When;

    /// <summary>Initialises an account unlock wrapper from an event record.</summary>
    public ADUserUnlocked(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "ADUserUnlocked";

        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;

        ComputerLockoutOn = SourceEvent.GetDataValueOrEmpty(KnownEventField.TargetDomainName);

        UserAffected = SourceEvent.GetTargetAccountOrEmpty();

        Who = SourceEvent.GetSubjectAccountOrEmpty();
        When = SourceEvent.TimeCreated;
    }
}


