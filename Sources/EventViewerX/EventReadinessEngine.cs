using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;

namespace EventViewerX;

/// <summary>Composes target, transport, effective-policy, observation, and configuration evidence.</summary>
public static partial class EventReadinessEngine {
    /// <summary>Assesses a selected typed-event workflow without changing the environment.</summary>
    public static EventReadinessReport Evaluate(
        EventReadinessRequest request,
        CancellationToken cancellationToken = default) =>
        Evaluate(request, new EventReadinessEvidenceProvider(), cancellationToken);

    internal static EventReadinessReport Evaluate(
        EventReadinessRequest request,
        IEventReadinessEvidenceProvider evidenceProvider,
        CancellationToken cancellationToken) {

        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }
        if (evidenceProvider == null) {
            throw new ArgumentNullException(nameof(evidenceProvider));
        }
        EventReadinessRequest snapshot = request.Snapshot();
        var stopwatch = Stopwatch.StartNew();
        var checks = new List<EventReadinessCheckResult>();
        checks.Add(CreateRuntimeCheck());

        EventTargetDiscoveryResult? discovery = null;
        IReadOnlyList<EventTargetInfo> targets;
        if (snapshot.Collector != null) {
            targets = new[] { new EventTargetInfo(snapshot.Collector, EventTargetKind.Collector) };
            if (snapshot.TargetDiscovery.Scope == EventTargetDiscoveryScope.LocalMachine) {
                checks.Add(new EventReadinessCheckResult(
                    EventReadinessLayer.TargetDiscovery,
                    "ExplicitCollector",
                    snapshot.Collector,
                    EventReadinessStatus.Pass,
                    EventReadinessEvidenceLevel.Inspected,
                    "A collector was explicitly selected; no Active Directory discovery was performed.",
                    string.Empty,
                    required: true));
            } else {
                discovery = evidenceProvider.ResolveTargets(snapshot.TargetDiscovery, cancellationToken);
                AddTargetDiscoveryChecks(discovery, checks);
            }
        } else {
            discovery = evidenceProvider.ResolveTargets(snapshot.TargetDiscovery, cancellationToken);
            targets = discovery.Targets;
            AddTargetDiscoveryChecks(discovery, checks);
        }

        IReadOnlyList<EventTargetInfo> sourceTargets = discovery?.Targets.Count > 0
            ? discovery.Targets
            : targets;
        AddTargetRoleChecks(snapshot, sourceTargets, evidenceProvider, checks);
        IReadOnlyList<EventSourceDefinition> sources = EventTypeCatalog.GetSources(snapshot.Types);
        foreach (EventTargetInfo target in targets) {
            foreach (EventSourceDefinition source in sources) {
                cancellationToken.ThrowIfCancellationRequested();
                bool collector = target.Kind == EventTargetKind.Collector;
                string targetLog = collector ? "ForwardedEvents" : source.LogName;
                string? machineName = target.Kind == EventTargetKind.LocalMachine ? null : target.ComputerName;
                NetworkCredential? credential = target.Kind == EventTargetKind.LocalMachine
                    ? null
                    : snapshot.EventLogCredential;
                EventLogProbeResult probe = collector
                    ? evidenceProvider.ProbeTypedCollectorSource(
                        snapshot.Types,
                        source,
                        target.ComputerName,
                        snapshot.ProbeTimeout,
                        snapshot.MaxEventsToScan,
                        credential,
                        snapshot.Authentication,
                        cancellationToken)
                    : ProbeSourceSafely(
                        evidenceProvider,
                        source,
                        collector: false,
                        targetLog,
                        machineName,
                        snapshot.ProbeTimeout,
                        snapshot.MaxEventsToScan,
                        credential,
                        snapshot.Authentication,
                        cancellationToken);
                checks.Add(CreateTransportCheck(target, source, probe, collector));
            }
        }

