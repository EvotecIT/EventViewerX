namespace EventViewerX.Rules.Windows;

/// <summary>
/// Operating system shutdown event
/// Event ID 13
/// </summary>
public class OSShutdown : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 13 };
    /// <inheritdoc />
    public override string LogName => "System";
    /// <inheritdoc />
    public override EventType Type => EventType.OSShutdown;

    /// <summary>Accepts kernel general shutdown events.</summary>
    public override bool CanHandle(EventObject eventObject) {
        return RuleHelpers.IsProvider(eventObject, "Microsoft-Windows-Kernel-General");
    }

    /// <summary>Machine that generated the shutdown event.</summary>
    public string Computer;
    /// <summary>Action name (Shutdown).</summary>
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

    /// <summary>Initialises an OS shutdown wrapper from an event record.</summary>
    public OSShutdown(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "OSShutdown";
        Computer = SourceEvent.ComputerName;
        Action = "System Shutdown";
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
