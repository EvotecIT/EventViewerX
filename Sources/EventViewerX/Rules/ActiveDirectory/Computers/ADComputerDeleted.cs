namespace EventViewerX.Rules.ActiveDirectory;

/// <summary>
/// Represents deletion of a computer account (event 4743).
/// </summary>
public class ADComputerDeleted : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 4743 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.ADComputerDeleted;

    /// <summary>Accepts matching computer deletion events without extra filtering.</summary>
    public override bool CanHandle(EventObject eventObject) {
        // Simple rule - always handle if event ID and log name match
        return true;
    }
    /// <summary>Machine where deletion occurred.</summary>
    public string Computer;
    /// <summary>Short description of the deletion.</summary>
    public string Action;
    /// <summary>Computer account deleted.</summary>
    public string ComputerAffected;
    /// <summary>User performing the deletion.</summary>
    public string Who;
    /// <summary>Time of deletion.</summary>
    public DateTime When;

    /// <summary>Initialises a computer deletion wrapper from an event record.</summary>
    public ADComputerDeleted(EventObject eventObject) : base(eventObject) {
        // common fields
        SourceEvent = eventObject;
        TypeName = "ADComputerDeleted";

        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;

        ComputerAffected = SourceEvent.GetTargetAccountOrEmpty();

        Who = SourceEvent.GetSubjectAccountOrEmpty();
        When = SourceEvent.TimeCreated;
    }
}


