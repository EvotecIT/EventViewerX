namespace EventViewerX.Rules.Windows;

/// <summary>
/// Scheduled task deleted
/// 4699: A scheduled task was deleted
/// </summary>
public class ScheduledTaskDeleted : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 4699 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.ScheduledTaskDeleted;

    /// <summary>Accepts scheduled task deletion events (4699) in the Security log.</summary>
    public override bool CanHandle(EventObject eventObject) {
        return true;
    }
    /// <summary>Computer where the task was deleted.</summary>
    public string Computer;
    /// <summary>Name of the task.</summary>
    public string TaskName;
    /// <summary>User that deleted the task.</summary>
    public string Who;
    /// <summary>Time the event occurred.</summary>
    public DateTime When;

    /// <summary>Initialises a scheduled-task-deleted wrapper from an event record.</summary>
    public ScheduledTaskDeleted(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "ScheduledTaskDeleted";
        Computer = SourceEvent.ComputerName;
        TaskName = SourceEvent.GetValueFromDataDictionary("TaskName");
        Who = SourceEvent.GetSubjectAccountOrEmpty();
        When = SourceEvent.TimeCreated;
    }
}



