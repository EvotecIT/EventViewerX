namespace EventViewerX.Rules.Windows;

/// <summary>Common projected fields for Microsoft Defender operational events.</summary>
public abstract class DefenderSecurityEvent : EventRuleBase {
    /// <summary>Creates a Microsoft Defender event projection.</summary>
    protected DefenderSecurityEvent(EventObject eventObject, string typeName) : base(eventObject) {
        SourceEvent = eventObject;
        TypeName = typeName;
        Computer = SourceEvent.ComputerName;
        ThreatName = FirstValue("Threat Name", "ThreatName");
        ThreatId = FirstValue("Threat ID", "ThreatID");
        Path = FirstValue("Path", "Resources");
        User = FirstValue("User", "Detection User");
        Configuration = FirstValue("Configuration", "Feature Name");
        OldValue = FirstValue("Old Value", "OldValue");
        NewValue = FirstValue("New Value", "NewValue");
        When = SourceEvent.TimeCreated;
    }
    /// <inheritdoc />
    public override string LogName => "Microsoft-Windows-Windows Defender/Operational";
    /// <summary>Accepts Windows Defender provider events.</summary>
    public override bool CanHandle(EventObject eventObject) => RuleHelpers.IsProvider(
        eventObject,
        "Microsoft-Windows-Windows Defender");
    /// <summary>Computer that emitted the event.</summary>
    public string Computer = string.Empty;
    /// <summary>Detected threat name.</summary>
    public string ThreatName = string.Empty;
    /// <summary>Detected threat identifier.</summary>
    public string ThreatId = string.Empty;
    /// <summary>Affected path or resource.</summary>
    public string Path = string.Empty;
    /// <summary>Affected user.</summary>
    public string User = string.Empty;
    /// <summary>Changed Defender setting.</summary>
    public string Configuration = string.Empty;
    /// <summary>Previous setting value.</summary>
    public string OldValue = string.Empty;
    /// <summary>New setting value.</summary>
    public string NewValue = string.Empty;
    /// <summary>Event timestamp.</summary>
    public DateTime When;

    private string FirstValue(string first, string second) {
        string value = SourceEvent.GetValueFromDataDictionary(first);
        return string.IsNullOrWhiteSpace(value)
            ? SourceEvent.GetValueFromDataDictionary(second)
            : value;
    }
}

/// <summary>Microsoft Defender detected malware or potentially unwanted software (1116).</summary>
public sealed class DefenderThreatDetected : DefenderSecurityEvent {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 1116 };
    /// <inheritdoc />
    public override EventType Type => EventType.DefenderThreatDetected;
    /// <summary>Creates the typed projection.</summary>
    public DefenderThreatDetected(EventObject eventObject) : base(eventObject, nameof(DefenderThreatDetected)) { }
}

/// <summary>Microsoft Defender took action on a detected threat (1117).</summary>
public sealed class DefenderThreatAction : DefenderSecurityEvent {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 1117 };
    /// <inheritdoc />
    public override EventType Type => EventType.DefenderThreatAction;
    /// <summary>Creates the typed projection.</summary>
    public DefenderThreatAction(EventObject eventObject) : base(eventObject, nameof(DefenderThreatAction)) { }
}

/// <summary>Microsoft Defender configuration changed (5007).</summary>
public sealed class DefenderConfigurationChanged : DefenderSecurityEvent {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 5007 };
    /// <inheritdoc />
    public override EventType Type => EventType.DefenderConfigurationChanged;
    /// <summary>Creates the typed projection.</summary>
    public DefenderConfigurationChanged(EventObject eventObject) : base(eventObject, nameof(DefenderConfigurationChanged)) { }
}
