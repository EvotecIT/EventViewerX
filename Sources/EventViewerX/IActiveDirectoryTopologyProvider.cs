namespace EventViewerX;

internal interface IActiveDirectoryTopologyProvider {
    ActiveDirectoryTopologySnapshot Discover(EventTargetDiscoveryRequest request);
}
