namespace EventViewerX.Rules.Windows;

/// <summary>
/// System bugcheck event
/// 1001: The computer has rebooted from a bugcheck.
/// </summary>
public class OSBugCheck : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 1001 };
    /// <inheritdoc />
    public override string LogName => "System";
    /// <inheritdoc />
    public override EventType Type => EventType.OSBugCheck;

    /// <summary>Accepts any bugcheck (1001) event in the System log.</summary>
    public override bool CanHandle(EventObject eventObject) {
        return true;
    }

    /// <summary>Computer where the bugcheck occurred.</summary>
    public string Computer;
    /// <summary>Bugcheck code.</summary>
    public string BugCheckCode;
    /// <summary>First bugcheck parameter.</summary>
    public string Parameter1;
    /// <summary>Second bugcheck parameter.</summary>
    public string Parameter2;
    /// <summary>Third bugcheck parameter.</summary>
    public string Parameter3;
    /// <summary>Fourth bugcheck parameter.</summary>
    public string Parameter4;
    /// <summary>Path to created dump file.</summary>
    public string DumpFile;
    /// <summary>Report identifier.</summary>
    public string ReportId;
    /// <summary>Event time.</summary>
    public DateTime When;

    /// <summary>Initialises a bugcheck wrapper from an event record.</summary>
    public OSBugCheck(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "OSBugCheck";
        Computer = SourceEvent.ComputerName;
        BugCheckCode = SourceEvent.GetValueFromDataDictionary("BugcheckCode", "param1");
        Parameter1 = SourceEvent.GetValueFromDataDictionary("BugcheckParameter1", "param2");
        Parameter2 = SourceEvent.GetValueFromDataDictionary("BugcheckParameter2", "param3");
        Parameter3 = SourceEvent.GetValueFromDataDictionary("BugcheckParameter3", "param4");
        Parameter4 = SourceEvent.GetValueFromDataDictionary("BugcheckParameter4", "param5");
        DumpFile = SourceEvent.GetValueFromDataDictionary("DumpFile");
        ReportId = SourceEvent.GetValueFromDataDictionary("ReportId");
        When = SourceEvent.TimeCreated;
    }
}
