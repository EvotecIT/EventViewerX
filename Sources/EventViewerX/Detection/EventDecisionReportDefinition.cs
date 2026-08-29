namespace EventViewerX;

/// <summary>Discoverable scope and data requirements for one decision report profile.</summary>
public sealed class EventDecisionReportDefinition {
    internal EventDecisionReportDefinition(
        EventDecisionReportKind kind,
        string title,
        string description,
        IReadOnlyList<EventType> eventTypes,
        IReadOnlyList<string> packIds,
        IReadOnlyList<string> tags,
        bool includeAllObservations,
        bool includeAllFindings,
        bool includeAllPacks) {

        Kind = kind;
        Title = title;
        Description = description;
        EventTypes = Array.AsReadOnly(eventTypes.Distinct().ToArray());
        PackIds = Array.AsReadOnly(packIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        Tags = Array.AsReadOnly(tags.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        IncludeAllObservations = includeAllObservations;
        IncludeAllFindings = includeAllFindings;
        IncludeAllPacks = includeAllPacks;
    }

    /// <summary>Stable report kind.</summary>
    public EventDecisionReportKind Kind { get; }
    /// <summary>Operator-facing title.</summary>
    public string Title { get; }
    /// <summary>Decision the report is designed to support.</summary>
    public string Description { get; }
    /// <summary>Typed event contracts directly relevant to the report.</summary>
    public IReadOnlyList<EventType> EventTypes { get; }
    /// <summary>Built-in pack IDs directly relevant to the report.</summary>
    public IReadOnlyList<string> PackIds { get; }
    /// <summary>Detection tags used to select additional content.</summary>
    public IReadOnlyList<string> Tags { get; }
    internal bool IncludeAllObservations { get; }
    internal bool IncludeAllFindings { get; }
    internal bool IncludeAllPacks { get; }
}
