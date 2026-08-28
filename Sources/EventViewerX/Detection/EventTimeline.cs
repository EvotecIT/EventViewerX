namespace EventViewerX;

/// <summary>Options controlling evidence inclusion and pivot filtering.</summary>
public sealed class EventTimelineOptions {
    /// <summary>Whether source observations are included.</summary>
    public bool IncludeObservations { get; set; } = true;
    /// <summary>Whether detection findings are included.</summary>
    public bool IncludeFindings { get; set; } = true;
    /// <summary>Optional canonical pivot category.</summary>
    public EventPivotKind? PivotKind { get; set; }
    /// <summary>Optional case-insensitive exact pivot value.</summary>
    public string? PivotValue { get; set; }
}

/// <summary>Ordered observations and findings with reproducible pivot filtering.</summary>
public sealed class EventTimeline {
    internal EventTimeline(
        IReadOnlyList<EventTimelineEntry> entries,
        EventPivotKind? pivotKind,
        string? pivotValue) {

        Entries = Array.AsReadOnly(entries.ToArray());
        PivotKind = pivotKind;
        PivotValue = pivotValue;
        StartTimeUtc = Entries.Count == 0 ? null : Entries.Min(static entry => entry.EventTimeUtc);
        EndTimeUtc = Entries.Count == 0 ? null : Entries.Max(static entry => entry.EventTimeUtc);
    }

    /// <summary>Entries ordered by source event time and stable identity.</summary>
    public IReadOnlyList<EventTimelineEntry> Entries { get; }
    /// <summary>Applied pivot category.</summary>
    public EventPivotKind? PivotKind { get; }
    /// <summary>Applied pivot value.</summary>
    public string? PivotValue { get; }
    /// <summary>Earliest represented source time.</summary>
    public DateTime? StartTimeUtc { get; }
    /// <summary>Latest represented source time.</summary>
    public DateTime? EndTimeUtc { get; }
}
