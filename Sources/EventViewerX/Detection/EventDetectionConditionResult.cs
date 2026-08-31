namespace EventViewerX;

/// <summary>One explain-plan condition evaluated for a rule and observation.</summary>
public sealed class EventDetectionConditionResult {
    internal EventDetectionConditionResult(string condition, bool satisfied, string detail) {
        Condition = condition;
        Satisfied = satisfied;
        Detail = detail;
    }

    /// <summary>Stable condition name.</summary>
    public string Condition { get; }
    /// <summary>Whether this condition accepted the observation.</summary>
    public bool Satisfied { get; }
    /// <summary>Expected and observed values or operator-facing state.</summary>
    public string Detail { get; }
}
