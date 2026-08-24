namespace EventViewerX.Reporting;

/// <summary>Bounded occurrence-grouping result with an explicit completeness contract.</summary>
public sealed class EventOccurrenceResult {
    internal EventOccurrenceResult(
        IReadOnlyList<EventOccurrenceGroup> groups,
        bool isComplete,
        string? diagnostic,
        long observationsEvaluated) {

        Groups = groups;
        IsComplete = isComplete;
        Diagnostic = diagnostic;
        ObservationsEvaluated = observationsEvaluated;
    }

    /// <summary>Derived occurrence groups, empty when a safety bound is exceeded.</summary>
    public IReadOnlyList<EventOccurrenceGroup> Groups { get; }

    /// <summary>Whether every source observation was available and grouped within all configured bounds.</summary>
    public bool IsComplete { get; }

    /// <summary>Reason an incomplete result was withheld.</summary>
    public string? Diagnostic { get; }

    /// <summary>
    /// Number of source observations evaluated, including the single proof observation that establishes
    /// a <see cref="EventOccurrenceOptions.MaximumObservations"/> overflow.
    /// </summary>
    public long ObservationsEvaluated { get; }
}
