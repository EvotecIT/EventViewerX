namespace EventViewerX.Rules.Logging;

/// <summary>
/// Logs Cleared Security
/// 1102: The audit log was cleared
/// </summary>
public class LogsClearedSecurity : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 1102 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override IReadOnlyList<string> ProviderNames => new[] { "Microsoft-Windows-Eventlog" };
    /// <inheritdoc />
    public override EventType Type => EventType.LogsClearedSecurity;

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

    /// <summary>Initialises a log-cleared Security event wrapper.</summary>
    public LogsClearedSecurity(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;

        TypeName = "LogsClearedSecurity";
        Computer = SourceEvent.ComputerName;
        Action = string.IsNullOrWhiteSpace(SourceEvent.MessageSubject) ? "Audit log cleared" : SourceEvent.MessageSubject;
        BackupPath = SourceEvent.GetValueFromDataDictionary("BackupPath");
        LogType = Value("Channel");
        if (LogType.Length == 0) { LogType = SourceEvent.OriginalLogName; }

        // common fields
        Who = SourceEvent.GetSubjectAccountOrEmpty();
        When = SourceEvent.TimeCreated;



    }

}



