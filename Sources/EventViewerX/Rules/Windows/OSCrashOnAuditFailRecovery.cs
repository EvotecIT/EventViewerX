namespace EventViewerX.Rules.Windows;

/// <summary>
/// Administrator recovered system from CrashOnAuditFail
/// Event ID 4621
/// </summary>
public class OSCrashOnAuditFailRecovery : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 4621 };
    /// <inheritdoc />
    public override string LogName => "Security";
    /// <inheritdoc />
    public override EventType Type => EventType.OSCrashOnAuditFailRecovery;

    /// <summary>Accepts security auditing provider events for CrashOnAuditFail recovery.</summary>
    /// <param name="eventObject">Event to evaluate.</param>
    /// <returns><c>true</c> when the provider is Microsoft-Windows-Security-Auditing.</returns>
    public override bool CanHandle(EventObject eventObject) {
        return RuleHelpers.IsProvider(eventObject, "Microsoft-Windows-Security-Auditing");
    }

    /// <summary>Computer where the recovery was recorded.</summary>
    public string Computer;
    /// <summary>Human-friendly action description.</summary>
    public string Action;
    /// <summary>Target object affected by the recovery action.</summary>
    public string ObjectAffected;
    /// <summary>Detail string supplied by the event.</summary>
    public string ActionDetails;
    /// <summary>Recovery timestamp in UTC when available.</summary>
    public DateTime? ActionTimestampUtc;
    /// <summary>ISO-8601 representation of <see cref="ActionTimestampUtc"/>.</summary>
    public string ActionTimestampIso => ActionTimestampUtc?.ToString("o") ?? string.Empty;
    /// <summary>Event timestamp.</summary>
    public DateTime When;

    /// <summary>
    /// Builds a CrashOnAuditFail recovery record from security event 4621.
    /// </summary>
    /// <param name="eventObject">Event describing the recovery.</param>
    public OSCrashOnAuditFailRecovery(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "OSCrashOnAuditFailRecovery";
        Computer = SourceEvent.ComputerName;
        Action = "Administrator recovered system from CrashOnAuditFail";
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
