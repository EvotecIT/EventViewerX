namespace EventViewerX;

/// <summary>Detached explanation of a compiled detection plan.</summary>
public sealed class EventDetectionPlanExplanation {
    internal EventDetectionPlanExplanation(
        string planHash,
        IReadOnlyList<EventDetectionRulePlanExplanation> rules,
        IReadOnlyList<EventType> requiredEventTypes) {

        PlanHash = planHash;
        Rules = Array.AsReadOnly(rules.ToArray());
        RequiredEventTypes = Array.AsReadOnly(requiredEventTypes.ToArray());
    }

    /// <summary>SHA-256 identity of the effective tuned plan.</summary>
    public string PlanHash { get; }
    /// <summary>Rules in deterministic evaluation order.</summary>
    public IReadOnlyList<EventDetectionRulePlanExplanation> Rules { get; }
    /// <summary>Typed event projections required before evaluation.</summary>
    public IReadOnlyList<EventType> RequiredEventTypes { get; }
    /// <summary>Number of enabled rules.</summary>
    public int RuleCount => Rules.Count;
    /// <summary>Number of rules that retain bounded state.</summary>
    public int StatefulRuleCount => Rules.Count(static rule => rule.Kind != EventDetectionRuleKind.Stateless);
}

/// <summary>Selectors and state semantics for one compiled detection rule.</summary>
public sealed class EventDetectionRulePlanExplanation {
    internal EventDetectionRulePlanExplanation(
        string ruleId,
        string title,
        EventDetectionRuleKind kind,
        IReadOnlyList<EventType> eventTypes,
        IReadOnlyList<int> eventIds,
        IReadOnlyList<string> channels,
        IReadOnlyList<string> providers,
        TimeSpan window,
        int threshold,
        string? groupBy,
        string? distinctBy,
        IReadOnlyList<string> steps) {

        RuleId = ruleId;
        Title = title;
        Kind = kind;
        EventTypes = Array.AsReadOnly(eventTypes.ToArray());
        EventIds = Array.AsReadOnly(eventIds.ToArray());
        Channels = Array.AsReadOnly(channels.ToArray());
        Providers = Array.AsReadOnly(providers.ToArray());
        Window = window;
        Threshold = threshold;
        GroupBy = groupBy;
        DistinctBy = distinctBy;
        Steps = Array.AsReadOnly(steps.ToArray());
    }

    /// <summary>Stable rule ID.</summary>
    public string RuleId { get; }
    /// <summary>Operator-facing title.</summary>
    public string Title { get; }
    /// <summary>Evaluation behavior.</summary>
    public EventDetectionRuleKind Kind { get; }
    /// <summary>Typed event selectors.</summary>
    public IReadOnlyList<EventType> EventTypes { get; }
    /// <summary>Native event-ID selectors.</summary>
    public IReadOnlyList<int> EventIds { get; }
    /// <summary>Channel selectors.</summary>
    public IReadOnlyList<string> Channels { get; }
    /// <summary>Provider selectors.</summary>
    public IReadOnlyList<string> Providers { get; }
    /// <summary>State window.</summary>
    public TimeSpan Window { get; }
    /// <summary>Count or distinct-value threshold.</summary>
    public int Threshold { get; }
    /// <summary>State partition field.</summary>
    public string? GroupBy { get; }
    /// <summary>Distinct-value field.</summary>
    public string? DistinctBy { get; }
    /// <summary>Temporal steps in declared order.</summary>
    public IReadOnlyList<string> Steps { get; }
}
