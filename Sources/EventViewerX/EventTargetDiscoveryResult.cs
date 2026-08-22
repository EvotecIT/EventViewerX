namespace EventViewerX;

/// <summary>Complete result of one explicit target-discovery request.</summary>
public sealed class EventTargetDiscoveryResult {
    internal EventTargetDiscoveryResult(
        EventTargetDiscoveryScope scope,
        string? requestedName,
        IReadOnlyList<EventTargetInfo> targets,
        IReadOnlyList<EventTargetDomainResult> domains,
        IReadOnlyList<EventTargetDiscoveryFailure> failures,
        string fingerprint,
        TimeSpan duration) {

        Scope = scope;
        RequestedName = requestedName;
        Targets = targets;
        Domains = domains;
        Failures = failures;
        Fingerprint = fingerprint;
        Duration = duration;
    }

    /// <summary>Explicit scope that was requested.</summary>
    public EventTargetDiscoveryScope Scope { get; }
    /// <summary>Named domain or forest supplied by the caller.</summary>
    public string? RequestedName { get; }
    /// <summary>Distinct normalized query targets.</summary>
    public IReadOnlyList<EventTargetInfo> Targets { get; }
    /// <summary>Per-domain successes and failures.</summary>
    public IReadOnlyList<EventTargetDomainResult> Domains { get; }
    /// <summary>Forest, trust, timeout, or other failures not hidden by successful domains.</summary>
    public IReadOnlyList<EventTargetDiscoveryFailure> Failures { get; }
    /// <summary>Stable SHA-256 fingerprint of scope and resolved target identities.</summary>
    public string Fingerprint { get; }
    /// <summary>Total elapsed discovery time.</summary>
    public TimeSpan Duration { get; }
    /// <summary>True only when no global or per-domain failure occurred.</summary>
    public bool IsComplete => Failures.Count == 0 && Domains.All(static domain => domain.Succeeded);
    /// <summary>True when a declared domain or target limit stopped discovery.</summary>
    public bool IsTruncated => Failures.Any(static failure => failure.Kind == EventTargetDiscoveryFailureKind.LimitReached) ||
        Domains.Any(static domain => domain.Failures.Any(static failure => failure.Kind == EventTargetDiscoveryFailureKind.LimitReached));
}
