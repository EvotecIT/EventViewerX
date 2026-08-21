namespace EventViewerX;

/// <summary>Explains where one predicate node is evaluated and why.</summary>
public sealed class EventPredicatePlanStep {
    internal EventPredicatePlanStep(
        string expression,
        EventPredicatePlanStage stage,
        string reason) {

        Expression = expression;
        Stage = stage;
        Reason = reason;
    }

    /// <summary>Compact predicate expression.</summary>
    public string Expression { get; }

    /// <summary>Selected execution stage.</summary>
    public EventPredicatePlanStage Stage { get; }

    /// <summary>Reason the stage was selected.</summary>
    public string Reason { get; }
}
