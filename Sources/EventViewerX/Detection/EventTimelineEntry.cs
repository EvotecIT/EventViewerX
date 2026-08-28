namespace EventViewerX;

/// <summary>Type of evidence represented by a timeline entry.</summary>
public enum EventTimelineEntryKind {
    /// <summary>Canonical source observation.</summary>
    Observation,
    /// <summary>Detection finding spanning one or more observations.</summary>
    Finding
}

/// <summary>One immutable evidence or finding entry in an explainable timeline.</summary>
public sealed class EventTimelineEntry {
    internal EventTimelineEntry(
        EventTimelineEntryKind kind,
        string identity,
        string title,
        DateTime eventTimeUtc,
        DateTime receivedTimeUtc,
        DateTime processedTimeUtc,
        string ruleId,
        EventDetectionSeverity? severity,
        IReadOnlyList<string> evidenceIdentities,
        IReadOnlyList<EventPivot> pivots) {

        Kind = kind;
        Identity = identity;
        Title = title;
        EventTimeUtc = eventTimeUtc;
        ReceivedTimeUtc = receivedTimeUtc;
        ProcessedTimeUtc = processedTimeUtc;
        RuleId = ruleId;
        Severity = severity;
        EvidenceIdentities = Array.AsReadOnly(evidenceIdentities.ToArray());
        Pivots = Array.AsReadOnly(pivots.ToArray());
    }

    /// <summary>Observation or finding.</summary>
    public EventTimelineEntryKind Kind { get; }
    /// <summary>Stable entry identity.</summary>
    public string Identity { get; }
    /// <summary>Human-readable entry title.</summary>
    public string Title { get; }
    /// <summary>Source event time or beginning of a finding window.</summary>
    public DateTime EventTimeUtc { get; }
    /// <summary>Latest receive time represented by this entry.</summary>
    public DateTime ReceivedTimeUtc { get; }
    /// <summary>Latest processing time represented by this entry.</summary>
    public DateTime ProcessedTimeUtc { get; }
    /// <summary>Detection rule ID for finding entries.</summary>
    public string RuleId { get; }
    /// <summary>Finding severity.</summary>
    public EventDetectionSeverity? Severity { get; }
    /// <summary>Stable source evidence identities.</summary>
    public IReadOnlyList<string> EvidenceIdentities { get; }
    /// <summary>Canonical hunting pivots.</summary>
    public IReadOnlyList<EventPivot> Pivots { get; }
}
