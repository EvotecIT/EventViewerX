namespace EventViewerX.Rules.Windows;

/// <summary>Windows measured shutdown duration (200).</summary>
public sealed class ShutdownPerformance : WorkstationHealthEvent {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 200 };
    /// <inheritdoc />
    public override string LogName => "Microsoft-Windows-Diagnostics-Performance/Operational";
    /// <inheritdoc />
    public override IReadOnlyList<string> ProviderNames => new[] { "Microsoft-Windows-Diagnostics-Performance" };
    /// <inheritdoc />
    public override EventType Type => EventType.ShutdownPerformance;

    /// <summary>Total shutdown duration in milliseconds.</summary>
    public long? DurationMilliseconds;
    /// <summary>Time spent shutting down services, in milliseconds.</summary>
    public long? ServicesMilliseconds;
    /// <summary>Provider degradation flag; unknown when absent.</summary>
    public bool? IsDegradation;
    /// <summary>Shutdown start time in UTC, when supplied.</summary>
    public DateTime? IncidentTimestampUtc;

    /// <summary>Projects the provider's payload without relying on a localized message.</summary>
    public ShutdownPerformance(EventObject source) : base(source, nameof(ShutdownPerformance)) {
        DurationMilliseconds = Number("ShutdownTime");
        ServicesMilliseconds = Number("ShutdownServicesTime");
        IsDegradation = Boolean("ShutdownIsDegradation");
        IncidentTimestampUtc = RuleHelpers.ParseDateTimeLoose(Value("ShutdownStartTime"));
    }
}
