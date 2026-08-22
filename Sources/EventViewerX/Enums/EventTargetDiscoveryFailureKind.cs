namespace EventViewerX;

/// <summary>Stable classification for one target-discovery failure.</summary>
public enum EventTargetDiscoveryFailureKind {
    /// <summary>The local computer is not joined to an Active Directory domain.</summary>
    NotDomainJoined,
    /// <summary>The caller cannot read the requested topology.</summary>
    AccessDenied,
    /// <summary>The requested domain or forest name is invalid or unavailable.</summary>
    NotFound,
    /// <summary>The bounded discovery deadline was reached.</summary>
    Timeout,
    /// <summary>A declared domain or target limit was reached.</summary>
    LimitReached,
    /// <summary>Another directory or platform error occurred.</summary>
    Error
}
