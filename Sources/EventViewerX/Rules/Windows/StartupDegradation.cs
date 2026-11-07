namespace EventViewerX.Rules.Windows;

/// <summary>Windows attributed startup degradation to a component or startup phase (101–110).</summary>
public sealed class StartupDegradation : WorkstationHealthEvent {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 101, 102, 103, 104, 105, 106, 107, 108, 109, 110 };
    /// <inheritdoc />
    public override string LogName => "Microsoft-Windows-Diagnostics-Performance/Operational";
    /// <inheritdoc />
    public override IReadOnlyList<string> ProviderNames => new[] { "Microsoft-Windows-Diagnostics-Performance" };
    /// <inheritdoc />
    public override EventType Type => EventType.StartupDegradation;

    /// <summary>Startup component category identified by the event ID.</summary>
    public string Component;
    /// <summary>Application, driver, service, device or startup phase named by the provider.</summary>
    public string Name;
    /// <summary>Friendly component name, when supplied.</summary>
    public string FriendlyName;
    /// <summary>Component version, when supplied.</summary>
    public string Version;
    /// <summary>Component path, when supplied.</summary>
    public string Path;
    /// <summary>Total component duration in milliseconds.</summary>
    public long? DurationMilliseconds;
    /// <summary>Additional time attributed to the component, in milliseconds.</summary>
    public long? DegradationMilliseconds;
    /// <summary>Incident start time in UTC, when supplied.</summary>
    public DateTime? IncidentTimestampUtc;

    /// <summary>Projects the provider's payload without relying on a localized message.</summary>
    public StartupDegradation(EventObject source) : base(source, nameof(StartupDegradation)) {
        Component = source.Id switch { 101 => "Application", 102 => "Driver", 103 => "Service", 104 => "CoreSystem", 105 => "ForegroundPrefetch", 106 => "BackgroundPrefetch", 107 => "MachinePolicy", 108 => "UserPolicy", 109 => "Device", _ => "SessionManager" };
        Name = Value("Name");
        FriendlyName = Value("FriendlyName");
        Version = Value("Version");
        Path = Value("Path");
        DurationMilliseconds = Number("TotalTime");
        DegradationMilliseconds = Number("DegradationTime");
        IncidentTimestampUtc = RuleHelpers.ParseDateTimeLoose(Value("StartTime"));
    }
}
