namespace EventViewerX.Rules.Windows;

/// <summary>
/// Scheduled task created
/// 4698: A scheduled task was created
/// </summary>
public class ScheduledTaskCreated : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 4698 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.ScheduledTaskCreated;

    /// <summary>Accepts scheduled task creation events (4698) in the Security log.</summary>
    public override bool CanHandle(EventObject eventObject) {
        return true;
    }
    /// <summary>Computer where the task was created.</summary>
    public string Computer = string.Empty;
    /// <summary>Name of the created task.</summary>
    public string TaskName = string.Empty;
    /// <summary>Author of the task definition.</summary>
    public string Author = string.Empty;
    /// <summary>Creation timestamp read from the task XML.</summary>
    public DateTime? Created;
    /// <summary>Time the event occurred.</summary>
    public DateTime When;

    /// <summary>Initialises a scheduled-task-created wrapper from an event record.</summary>
    public ScheduledTaskCreated(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "ScheduledTaskCreated";
        Computer = SourceEvent.ComputerName;
        TaskName = SourceEvent.GetValueFromDataDictionary("TaskName");
        var taskContent = SourceEvent.GetValueFromDataDictionary("TaskContent");
        if (!string.IsNullOrEmpty(taskContent)) {
            try {
                var xml = System.Xml.Linq.XDocument.Parse(taskContent);
                Author = xml.Root?.Element("RegistrationInfo")?.Element("Author")?.Value ?? string.Empty;
                var createdStr = xml.Root?.Element("RegistrationInfo")?.Element("Date")?.Value;
                if (DateTime.TryParse(createdStr, out var dt)) {
                    Created = dt;
                }
            } catch {
                // ignore malformed XML
            }
        }
        When = SourceEvent.TimeCreated;
    }
}

