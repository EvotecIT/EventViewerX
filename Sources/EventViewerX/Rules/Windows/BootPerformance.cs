namespace EventViewerX.Rules.Windows;

/// <summary>Windows measured boot duration and its main path (100).</summary>
public sealed class BootPerformance : WorkstationHealthEvent {
    /// <inheritdoc />
    public override List<int> EventIds => new() { 100 };
    /// <inheritdoc />
    public override string LogName => "Microsoft-Windows-Diagnostics-Performance/Operational";
    /// <inheritdoc />
    public override IReadOnlyList<string> ProviderNames => new[] { "Microsoft-Windows-Diagnostics-Performance" };
    /// <inheritdoc />
    public override EventType Type => EventType.BootPerformance;

    /// <summary>Total boot duration in milliseconds.</summary>
    public long? DurationMilliseconds;
    /// <summary>Main boot-path duration in milliseconds.</summary>
    public long? MainPathMilliseconds;
    /// <summary>Post-boot duration in milliseconds.</summary>
    public long? PostBootMilliseconds;
    /// <summary>Provider degradation flag; unknown when absent.</summary>
    public bool? IsDegradation;
    /// <summary>Boot start time in UTC, when supplied.</summary>
    public DateTime? IncidentTimestampUtc;

    /// <summary>Projects the provider's payload without relying on a localized message.</summary>
    public BootPerformance(EventObject source) : base(source, nameof(BootPerformance)) {
        DurationMilliseconds = Number("BootTime");
        MainPathMilliseconds = Number("MainPathBootTime");
        PostBootMilliseconds = Number("BootPostBootTime");
        IsDegradation = Boolean("BootIsDegradation");
        IncidentTimestampUtc = RuleHelpers.ParseDateTimeLoose(Value("BootStartTime"));
    }
}
