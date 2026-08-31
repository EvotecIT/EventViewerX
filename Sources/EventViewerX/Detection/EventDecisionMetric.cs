namespace EventViewerX;

/// <summary>Stable numeric decision-report metric suitable for C#, PowerShell, CLI, and renderers.</summary>
public sealed class EventDecisionMetric {
    internal EventDecisionMetric(
        string name,
        string displayName,
        double value,
        string unit,
        string description) {

        Name = name;
        DisplayName = displayName;
        Value = value;
        Unit = unit;
        Description = description;
    }

    /// <summary>Stable machine-readable metric name.</summary>
    public string Name { get; }
    /// <summary>Operator-facing metric label.</summary>
    public string DisplayName { get; }
    /// <summary>Invariant numeric value.</summary>
    public double Value { get; }
    /// <summary>Unit such as count or seconds.</summary>
    public string Unit { get; }
    /// <summary>Meaning and decision context.</summary>
    public string Description { get; }
}
