namespace EventViewerX.Reporting;

/// <summary>Bounds and policy selection for derived occurrence grouping.</summary>
public sealed class EventOccurrenceOptions {
    /// <summary>Duplicate grouping level.</summary>
    public EventDuplicateMode Mode { get; set; } = EventDuplicateMode.Semantic;

    /// <summary>Maximum separation between observations with the same causal discriminator.</summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Maximum source observations accepted by one managed grouping operation.</summary>
    public int MaximumObservations { get; set; } = 100000;

    /// <summary>Maximum derived occurrence groups.</summary>
    public int MaximumGroups { get; set; } = 25000;
}
