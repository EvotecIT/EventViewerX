namespace EventViewerX.Rules.Windows;

/// <summary>Windows attributed shutdown delay to a service (203).</summary>
public sealed class ShutdownDegradation : WorkstationHealthEvent {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 203 };
    /// <inheritdoc />
    public override string LogName => "Microsoft-Windows-Diagnostics-Performance/Operational";
    /// <inheritdoc />
    public override IReadOnlyList<string> ProviderNames => new[] { "Microsoft-Windows-Diagnostics-Performance" };
    /// <inheritdoc />
    public override EventType Type => EventType.ShutdownDegradation;

    /// <summary>Shutdown component category identified by the event ID.</summary>
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
    public ShutdownDegradation(EventObject source) : base(source, nameof(ShutdownDegradation)) {
        Component = "Service";
        Name = Value("Name");
        FriendlyName = Value("FriendlyName");
        Version = Value("Version");
        Path = Value("Path");
        DurationMilliseconds = Number("TotalTime");
        DegradationMilliseconds = Number("DegradationTime");
        IncidentTimestampUtc = RuleHelpers.ParseDateTimeLoose(Value("StartTime"));
    }
}
