using System.Diagnostics.Eventing.Reader;
using System.Net;

namespace EventViewerX;

/// <summary>Selected workflow and bounded evidence options for readiness assessment.</summary>
public sealed class EventReadinessRequest {
    /// <summary>Explicit leaf or composite event types.</summary>
    public IReadOnlyList<EventType> Types { get; set; } = Array.Empty<EventType>();
    /// <summary>Optional built-in scenario resolved to event types.</summary>
    public EventReadinessScenario Scenario { get; set; }
    /// <summary>Local/direct targets or, with a collector, the explicit expected source set.</summary>
    public EventTargetDiscoveryRequest TargetDiscovery { get; set; } = new();
    /// <summary>Optional Windows Event Collector used for event-log transport.</summary>
    public string? Collector { get; set; }
    /// <summary>Optional WEC subscription inspected for configuration and runtime coverage.</summary>
    public string? SubscriptionName { get; set; }
    /// <summary>Additional expected WEC sources compared with runtime source status.</summary>
    public IReadOnlyList<string> ExpectedSources { get; set; } = Array.Empty<string>();
    /// <summary>Optional credential used by remote Event Log probes.</summary>
    public NetworkCredential? EventLogCredential { get; set; }
    /// <summary>Authentication package used by remote Event Log probes.</summary>
    public EventLogAuthentication Authentication { get; set; }
    /// <summary>Budget for each native Event Log probe.</summary>
    public TimeSpan ProbeTimeout { get; set; } = TimeSpan.FromSeconds(15);
    /// <summary>Maximum records inspected by each probe.</summary>
    public int MaxEventsToScan { get; set; } = 4096;

    internal EventReadinessRequest Snapshot() {
        EventType[] selected = Scenario == EventReadinessScenario.None
            ? Types.Distinct().ToArray()
            : EventReadinessScenarioCatalog.GetTypes(Scenario).Distinct().ToArray();
        if (selected.Length == 0) {
            throw new ArgumentException("At least one event type or readiness scenario is required.", nameof(Types));
        }
        if (Scenario != EventReadinessScenario.None && Types.Count > 0) {
            throw new ArgumentException("Specify either Types or Scenario, not both.", nameof(Scenario));
        }
        if (TargetDiscovery == null) {
            throw new ArgumentNullException(nameof(TargetDiscovery));
        }
        EventTargetDiscoveryRequest discovery = TargetDiscovery.Snapshot();
        string? collector = string.IsNullOrWhiteSpace(Collector) ? null : Collector!.Trim();
        string? subscriptionName = string.IsNullOrWhiteSpace(SubscriptionName) ? null : SubscriptionName!.Trim();
        if (subscriptionName != null && collector == null) {
            throw new ArgumentException("SubscriptionName requires Collector.", nameof(SubscriptionName));
        }
        string[] expectedSources = ExpectedSources
            .Where(static source => !string.IsNullOrWhiteSpace(source))
            .Select(static source => NormalizeExpectedSource(source))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (expectedSources.Length > 0 && subscriptionName == null) {
            throw new ArgumentException("ExpectedSources requires SubscriptionName.", nameof(ExpectedSources));
        }
        if (ProbeTimeout <= TimeSpan.Zero || ProbeTimeout.TotalMilliseconds > int.MaxValue) {
            throw new ArgumentOutOfRangeException(nameof(ProbeTimeout));
        }
        if (MaxEventsToScan <= 0) {
            throw new ArgumentOutOfRangeException(nameof(MaxEventsToScan));
        }
        return new EventReadinessRequest {
            Types = selected,
            Scenario = Scenario,
            TargetDiscovery = discovery,
            Collector = collector,
            SubscriptionName = subscriptionName,
            ExpectedSources = expectedSources,
            EventLogCredential = EventLogCredential == null
                ? null
                : new NetworkCredential(EventLogCredential.UserName, EventLogCredential.Password, EventLogCredential.Domain),
            Authentication = Authentication,
            ProbeTimeout = ProbeTimeout,
            MaxEventsToScan = MaxEventsToScan
        };
    }

    private static string NormalizeExpectedSource(string source) =>
        EventLogTarget.IsLocalMachine(source)
            ? EventLogTarget.LocalMachineName
            : source.Trim().TrimEnd('.');
}
