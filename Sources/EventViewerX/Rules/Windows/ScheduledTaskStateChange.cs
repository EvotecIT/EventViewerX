namespace EventViewerX.Rules.Windows;

/// <summary>Common projected fields for scheduled-task state changes.</summary>
public abstract class ScheduledTaskStateChange : EventRuleBase {
    /// <summary>Creates a scheduled-task state-change projection.</summary>
    protected ScheduledTaskStateChange(EventObject eventObject, string typeName) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = typeName;
        Computer = SourceEvent.ComputerName;
        TaskName = SourceEvent.GetValueFromDataDictionary("TaskName");
        Who = SourceEvent.GetSubjectAccountOrEmpty();
        When = SourceEvent.TimeCreated;
    }
    /// <summary>Accepts the event after its source and identifier match.</summary>
    public override bool CanHandle(EventObject eventObject) => true;
    /// <summary>Computer where the task changed.</summary>
    public string Computer = string.Empty;
    /// <summary>Task path and name.</summary>
    public string TaskName = string.Empty;
    /// <summary>Account that changed the task.</summary>
    public string Who = string.Empty;
    /// <summary>Event timestamp.</summary>
    public DateTime When;
}

/// <summary>A scheduled task was enabled (4700).</summary>
public sealed class ScheduledTaskEnabled : ScheduledTaskStateChange {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 4700 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.ScheduledTaskEnabled;
    /// <summary>Creates the typed projection.</summary>
    public ScheduledTaskEnabled(EventObject eventObject) : base(eventObject, nameof(ScheduledTaskEnabled)) { }
}

/// <summary>A scheduled task was disabled (4701).</summary>
public sealed class ScheduledTaskDisabled : ScheduledTaskStateChange {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 4701 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.ScheduledTaskDisabled;
    /// <summary>Creates the typed projection.</summary>
    public ScheduledTaskDisabled(EventObject eventObject) : base(eventObject, nameof(ScheduledTaskDisabled)) { }
}

/// <summary>A scheduled task was updated (4702).</summary>
public sealed class ScheduledTaskUpdated : ScheduledTaskStateChange {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 4702 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.ScheduledTaskUpdated;
    /// <summary>Updated task XML.</summary>
    public string TaskContent = string.Empty;
    /// <summary>Creates the typed projection.</summary>
    public ScheduledTaskUpdated(EventObject eventObject) : base(eventObject, nameof(ScheduledTaskUpdated)) {
        TaskContent = SourceEvent.GetValueFromDataDictionary("TaskContent");
    }
}
