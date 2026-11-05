using System.Text.RegularExpressions;

namespace EventViewerX.Rules.Windows;

/// <summary>
/// System bugcheck event
/// 1001: The computer has rebooted from a bugcheck.
/// </summary>
public class OSBugCheck : EventRuleBase {
    private static readonly Regex PackedBugCheck = new(
        @"\A\s*(0[xX][0-9a-fA-F]+)\s*\(\s*(0[xX][0-9a-fA-F]+)\s*,\s*(0[xX][0-9a-fA-F]+)\s*,\s*(0[xX][0-9a-fA-F]+)\s*,\s*(0[xX][0-9a-fA-F]+)\s*\)\s*\z");

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

        if (RuleHelpers.IsProvider(SourceEvent, "Microsoft-Windows-WER-SystemErrorReporting") &&
            !SourceEvent.TryGetDataValue("BugcheckCode", out _)) {
            // WER stores code + parameters together; param2 and param3 describe the report.
            DumpFile = SourceEvent.GetValueFromDataDictionary("param2");
            ReportId = SourceEvent.GetValueFromDataDictionary("param3");
            Parameter1 = Parameter2 = Parameter3 = Parameter4 = string.Empty;

            Match packed = PackedBugCheck.Match(BugCheckCode);
            if (packed.Success) {
                BugCheckCode = packed.Groups[1].Value;
                Parameter1 = packed.Groups[2].Value;
                Parameter2 = packed.Groups[3].Value;
                Parameter3 = packed.Groups[4].Value;
                Parameter4 = packed.Groups[5].Value;
            }
        }
    }
}
