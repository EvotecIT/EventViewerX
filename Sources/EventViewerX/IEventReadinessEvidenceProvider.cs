using System.Diagnostics.Eventing.Reader;
using System.Net;

namespace EventViewerX;

internal interface IEventReadinessEvidenceProvider {
    EventTargetDiscoveryResult ResolveTargets(
        EventTargetDiscoveryRequest request,
        CancellationToken cancellationToken);

    EventLogProbeResult Probe(
        string logName,
        string xpath,
        string? machineName,
        TimeSpan timeout,
        int maxEventsToScan,
        NetworkCredential? credential,
        EventLogAuthentication authentication,
        CancellationToken cancellationToken);

    EventLogProbeResult ProbeTypedCollectorSource(
        IReadOnlyList<EventType> types,
        EventSourceDefinition source,
        string collector,
        TimeSpan timeout,
        int maxEventsToScan,
        NetworkCredential? credential,
        EventLogAuthentication authentication,
        CancellationToken cancellationToken);

    IReadOnlyList<EffectiveAuditPolicyResult> QueryAuditPolicy(IReadOnlyList<Guid> subcategoryGuids);

    ChannelPolicy? ReadChannelPolicy(
        string logName,
        string? machineName,
        TimeSpan timeout,
        NetworkCredential? credential,
        EventLogAuthentication authentication);

    EventReadinessConfigurationEvidence ReadLocalConfiguration(string requirementKey);

    CollectorSubscriptionSnapshot? ReadCollectorSubscription(
        string name,
        string? machineName,
        TimeSpan timeout,
        CancellationToken cancellationToken);

    CollectorReadinessStatus ReadLocalCollectorReadiness(CancellationToken cancellationToken);

    CollectorSubscriptionRuntimeStatus ReadLocalCollectorRuntime(
        string subscriptionName,
        CancellationToken cancellationToken);
}
