namespace EventViewerX.Reporting;

/// <summary>Bounded occurrence-grouping result with an explicit completeness contract.</summary>
public sealed class EventOccurrenceResult {
    internal EventOccurrenceResult(
        IReadOnlyList<EventOccurrenceGroup> groups,
        bool isComplete,
        string? diagnostic) {

        Groups = groups;
        IsComplete = isComplete;
        Diagnostic = diagnostic;
    }

    /// <summary>Derived occurrence groups, empty when a safety bound is exceeded.</summary>
    public IReadOnlyList<EventOccurrenceGroup> Groups { get; }

    /// <summary>Whether every supplied observation was grouped within all configured bounds.</summary>
    public bool IsComplete { get; }

    /// <summary>Reason an incomplete result was withheld.</summary>
    public string? Diagnostic { get; }
}
