namespace EventViewerX;

/// <summary>Per-domain target discovery outcome.</summary>
public sealed class EventTargetDomainResult {
    /// <summary>Creates one per-domain outcome.</summary>
    public EventTargetDomainResult(
        string domainName,
        string? forestName,
        IReadOnlyList<EventTargetInfo> targets,
        IReadOnlyList<EventTargetDiscoveryFailure> failures) {

        DomainName = domainName;
        ForestName = forestName;
        Targets = targets;
        Failures = failures;
    }

    /// <summary>DNS domain name.</summary>
    public string DomainName { get; }
    /// <summary>DNS forest name when known.</summary>
    public string? ForestName { get; }
    /// <summary>Domain controllers discovered for the domain.</summary>
    public IReadOnlyList<EventTargetInfo> Targets { get; }
    /// <summary>Failures isolated to this domain.</summary>
    public IReadOnlyList<EventTargetDiscoveryFailure> Failures { get; }
    /// <summary>Whether the domain completed without a discovery failure.</summary>
    public bool Succeeded => Failures.Count == 0 && Targets.Count > 0;
}
