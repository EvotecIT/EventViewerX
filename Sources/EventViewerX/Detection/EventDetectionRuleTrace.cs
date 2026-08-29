namespace EventViewerX;

/// <summary>Explainable per-observation rule decision without mutating correlation state.</summary>
public sealed class EventDetectionRuleTrace {
    internal EventDetectionRuleTrace(
        string ruleId,
        string title,
        EventDetectionRuleKind kind,
        bool accepted,
        bool suppressed,
        string outcome,
        IReadOnlyList<string> matchingSteps,
        IReadOnlyList<EventDetectionConditionResult> conditions) {

        RuleId = ruleId;
        Title = title;
        Kind = kind;
        Accepted = accepted;
        Suppressed = suppressed;
        Outcome = outcome;
        MatchingSteps = Array.AsReadOnly(matchingSteps.ToArray());
        Conditions = Array.AsReadOnly(conditions.ToArray());
    }

    /// <summary>Stable rule ID.</summary>
    public string RuleId { get; }
    /// <summary>Operator-facing rule title.</summary>
    public string Title { get; }
    /// <summary>Rule state behavior.</summary>
    public EventDetectionRuleKind Kind { get; }
    /// <summary>Whether selectors and the semantic predicate accepted this observation.</summary>
    public bool Accepted { get; }
    /// <summary>Whether environment tuning suppressed an otherwise accepted observation.</summary>
    public bool Suppressed { get; }
    /// <summary>Matched, rejected condition, suppressed, awaiting state, or unavailable evidence explanation.</summary>
    public string Outcome { get; }
    /// <summary>Temporal steps matched by this observation.</summary>
    public IReadOnlyList<string> MatchingSteps { get; }
    /// <summary>Every selector, predicate, suppression, and coverage decision.</summary>
    public IReadOnlyList<EventDetectionConditionResult> Conditions { get; }
}
