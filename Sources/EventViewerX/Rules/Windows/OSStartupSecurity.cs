namespace EventViewerX.Rules.Windows;

/// <summary>
/// Operating system startup event from Security log
/// Event ID 4608
/// </summary>
public class OSStartupSecurity : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 4608 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.OSStartupSecurity;

    /// <summary>Accepts all startup events within the Security log.</summary>
    /// <param name="eventObject">Event to evaluate.</param>
    /// <returns>Always <c>true</c> for this rule.</returns>
    public override bool CanHandle(EventObject eventObject) {
        return true;
    }

    /// <summary>Machine that logged the startup in the Security log.</summary>
    public string Computer;
    /// <summary>Action description (Windows is starting up).</summary>
    public string Action;
    /// <summary>Object affected by the action (typically the host).</summary>
    public string ObjectAffected;
    /// <summary>Detail string from the security event.</summary>
    public string ActionDetails;
    /// <summary>Timestamp in UTC parsed from the payload when present.</summary>
    public DateTime? ActionTimestampUtc;
    /// <summary>ISO-8601 representation of the UTC timestamp.</summary>
    public string ActionTimestampIso => ActionTimestampUtc?.ToString("o") ?? string.Empty;
    /// <summary>Event timestamp.</summary>
    public DateTime When;

    /// <summary>Creates a wrapper for Security log startup events (4608).</summary>
    /// <param name="eventObject">Event carrying startup details.</param>
    public OSStartupSecurity(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "OSStartupSecurity";
        Computer = SourceEvent.ComputerName;
        Action = "Windows is starting up";
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
