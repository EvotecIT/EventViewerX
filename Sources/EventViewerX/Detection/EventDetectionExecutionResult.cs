namespace EventViewerX;

/// <summary>Materialized result returned by a bounded dry run or fixture test.</summary>
public sealed class EventDetectionExecutionResult {
    internal EventDetectionExecutionResult(
        IReadOnlyList<EventObservation> observations,
        IReadOnlyList<EventDetectionFinding> findings,
        EventDetectionCoverage coverage) {

        Observations = Array.AsReadOnly(observations.ToArray());
        Findings = Array.AsReadOnly(findings.ToArray());
        Coverage = coverage.Snapshot();
    }

    /// <summary>Number of observations evaluated.</summary>
    public long ObservationCount => Observations.Count;
    /// <summary>Canonical observations in deterministic evaluation order.</summary>
    public IReadOnlyList<EventObservation> Observations { get; }
    /// <summary>Matched, incomplete, and error findings.</summary>
    public IReadOnlyList<EventDetectionFinding> Findings { get; }
    /// <summary>Expected and observed data-source coverage for the execution window.</summary>
    public EventDetectionCoverage Coverage { get; }
    /// <summary>Whether rule evaluation completed without incomplete or error outcomes.</summary>
    public bool IsEvaluationComplete => Findings.All(static finding => finding.Status == EventDetectionFindingStatus.Matched);
    /// <summary>Whether both rule evaluation and declared collection coverage are complete.</summary>
    public bool IsComplete => Coverage.IsComplete && IsEvaluationComplete;
}
