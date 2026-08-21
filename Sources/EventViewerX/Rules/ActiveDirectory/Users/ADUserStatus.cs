namespace EventViewerX.Rules.ActiveDirectory;

/// <summary>
/// A user account was enabled, disabled, unlocked, password changed, password reset, or deleted.
/// <para>4722: A user account was enabled (this includes computer accounts)</para>
/// <para>4725: A user account was disabled (this includes computer accounts)</para>
/// <para>4767: A user account was unlocked</para>
/// <para>4723: An attempt was made to change an account's password</para>
/// <para>4724: An attempt was made to reset an account's password</para>
/// <para>4726: A user account was deleted</para>
/// </summary>
public class ADUserStatus : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 4722, 4725, 4723, 4724, 4726 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.ADUserStatus;

    /// <summary>Accepts matching status change events without additional filtering.</summary>
    public override bool CanHandle(EventObject eventObject) {
        // Simple rule - always handle if event ID and log name match
        return true;
    }

    /// <summary>
    /// Computer where the status change occurred.
    /// </summary>
    public string Computer;

    /// <summary>
    /// Short description of the action.
    /// </summary>
    public string Action;

    /// <summary>
    /// User performing the change.
    /// </summary>
    public string Who;

    /// <summary>
    /// Time of the status change.
    /// </summary>
    public DateTime When;

    /// <summary>
    /// Account affected by the action.
    /// </summary>
    public string UserAffected;

    /// <summary>Initialises a user status change wrapper from an event record.</summary>
    public ADUserStatus(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "ADUsersStatus";

        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;

        UserAffected = SourceEvent.GetTargetAccountOrEmpty();

        Who = SourceEvent.GetSubjectAccountOrEmpty();
        When = SourceEvent.TimeCreated;
    }
}


