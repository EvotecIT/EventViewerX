namespace EventViewerX.Reporting;

/// <summary>Compiled, versioned owner for one semantic occurrence identity.</summary>
public interface IEventOccurrencePolicy {
    /// <summary>Stable policy name.</summary>
    string Name { get; }

    /// <summary>Policy contract version.</summary>
    int Version { get; }

    /// <summary>Returns a causal identity only when the record carries policy-owned evidence.</summary>
    bool TryGetIdentity(EventReportRow observation, out string identity, out string reason);
}
