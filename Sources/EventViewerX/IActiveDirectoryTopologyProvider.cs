namespace EventViewerX;

internal interface IActiveDirectoryTopologyProvider {
    ActiveDirectoryTopologySnapshot Discover(
        EventTargetDiscoveryRequest request,
        CancellationToken cancellationToken,
        Action<EventTargetDomainResult> domainCompleted,
        Action<EventTargetDiscoveryFailure> failureReported);
}
