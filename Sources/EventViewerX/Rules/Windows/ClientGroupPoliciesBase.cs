namespace EventViewerX.Rules.Windows;

/// <summary>
/// Base class for client side group policy processing events.
/// </summary>
public abstract class ClientGroupPoliciesBase : EventRuleBase {
    /// <summary>Machine where the policy processing occurred.</summary>
    public string Computer;
    /// <summary>High-level description of the policy processing action.</summary>
    public string Action;
    /// <summary>Scope of the policy (computer or user).</summary>
    public string PolicyScope;
    /// <summary>Name of the item being applied.</summary>
    public string ItemName;
    /// <summary>Display name of the GPO or extension.</summary>
    public string PolicyName;
    /// <summary>Error text supplied by the event, if any.</summary>
    public string Error;
    /// <summary>Timestamp (UTC) parsed from the event payload when available.</summary>
    public DateTime? ActionTimestampUtc;
    /// <summary>ISO-8601 representation of <see cref="ActionTimestampUtc"/>.</summary>
    public string ActionTimestampIso => ActionTimestampUtc?.ToString("o") ?? string.Empty;
    /// <summary>Account that processed the policy.</summary>
    public string Who;
    /// <summary>Event timestamp (local time).</summary>
    public DateTime When;

    /// <summary>
    /// Seeds common fields for group policy client events.
    /// </summary>
    /// <param name="eventObject">Underlying event record.</param>
    protected ClientGroupPoliciesBase(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = GetType().Name;
        Computer = SourceEvent.ComputerName;
        Action = SourceEvent.MessageSubject;
        PolicyScope = SourceEvent.GetValueFromDataDictionary("NoNameA0");
        ItemName = SourceEvent.GetValueFromDataDictionary("NoNameA1");
        PolicyName = SourceEvent.GetValueFromDataDictionary("NoNameA2", "ExtensionName");
        Error = SourceEvent.GetValueFromDataDictionary("NoNameA3", "ErrorDescription");
        Who = SourceEvent.GetSubjectAccountOrEmpty();
        ActionTimestampUtc = SourceEvent.TimeCreated.ToUniversalTime();
        When = ActionTimestampUtc ?? SourceEvent.TimeCreated;
    }

    /// <summary>Accepts group policy operational events from the relevant providers.</summary>
    /// <param name="eventObject">Event to evaluate.</param>
    /// <returns><c>true</c> when the provider/channel matches GroupPolicy.</returns>
    public override bool CanHandle(EventObject eventObject) {
        return RuleHelpers.IsProvider(eventObject, "Microsoft-Windows-GroupPolicy") ||
               RuleHelpers.IsChannel(eventObject, "Microsoft-Windows-GroupPolicy/Operational");
    }
}



