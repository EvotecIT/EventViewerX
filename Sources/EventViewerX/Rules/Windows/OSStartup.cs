namespace EventViewerX.Rules.Windows;

/// <summary>
/// Operating system startup event from System log
/// Event ID 12
/// </summary>
public class OSStartup : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 12 };
    /// <inheritdoc />
    public override string LogName => "System";
    /// <inheritdoc />
    public override EventType Type => EventType.OSStartup;

    /// <summary>Accepts kernel general provider startup events.</summary>
    public override bool CanHandle(EventObject eventObject) {
        return RuleHelpers.IsProvider(eventObject, "Microsoft-Windows-Kernel-General");
    }

    /// <summary>Machine that generated the startup event.</summary>
    public string Computer;
    /// <summary>Action name (System Start).</summary>
    public string Action;
    /// <summary>Object affected by the action (typically the host).</summary>
    public string ObjectAffected;
    /// <summary>Additional details from the event payload.</summary>
    public string ActionDetails;
    /// <summary>Timestamp in UTC parsed from the event if present.</summary>
    public DateTime? ActionTimestampUtc;
    /// <summary>ISO-8601 representation of the UTC timestamp.</summary>
    public string ActionTimestampIso => ActionTimestampUtc?.ToString("o") ?? string.Empty;
    /// <summary>Event timestamp.</summary>
    public DateTime When;

    /// <summary>Initialises an OS startup wrapper from an event record.</summary>
    public OSStartup(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "OSStartup";
        Computer = SourceEvent.ComputerName;
        Action = "System Start";
        ObjectAffected = SourceEvent.MachineName;
        ActionDetails = SourceEvent.MessageSubject;
        var rawStartText = SourceEvent.GetValueFromDataDictionary("StartTime") ??
                           SourceEvent.GetValueFromDataDictionary("#text") ??
                           SourceEvent.GetValueFromDataDictionary("ActionDetailsDateTime");
        ActionTimestampUtc = RuleHelpers.ParseUnlabeledOsTimestamp(SourceEvent)
                            ?? RuleHelpers.ParseDateTimeLoose(rawStartText)
                            ?? SourceEvent.TimeCreated.ToUniversalTime();
        When = ActionTimestampUtc ?? SourceEvent.TimeCreated;
    }
}
