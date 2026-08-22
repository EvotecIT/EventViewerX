namespace EventViewerX;

/// <summary>Complete readiness assessment with explicit unknown and partial evidence.</summary>
public sealed class EventReadinessReport {
    internal EventReadinessReport(
        EventReadinessScenario scenario,
        IReadOnlyList<EventType> requestedTypes,
        EventTargetDiscoveryResult? targetDiscovery,
        IReadOnlyList<EventTargetInfo> targets,
        IReadOnlyList<EventReadinessCheckResult> checks,
        TimeSpan duration) {

        Scenario = scenario;
        RequestedTypes = requestedTypes;
        TargetDiscovery = targetDiscovery;
        Targets = targets;
        Checks = checks;
        Duration = duration;
    }

    /// <summary>Selected convenience scenario.</summary>
    public EventReadinessScenario Scenario { get; }
    /// <summary>Effective requested leaf and composite types.</summary>
    public IReadOnlyList<EventType> RequestedTypes { get; }
    /// <summary>Local, collector, or AD target result.</summary>
    public EventTargetDiscoveryResult? TargetDiscovery { get; }
    /// <summary>Effective event-log machines or collector assessed by the report.</summary>
    public IReadOnlyList<EventTargetInfo> Targets { get; }
    /// <summary>Ordered checks from every applicable layer.</summary>
    public IReadOnlyList<EventReadinessCheckResult> Checks { get; }
    /// <summary>Required checks proven not ready.</summary>
    public IReadOnlyList<EventReadinessCheckResult> RequiredFailures =>
        Checks.Where(static check => check.Required && check.Status == EventReadinessStatus.Fail).ToArray();
    /// <summary>Required evidence that could not be proven.</summary>
    public IReadOnlyList<EventReadinessCheckResult> UnknownRequiredChecks =>
        Checks.Where(static check => check.Required && check.Status == EventReadinessStatus.Unknown).ToArray();
    /// <summary>True only when every required check passed or produced a non-blocking warning.</summary>
    public bool IsReady => Checks.Where(static check => check.Required).All(static check =>
        check.Status == EventReadinessStatus.Pass || check.Status == EventReadinessStatus.Warning);
    /// <summary>False when any required check was unknown or skipped.</summary>
    public bool IsComplete => Checks.Where(static check => check.Required).All(static check =>
        check.Status != EventReadinessStatus.Unknown && check.Status != EventReadinessStatus.Skipped);
    /// <summary>Total assessment duration.</summary>
    public TimeSpan Duration { get; }
}
