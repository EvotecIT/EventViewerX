namespace EventViewerX.Rules.Windows;

/// <summary>
/// Windows Firewall rule modified
/// 4947: A change has been made to Windows Firewall exception list. A rule was modified.
/// </summary>
public class FirewallRuleChange : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 4947 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.FirewallRuleChange;

    /// <summary>Accepts firewall rule modification events from auditing or firewall providers.</summary>
    public override bool CanHandle(EventObject eventObject) {
        return RuleHelpers.IsProvider(eventObject, "Microsoft-Windows-Security-Auditing", "Microsoft-Windows-Windows Firewall With Advanced Security");
    }
    /// <summary>Computer where the rule was modified.</summary>
    public string Computer;
    /// <summary>Description of the action.</summary>
    public string Action;
    /// <summary>Name of the rule.</summary>
    public string RuleName;
    /// <summary>Firewall profile that changed.</summary>
    public string ProfileChanged;
    /// <summary>Time the event occurred.</summary>
    public DateTime When;

    /// <summary>Initialises a firewall rule change wrapper from an event record.</summary>
    public FirewallRuleChange(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "FirewallRuleChange";
        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;
        RuleName = SourceEvent.GetValueFromDataDictionary("RuleName");
        ProfileChanged = SourceEvent.GetValueFromDataDictionary("ProfileChanged");
        When = SourceEvent.TimeCreated;
    }
}

