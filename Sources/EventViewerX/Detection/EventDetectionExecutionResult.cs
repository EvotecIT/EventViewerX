namespace EventViewerX;

/// <summary>Materialized result returned by a bounded dry run or fixture test.</summary>
public sealed class EventDetectionExecutionResult {
    internal EventDetectionExecutionResult(long observationCount, IReadOnlyList<EventDetectionFinding> findings) {
        ObservationCount = observationCount;
        Findings = Array.AsReadOnly(findings.ToArray());
    }

    /// <summary>Number of observations evaluated.</summary>
    public long ObservationCount { get; }
    /// <summary>Matched, incomplete, and error findings.</summary>
    public IReadOnlyList<EventDetectionFinding> Findings { get; }
    /// <summary>Whether evaluation completed without incomplete or error outcomes.</summary>
    public bool IsComplete => Findings.All(static finding => finding.Status == EventDetectionFindingStatus.Matched);
}
