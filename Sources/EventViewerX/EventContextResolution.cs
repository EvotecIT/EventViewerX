namespace EventViewerX;

/// <summary>Deterministic context resolved from facts visible at a requested event time.</summary>
public sealed class EventContextResolution {
    /// <summary>Resolved object family.</summary>
    public EventContextObjectKind ObjectKind { get; internal set; }

    /// <summary>Resolved canonical identity.</summary>
    public string? CanonicalId { get; internal set; }

    /// <summary>State at the requested time.</summary>
    public EventContextState State { get; internal set; }

    /// <summary>Best known display name at the requested event time.</summary>
    public string? NameAtEventTime { get; internal set; }

    /// <summary>Last non-conflicting name known at or before the requested event time.</summary>
    public string? LastKnownName { get; internal set; }

    /// <summary>Current name only when the latest stored state is live and unambiguous.</summary>
    public string? CurrentName { get; internal set; }

    /// <summary>Distinguished name effective at the requested time.</summary>
    public string? DistinguishedName { get; internal set; }

    /// <summary>Directory domain associated with the resolved object.</summary>
    public string? Domain { get; internal set; }

    /// <summary>Provenance of the decisive fact, when available.</summary>
    public EventContextProvenance? Provenance { get; internal set; }

    /// <summary>Explanation of unknown or ambiguous results.</summary>
    public string? Reason { get; internal set; }
}
