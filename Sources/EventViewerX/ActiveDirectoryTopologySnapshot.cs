namespace EventViewerX;

internal sealed class ActiveDirectoryTopologySnapshot {
    internal ActiveDirectoryTopologySnapshot(
        IReadOnlyList<EventTargetDomainResult> domains,
        IReadOnlyList<EventTargetDiscoveryFailure> failures) {

        Domains = domains;
        Failures = failures;
    }

    internal IReadOnlyList<EventTargetDomainResult> Domains { get; }
    internal IReadOnlyList<EventTargetDiscoveryFailure> Failures { get; }
}
