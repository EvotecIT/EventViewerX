namespace EventViewerX.Rules.Windows;

/// <summary>Common projected fields for a Windows Firewall rule lifecycle event.</summary>
public abstract class FirewallRuleLifecycle : EventRuleBase {
    /// <summary>Creates a firewall rule projection.</summary>
    protected FirewallRuleLifecycle(EventObject eventObject, string typeName) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = typeName;
        Computer = SourceEvent.ComputerName;
        RuleName = SourceEvent.GetValueFromDataDictionary("RuleName");
        RuleId = SourceEvent.GetValueFromDataDictionary("RuleId");
        ProfileChanged = SourceEvent.GetValueFromDataDictionary("ProfileChanged");
        Who = SourceEvent.GetSubjectAccountOrEmpty();
        When = SourceEvent.TimeCreated;
    }
    /// <summary>Accepts firewall events only from expected providers.</summary>
    public override bool CanHandle(EventObject eventObject) => RuleHelpers.IsProvider(
        eventObject,
        "Microsoft-Windows-Security-Auditing",
        "Microsoft-Windows-Windows Firewall With Advanced Security");
    /// <summary>Computer where the rule changed.</summary>
    public string Computer = string.Empty;
    /// <summary>Firewall rule display name.</summary>
    public string RuleName = string.Empty;
    /// <summary>Stable firewall rule identifier.</summary>
    public string RuleId = string.Empty;
    /// <summary>Affected firewall profile.</summary>
    public string ProfileChanged = string.Empty;
    /// <summary>Account that changed the rule.</summary>
    public string Who = string.Empty;
    /// <summary>Event timestamp.</summary>
    public DateTime When;
}

/// <summary>A Windows Firewall rule was added (4946).</summary>
public sealed class FirewallRuleAdded : FirewallRuleLifecycle {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 4946 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.FirewallRuleAdded;
    /// <summary>Creates the typed projection.</summary>
    public FirewallRuleAdded(EventObject eventObject) : base(eventObject, nameof(FirewallRuleAdded)) { }
}

/// <summary>A Windows Firewall rule was deleted (4948).</summary>
public sealed class FirewallRuleDeleted : FirewallRuleLifecycle {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 4948 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.FirewallRuleDeleted;
    /// <summary>Creates the typed projection.</summary>
    public FirewallRuleDeleted(EventObject eventObject) : base(eventObject, nameof(FirewallRuleDeleted)) { }
}