        if (snapshot.Collector != null) {
            AddCollectorChecks(snapshot, discovery, evidenceProvider, checks, cancellationToken);
        }
        AddAuditPolicyChecks(snapshot, sourceTargets, evidenceProvider, checks);
        AddConfigurationChecks(snapshot, sourceTargets, evidenceProvider, checks);
        return new EventReadinessReport(
            snapshot.Scenario,
            snapshot.Types,
            discovery,
            targets,
            checks,
            stopwatch.Elapsed);
    }

    private static EventReadinessCheckResult CreateRuntimeCheck() {
        bool windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        return new EventReadinessCheckResult(
            EventReadinessLayer.Runtime,
            "WindowsPlatform",
            EventLogTarget.LocalMachineName,
            windows ? EventReadinessStatus.Pass : EventReadinessStatus.Fail,
            EventReadinessEvidenceLevel.Inspected,
            windows ? "EventViewerX is running on Windows." : "Windows Event Log APIs are unavailable on this platform.",
            windows ? string.Empty : "Run EventViewerX collection and readiness checks on Windows.",
            required: true,
            diagnosticKind: windows ? EventReadinessDiagnosticKind.None : EventReadinessDiagnosticKind.Unavailable);
    }

    private static void AddTargetDiscoveryChecks(
        EventTargetDiscoveryResult discovery,
        List<EventReadinessCheckResult> checks) {

        foreach (EventTargetDomainResult domain in discovery.Domains) {
            EventTargetDiscoveryFailure? firstFailure = domain.Failures.FirstOrDefault();
            bool accessDenied = firstFailure?.Kind == EventTargetDiscoveryFailureKind.AccessDenied;
            checks.Add(new EventReadinessCheckResult(
                EventReadinessLayer.TargetDiscovery,
                "DomainControllers",
                domain.DomainName,
                domain.Succeeded && domain.Targets.Count > 0
                    ? EventReadinessStatus.Pass
                    : accessDenied
                        ? EventReadinessStatus.Unknown
                        : EventReadinessStatus.Fail,
                accessDenied ? EventReadinessEvidenceLevel.Unknown : EventReadinessEvidenceLevel.Inspected,
                domain.Succeeded
                    ? $"Discovered {domain.Targets.Count} domain controller(s)."
                    : $"Discovered {domain.Targets.Count} domain controller(s) with {domain.Failures.Count} failure(s).",
                domain.Succeeded && domain.Targets.Count > 0
                    ? string.Empty
                    : "Review the per-domain discovery failures, DNS, trust direction, and directory permissions.",
                required: true,
                diagnosticKind: firstFailure == null
                    ? domain.Targets.Count == 0
                        ? EventReadinessDiagnosticKind.Missing
                        : EventReadinessDiagnosticKind.None
                    : MapDiscoveryFailure(firstFailure.Kind)));
        }
        foreach (EventTargetDiscoveryFailure failure in discovery.Failures) {
            checks.Add(new EventReadinessCheckResult(
                EventReadinessLayer.TargetDiscovery,
                failure.Stage,
                failure.Scope,
                failure.Kind == EventTargetDiscoveryFailureKind.AccessDenied
                    ? EventReadinessStatus.Unknown
                    : EventReadinessStatus.Fail,
                EventReadinessEvidenceLevel.Unknown,
                failure.Message,
                "Review directory membership, DNS, reachability, permissions, and the explicit discovery scope.",
                required: true,
                diagnosticKind: MapDiscoveryFailure(failure.Kind)));
        }
        if (discovery.Targets.Count == 0) {
            checks.Add(new EventReadinessCheckResult(
                EventReadinessLayer.TargetDiscovery,
                "ResolvedTargets",
                discovery.RequestedName ?? discovery.Scope.ToString(),
                EventReadinessStatus.Fail,
                EventReadinessEvidenceLevel.Unknown,
                "No event-log target was resolved.",
                "Correct the explicit scope or use the default local-machine assessment.",
                required: true,
                diagnosticKind: EventReadinessDiagnosticKind.Missing));
        } else if (discovery.Domains.Count == 0 && discovery.Failures.Count == 0) {
            checks.Add(new EventReadinessCheckResult(
                EventReadinessLayer.TargetDiscovery,
                "LocalMachine",
                discovery.Targets[0].ComputerName,
                EventReadinessStatus.Pass,
                EventReadinessEvidenceLevel.Inspected,
                "The default local-machine target was selected; no Active Directory discovery was performed.",
                string.Empty,
                required: true));
        }
    }

    private static EventReadinessCheckResult CreateTransportCheck(
        EventTargetInfo target,
        EventSourceDefinition source,
        EventLogProbeResult probe,
        bool collector) {

        EventReadinessStatus status;
        EventReadinessEvidenceLevel evidenceLevel;
        EventReadinessDiagnosticKind diagnosticKind = EventReadinessDiagnosticKind.None;
        string evidence;
        string remediation = string.Empty;
        switch (probe.Status) {
            case EventLogProbeStatus.Ok:
                status = EventReadinessStatus.Pass;
                evidenceLevel = EventReadinessEvidenceLevel.Observed;
                evidence = $"The native query succeeded and observed a matching {source.LogName} event at {probe.EventTimeUtc:O}.";
                break;
            case EventLogProbeStatus.NoEvent:
                status = EventReadinessStatus.Warning;
                evidenceLevel = EventReadinessEvidenceLevel.Transport;
                evidence = $"The native query succeeded, but no matching {source.LogName} event was observed.";
                remediation = "Confirm effective audit/provider configuration and retain enough log history for the intended monitoring period.";
                diagnosticKind = EventReadinessDiagnosticKind.NoEvidence;
                break;
            case EventLogProbeStatus.LimitReached:
            case EventLogProbeStatus.NoUsableTimestamp:
                status = EventReadinessStatus.Warning;
                evidenceLevel = EventReadinessEvidenceLevel.Transport;
                evidence = probe.Message ?? "The native query succeeded but did not produce a usable timestamp.";
                remediation = "Review event rendering and increase the bounded scan only when the source volume justifies it.";
                diagnosticKind = probe.Status == EventLogProbeStatus.LimitReached
                    ? EventReadinessDiagnosticKind.Truncated
                    : EventReadinessDiagnosticKind.NoEvidence;
                break;
            default:
                status = probe.Status == EventLogProbeStatus.AccessDenied
                    ? EventReadinessStatus.Unknown
                    : EventReadinessStatus.Fail;
                evidenceLevel = EventReadinessEvidenceLevel.Unknown;
                evidence = probe.Message ?? probe.Status.ToString();
                remediation = probe.Status switch {
                    EventLogProbeStatus.AccessDenied => "Grant the collection identity Event Log read access on the source or collector.",
                    EventLogProbeStatus.LogNotFound => collector
                        ? "Enable and configure ForwardedEvents on the collector."
                        : $"Verify that the {source.LogName} channel exists and is enabled on the target role.",
                    EventLogProbeStatus.HostUnavailable => "Verify DNS, firewall Remote Event Log Management rules, RPC reachability, and service state.",
                    EventLogProbeStatus.Timeout => "Verify reachability and increase the bounded probe timeout only after diagnosing the slow stage.",
                    _ => "Review the native probe status and Windows error before scheduling collection."
                };
                diagnosticKind = MapProbeStatus(probe.Status);
                break;
        }
        return new EventReadinessCheckResult(
            EventReadinessLayer.EventLogTransport,
            collector ? "ForwardedSourceQuery" : "DirectSourceQuery",
            target.ComputerName + "/" + source.LogName,
            status,
            evidenceLevel,
            evidence,
            remediation,
            required: true,
            requirementKey: "channel:" + source.LogName.ToUpperInvariant(),
            duration: probe.Duration,
            diagnosticKind: diagnosticKind);
    }

    private static void AddTargetRoleChecks(
        EventReadinessRequest request,
        IReadOnlyList<EventTargetInfo> targets,
        IEventReadinessEvidenceProvider evidenceProvider,
        List<EventReadinessCheckResult> checks) {

        EventPrerequisite[] requirements = request.Types
            .Select(EventRequirementCatalog.GetRequirement)
            .SelectMany(static requirement => requirement.Prerequisites)
            .Where(static prerequisite => prerequisite.Kind == EventRequirementKind.TargetRole)
            .GroupBy(static prerequisite => prerequisite.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
        foreach (EventTargetInfo target in targets) {
            foreach (EventPrerequisite requirement in requirements) {
                EventReadinessConfigurationEvidence evidence;
                if (target.Kind == EventTargetKind.DomainController) {
                    evidence = new EventReadinessConfigurationEvidence(
                        EventReadinessStatus.Pass,
                        "Active Directory discovery identified this target as a domain controller.",
                        string.Empty);
                } else if (target.Kind == EventTargetKind.LocalMachine) {
                    evidence = evidenceProvider.ReadLocalConfiguration(requirement.Key);
                } else {
                    evidence = new EventReadinessConfigurationEvidence(
                        EventReadinessStatus.Unknown,
                        "The collector query does not prove the Windows role of each forwarding source.",
                        "Confirm that the subscription contains the intended domain controllers and retain per-source runtime evidence.");
                }
                checks.Add(new EventReadinessCheckResult(
                    EventReadinessLayer.TargetDiscovery,
                    requirement.Name,
                    target.ComputerName,
                    evidence.Status,
                    evidence.Status == EventReadinessStatus.Unknown
                        ? EventReadinessEvidenceLevel.Unknown
                        : EventReadinessEvidenceLevel.Inspected,
                    evidence.Evidence,
                    evidence.Remediation,
                    required: true,
                    requirementKey: requirement.Key,
                    diagnosticKind: evidence.DiagnosticKind));
            }
        }
    }

    private static void AddAuditPolicyChecks(
        EventReadinessRequest request,
        IReadOnlyList<EventTargetInfo> targets,
        IEventReadinessEvidenceProvider evidenceProvider,
        List<EventReadinessCheckResult> checks) {

        EventPrerequisite[] auditRequirements = request.Types
            .Select(EventRequirementCatalog.GetRequirement)
            .SelectMany(static requirement => requirement.Prerequisites)
            .Where(static prerequisite => prerequisite.Kind == EventRequirementKind.AuditPolicy)
            .GroupBy(static prerequisite => prerequisite.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
        Guid[] guids = auditRequirements
            .Where(static requirement => requirement.AuditSubcategoryGuid.HasValue)
            .Select(static requirement => requirement.AuditSubcategoryGuid!.Value)
            .Distinct()
            .ToArray();
        bool needsLocalPolicy = targets.Any(static target => target.Kind == EventTargetKind.LocalMachine);
        IReadOnlyDictionary<Guid, EffectiveAuditPolicyResult> localPolicy = guids.Length == 0 || !needsLocalPolicy
            ? new Dictionary<Guid, EffectiveAuditPolicyResult>()
            : QueryAuditPolicySafely(evidenceProvider, guids);

        foreach (EventTargetInfo target in targets) {
            foreach (EventPrerequisite requirement in auditRequirements) {
                bool local = target.Kind == EventTargetKind.LocalMachine;
                if (!local || !requirement.AuditSubcategoryGuid.HasValue) {
                    checks.Add(new EventReadinessCheckResult(
                        EventReadinessLayer.AuditPolicy,
                        requirement.Name,
                        target.ComputerName,
                        EventReadinessStatus.Unknown,
                        EventReadinessEvidenceLevel.Unknown,
                        "Effective audit policy was not read on this remote source. Transport and event observation are reported separately and do not prove the effective policy.",
                        "Inspect effective advanced audit policy on the source computer; configured GPO and historical events are not substitutes for effective policy.",
                        required: true,
                        requirementKey: requirement.Key,
                        diagnosticKind: EventReadinessDiagnosticKind.NoEvidence));
                    continue;
                }
                EffectiveAuditPolicyResult policy = localPolicy.TryGetValue(
                    requirement.AuditSubcategoryGuid.Value,
                    out EffectiveAuditPolicyResult? result)
                    ? result
                    : new EffectiveAuditPolicyResult(
                        requirement.AuditSubcategoryGuid.Value,
                        false,
                        EventAuditOutcome.None,
                        0,
                        "The effective audit policy provider did not return this requested subcategory.");
                EventAuditOutcome missing = requirement.AuditOutcomes & ~policy.Outcomes;
                EventReadinessStatus status = !policy.Succeeded
                    ? EventReadinessStatus.Unknown
                    : missing == EventAuditOutcome.None
                        ? EventReadinessStatus.Pass
                        : EventReadinessStatus.Fail;
                checks.Add(new EventReadinessCheckResult(
                    EventReadinessLayer.AuditPolicy,
                    requirement.Name,
                    target.ComputerName,
                    status,
                    policy.Succeeded ? EventReadinessEvidenceLevel.Effective : EventReadinessEvidenceLevel.Unknown,
                    policy.Succeeded
                        ? $"Effective outcomes: {policy.Outcomes}; required outcomes: {requirement.AuditOutcomes}."
                        : policy.Message ?? "Effective audit policy could not be read.",
                    status == EventReadinessStatus.Pass
                        ? string.Empty
                        : $"Enable {requirement.AuditOutcomes} for {requirement.Name} on the effective source policy.",
                    required: true,
                    requirementKey: requirement.Key,
                    diagnosticKind: !policy.Succeeded
                        ? policy.ErrorCode == 5
                            ? EventReadinessDiagnosticKind.AccessDenied
                            : EventReadinessDiagnosticKind.Error
                        : status == EventReadinessStatus.Fail
                            ? EventReadinessDiagnosticKind.InvalidConfiguration
                            : EventReadinessDiagnosticKind.None));
            }
        }
    }

    private static IReadOnlyDictionary<Guid, EffectiveAuditPolicyResult> QueryAuditPolicySafely(
        IEventReadinessEvidenceProvider evidenceProvider,
        IReadOnlyList<Guid> guids) {

        try {
            return evidenceProvider.QueryAuditPolicy(guids)
                .GroupBy(static result => result.SubcategoryGuid)
                .ToDictionary(static group => group.Key, static group => group.First());
        } catch (Exception exception) {
            return guids.ToDictionary(
                static guid => guid,
                guid => new EffectiveAuditPolicyResult(
                    guid,
                    false,
                    EventAuditOutcome.None,
                    0,
                    exception.Message));
        }
    }

    private static void AddConfigurationChecks(
        EventReadinessRequest request,
        IReadOnlyList<EventTargetInfo> targets,
        IEventReadinessEvidenceProvider evidenceProvider,
        List<EventReadinessCheckResult> checks) {

        EventPrerequisite[] requirements = request.Types
            .Select(EventRequirementCatalog.GetRequirement)
            .SelectMany(static requirement => requirement.Prerequisites)
            .Where(static prerequisite => prerequisite.Kind == EventRequirementKind.Configuration)
            .GroupBy(static prerequisite => prerequisite.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
        foreach (EventTargetInfo target in targets) {
            foreach (EventPrerequisite requirement in requirements) {
                EventReadinessConfigurationEvidence evidence = target.Kind == EventTargetKind.LocalMachine
                    ? evidenceProvider.ReadLocalConfiguration(requirement.Key)
                    : new EventReadinessConfigurationEvidence(
                        EventReadinessStatus.Unknown,
                        "This provider-specific configuration was not read on the remote source.",
                        "Inspect the documented setting on the source computer with an appropriately scoped identity.");
                checks.Add(new EventReadinessCheckResult(
                    EventReadinessLayer.Configuration,
                    requirement.Name,
                    target.ComputerName,
                    evidence.Status,
                    evidence.Status == EventReadinessStatus.Unknown
                        ? EventReadinessEvidenceLevel.Unknown
                        : EventReadinessEvidenceLevel.Inspected,
                    evidence.Evidence,
                    evidence.Remediation,
                    required: true,
                    requirementKey: requirement.Key,
                    diagnosticKind: evidence.DiagnosticKind));
            }
        }
    }

    private static EventReadinessDiagnosticKind MapDiscoveryFailure(EventTargetDiscoveryFailureKind kind) => kind switch {
        EventTargetDiscoveryFailureKind.AccessDenied => EventReadinessDiagnosticKind.AccessDenied,
        EventTargetDiscoveryFailureKind.Timeout => EventReadinessDiagnosticKind.Timeout,
        EventTargetDiscoveryFailureKind.LimitReached => EventReadinessDiagnosticKind.Truncated,
        EventTargetDiscoveryFailureKind.NotFound => EventReadinessDiagnosticKind.Missing,
        EventTargetDiscoveryFailureKind.NotDomainJoined => EventReadinessDiagnosticKind.InvalidConfiguration,
        _ => EventReadinessDiagnosticKind.Error
    };

    private static EventReadinessDiagnosticKind MapProbeStatus(EventLogProbeStatus status) => status switch {
        EventLogProbeStatus.AccessDenied => EventReadinessDiagnosticKind.AccessDenied,
        EventLogProbeStatus.Timeout => EventReadinessDiagnosticKind.Timeout,
        EventLogProbeStatus.HostUnavailable => EventReadinessDiagnosticKind.Unavailable,
        EventLogProbeStatus.LogNotFound => EventReadinessDiagnosticKind.Missing,
        EventLogProbeStatus.InvalidQuery => EventReadinessDiagnosticKind.InvalidConfiguration,
        EventLogProbeStatus.NoEvent or EventLogProbeStatus.NoUsableTimestamp => EventReadinessDiagnosticKind.NoEvidence,
        EventLogProbeStatus.LimitReached => EventReadinessDiagnosticKind.Truncated,
        EventLogProbeStatus.Error => EventReadinessDiagnosticKind.Error,
        _ => EventReadinessDiagnosticKind.None
    };

}
