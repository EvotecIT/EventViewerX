namespace EventViewerX;

/// <summary>Reusable positive, negative, boundary, or known-false-positive detection fixture.</summary>
public sealed class EventDetectionFixture {
    /// <summary>Fixture name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Ordered observations supplied to the engine.</summary>
    public IReadOnlyList<EventObservation> Observations { get; set; } = Array.Empty<EventObservation>();
    /// <summary>Expected matched rule IDs, including duplicates when repeated findings are expected.</summary>
    public IReadOnlyList<string> ExpectedRuleIds { get; set; } = Array.Empty<string>();
}

/// <summary>Comparison of actual fixture findings with declared expectations.</summary>
public sealed class EventDetectionFixtureResult {
    internal EventDetectionFixtureResult(
        string name,
        EventDetectionExecutionResult execution,
        IReadOnlyList<string> expectedRuleIds) {

        Name = name;
        Execution = execution;
        ExpectedRuleIds = Array.AsReadOnly(expectedRuleIds.ToArray());
        ActualRuleIds = Array.AsReadOnly(execution.Findings
            .Where(static finding => finding.Status == EventDetectionFindingStatus.Matched)
            .Select(static finding => finding.RuleId)
            .ToArray());
        IsMatch = ExpectedRuleIds.SequenceEqual(ActualRuleIds, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Fixture name.</summary>
    public string Name { get; }
    /// <summary>Full execution result.</summary>
    public EventDetectionExecutionResult Execution { get; }
    /// <summary>Expected matched rule IDs.</summary>
    public IReadOnlyList<string> ExpectedRuleIds { get; }
    /// <summary>Actual matched rule IDs.</summary>
    public IReadOnlyList<string> ActualRuleIds { get; }
    /// <summary>Whether actual findings exactly matched declared order and multiplicity.</summary>
    public bool IsMatch { get; }
}
