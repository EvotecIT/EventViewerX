namespace EventViewerX.Rules.Logging;

/// <summary>
/// Logs Cleared Application, System, Others
/// 104: The audit log was cleared
/// </summary>
public class LogsClearedOther : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 104 };
    /// <inheritdoc />
    public override string LogName => "System";
    /// <inheritdoc />
    public override EventType Type => EventType.LogsClearedOther;

    /// <summary>Verifies the event originates from the EventLog provider.</summary>
    public override bool CanHandle(EventObject eventObject) {
        return RuleHelpers.IsProvider(eventObject, "EventLog", "Microsoft-Windows-Eventlog");
    }
    /// <summary>Machine where the log was cleared.</summary>
    public string Computer;
    /// <summary>Action description from the event.</summary>
    public string Action;
    /// <summary>Backup path (if automatic backup occurred).</summary>
    public string BackupPath;
    /// <summary>Channel that was cleared.</summary>
    public string LogType;
    /// <summary>Account responsible for clearing/backing up the log.</summary>
    public string Who;
    /// <summary>Timestamp of the event.</summary>
    public DateTime When;

    /// <summary>Initialises a log-cleared (non-Security) event wrapper.</summary>
    public LogsClearedOther(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;

        TypeName = "LogsClearedOther";
        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;
        BackupPath = SourceEvent.GetValueFromDataDictionary("BackupPath");
        LogType = ConvertFromOperationType(SourceEvent.GetDataValueOrEmpty("Channel"));

        // common fields
        Who = SourceEvent.GetSubjectAccountOrEmpty();
        When = SourceEvent.TimeCreated;


        if (BackupPath == "") {
            BackupPath = "N/A";
        }
    }
}



