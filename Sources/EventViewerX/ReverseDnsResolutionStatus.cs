namespace EventViewerX;

/// <summary>
/// Describes whether reverse-DNS enrichment was requested and what it produced.
/// </summary>
public enum ReverseDnsResolutionStatus {
    /// <summary>No reverse-DNS lookup was requested.</summary>
    NotRequested,

    /// <summary>The client value was already a DNS host name and required no PTR lookup.</summary>
    AlreadyNamed,

    /// <summary>One or more PTR names were resolved.</summary>
    Resolved,

    /// <summary>The resolver returned no PTR records for the address.</summary>
    NoRecord,

    /// <summary>The event field was neither an IP address nor a valid DNS host name.</summary>
    InvalidAddress,

    /// <summary>The reverse-DNS lookup exceeded its configured timeout.</summary>
    TimedOut,

    /// <summary>The caller cancelled the reverse-DNS lookup.</summary>
    Cancelled,

    /// <summary>The resolver failed for another reason.</summary>
    Failed
}
