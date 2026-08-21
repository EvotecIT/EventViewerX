namespace EventViewerX.Rules.Windows;

/// <summary>
/// Operating system rebooted without clean shutdown
/// Event ID 41
/// </summary>
public class OSUncleanShutdown : EventRuleBase {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 41 };
    /// <inheritdoc />
    public override string LogName => "System";
    /// <inheritdoc />
    public override EventType Type => EventType.OSUncleanShutdown;

    /// <summary>Accepts kernel power events indicating unclean shutdowns.</summary>
    public override bool CanHandle(EventObject eventObject) {
        return RuleHelpers.IsProvider(eventObject, "Microsoft-Windows-Kernel-Power");
    }

    /// <summary>Machine that logged the unclean shutdown.</summary>
    public string Computer;
    /// <summary>Action description (dirty reboot).</summary>
    public string Action;
    /// <summary>Object affected by the action (typically the host).</summary>
    public string ObjectAffected;
    /// <summary>Detail string from the event payload.</summary>
    public string ActionDetails;
    /// <summary>Timestamp in UTC parsed from the payload when present.</summary>
    public DateTime? ActionTimestampUtc;
    /// <summary>ISO-8601 representation of the UTC timestamp.</summary>
    public string ActionTimestampIso => ActionTimestampUtc?.ToString("o") ?? string.Empty;
    /// <summary>Event timestamp.</summary>
    public DateTime When;

    /// <summary>Initialises an unclean shutdown wrapper from an event record.</summary>
    public OSUncleanShutdown(EventObject eventObject) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = "OSUncleanShutdown";
        Computer = SourceEvent.ComputerName;
        Action = "System Dirty Reboot";
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
