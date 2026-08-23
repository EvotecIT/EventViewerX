namespace EventViewerX.Reporting;

/// <summary>Derived logical occurrence retaining every contributing source observation.</summary>
public sealed class EventOccurrenceGroup {
    internal EventOccurrenceGroup(
        string identity,
        EventReportRow representative,
        IReadOnlyList<EventReportRow> observations,
        string policyName,
        int policyVersion,
        string matchReason) {

        Identity = identity;
        Representative = representative;
        Observations = observations;
        PolicyName = policyName;
        PolicyVersion = policyVersion;
        MatchReason = matchReason;
    }

    /// <summary>Stable derived identity for this policy version and observation set.</summary>
    public string Identity { get; }

    /// <summary>Deterministically selected richest observation.</summary>
    public EventReportRow Representative { get; }

    /// <summary>Every immutable source observation in deterministic order.</summary>
    public IReadOnlyList<EventReportRow> Observations { get; }

    /// <summary>Compiled policy that established membership.</summary>
    public string PolicyName { get; }

    /// <summary>Policy contract version.</summary>
    public int PolicyVersion { get; }

    /// <summary>Evidence used to establish membership.</summary>
    public string MatchReason { get; }

    /// <summary>Number of retained observations.</summary>
    public int ObservationCount => Observations.Count;

    /// <summary>Distinct event-source computers represented by the group.</summary>
    public IReadOnlyList<string> SourceComputers => Observations
        .Select(static observation => observation.SourceComputer)
        .Where(static computer => !string.IsNullOrWhiteSpace(computer))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(static computer => computer, StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
