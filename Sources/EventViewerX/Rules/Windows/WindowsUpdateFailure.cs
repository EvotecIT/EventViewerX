namespace EventViewerX.Rules.Windows;

/// <summary>
/// Windows Update installation failure
/// 20: Installation Failure
/// </summary>
public class WindowsUpdateFailure : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 20 };
    /// <inheritdoc />
    public override string LogName => "Setup";
    /// <inheritdoc />
    public override EventType Type => EventType.WindowsUpdateFailure;

    /// <summary>Accepts update failure events from the Windows Update Client provider.</summary>
    public override bool CanHandle(EventObject eventObject) {
        return RuleHelpers.IsProvider(eventObject, "Microsoft-Windows-WindowsUpdateClient");
    }
    /// <summary>Computer where the update failed.</summary>
    public string Computer;
    /// <summary>KB article number of the update.</summary>
    public string KB;
    /// <summary>Reason of the failure.</summary>
    public string Reason;
    /// <summary>Time the event occurred.</summary>
    public DateTime When;

    /// <summary>Initialises a Windows Update failure wrapper from an event record.</summary>
    public WindowsUpdateFailure(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "WindowsUpdateFailure";
        Computer = SourceEvent.ComputerName;
        var title = SourceEvent.GetValueFromDataDictionary("UpdateTitle", "Title");
        if (string.IsNullOrEmpty(title)) {
            title = SourceEvent.Message;
        }
        var kbMatch = System.Text.RegularExpressions.Regex.Match(title ?? string.Empty, @"KB\d{6,7}", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        KB = kbMatch.Success ? kbMatch.Value : string.Empty;
        Reason = SourceEvent.GetValueFromDataDictionary("ErrorDescription", "Message");
        if (string.IsNullOrEmpty(Reason)) {
            Reason = SourceEvent.GetValueFromDataDictionary("ErrorCode", "ResultCode");
        }
        When = SourceEvent.TimeCreated;
    }
}

