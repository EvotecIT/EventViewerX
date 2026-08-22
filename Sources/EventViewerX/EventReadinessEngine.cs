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
        CancellationToken cancellationToken) => Evaluate(
            request,
            evidenceProvider,
            EventTypeCatalog.GetSources,
            cancellationToken);

    internal static EventReadinessReport Evaluate(
        EventReadinessRequest request,
        IEventReadinessEvidenceProvider evidenceProvider,
        Func<IEnumerable<EventType>, IReadOnlyList<EventSourceDefinition>> sourceResolver,
        CancellationToken cancellationToken) {

        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }
        if (evidenceProvider == null) {
            throw new ArgumentNullException(nameof(evidenceProvider));
        }
        if (sourceResolver == null) {
            throw new ArgumentNullException(nameof(sourceResolver));
        }
        EventReadinessRequest snapshot = request.Snapshot();
        var stopwatch = Stopwatch.StartNew();
        var checks = new List<EventReadinessCheckResult>();
        checks.Add(CreateRuntimeCheck());

        EventTargetDiscoveryResult? discovery = null;
        IReadOnlyList<EventTargetInfo> targets;
        if (snapshot.Collector != null) {
            string collectorTarget = NormalizeCollectorTarget(snapshot.Collector);
            targets = new[] { new EventTargetInfo(collectorTarget, EventTargetKind.Collector) };
            if (snapshot.TargetDiscovery.Scope == EventTargetDiscoveryScope.LocalMachine) {
                checks.Add(new EventReadinessCheckResult(
                    EventReadinessLayer.TargetDiscovery,
                    "ExplicitCollector",
                    collectorTarget,
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

        IReadOnlyList<EventTargetInfo> sourceTargets = ResolveSourceTargets(
            snapshot,
            discovery,
            targets);
        AddTargetRoleChecks(snapshot, sourceTargets, evidenceProvider, checks);
        EventType[] missingSourceTypes = EventTypeCatalog
            .Expand(snapshot.Types)
            .Where(type => sourceResolver(new[] { type }).Count == 0)
            .ToArray();
        AddRuleCatalogChecks(missingSourceTypes, checks);
        IReadOnlyList<EventSourceDefinition> sources = sourceResolver(snapshot.Types);
        AddChannelPolicyChecks(
            snapshot,
            discovery,
            sourceTargets,
            sources,
            evidenceProvider,
            checks,
            cancellationToken);
        foreach (EventTargetInfo target in targets) {
            foreach (EventSourceDefinition source in sources) {
                cancellationToken.ThrowIfCancellationRequested();
                bool collector = target.Kind == EventTargetKind.Collector;
                string targetLog = collector ? "ForwardedEvents" : source.LogName;
                bool localTarget = IsLocalSourceTarget(target);
                string? machineName = localTarget ? null : target.ComputerName;
                NetworkCredential? credential = localTarget
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
                        snapshot.Types,
                        source,
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
            AddCollectorChecks(snapshot, discovery, sources, evidenceProvider, checks, cancellationToken);
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

    private static string NormalizeCollectorTarget(string collector) =>
        EventLogTarget.IsLocalMachine(collector)
            ? EventLogTarget.LocalMachineName
            : collector.Trim().TrimEnd('.');

    private static IReadOnlyList<EventTargetInfo> ResolveSourceTargets(
        EventReadinessRequest request,
        EventTargetDiscoveryResult? discovery,
        IReadOnlyList<EventTargetInfo> transportTargets) {

        if (request.Collector == null) {
            return transportTargets;
        }
        EventTargetInfo[] expectedTargets = request.ExpectedSources
            .Select(static source => EventLogTarget.IsLocalMachine(source)
                ? new EventTargetInfo(EventLogTarget.LocalMachineName, EventTargetKind.LocalMachine)
                : new EventTargetInfo(source.Trim().TrimEnd('.'), EventTargetKind.EventLogMachine))
            .ToArray();
        if (discovery == null && expectedTargets.Length == 0) {
            return transportTargets;
        }
        return (discovery?.Targets ?? Array.Empty<EventTargetInfo>())
            .Concat(expectedTargets)
            .GroupBy(
                static target => EventLogTarget.IsLocalMachine(target.ComputerName)
                    ? EventLogTarget.LocalMachineName
                    : target.ComputerName.Trim().TrimEnd('.'),
                StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
    }

    private static bool IsLocalSourceTarget(EventTargetInfo target) =>
        target.Kind != EventTargetKind.Collector &&
        EventLogTarget.IsLocalMachine(target.ComputerName);

    private static void AddChannelPolicyChecks(
        EventReadinessRequest request,
        EventTargetDiscoveryResult? discovery,
        IReadOnlyList<EventTargetInfo> sourceTargets,
        IReadOnlyList<EventSourceDefinition> sources,
        IEventReadinessEvidenceProvider evidenceProvider,
        List<EventReadinessCheckResult> checks,
        CancellationToken cancellationToken) {

        if (request.Collector != null &&
            discovery == null &&
            request.ExpectedSources.Count == 0) {
            string collectorTarget = NormalizeCollectorTarget(request.Collector);
            foreach (EventSourceDefinition source in sources) {
                cancellationToken.ThrowIfCancellationRequested();
                checks.Add(new EventReadinessCheckResult(
                    EventReadinessLayer.EventLogTransport,
                    "ChannelPolicy",
                    collectorTarget + "/" + source.LogName,
                    EventReadinessStatus.Unknown,
                    EventReadinessEvidenceLevel.Unknown,
                    "A collector was selected without an explicit source scope, so the source channel policy was not inspected.",
                    "Supply an explicit Active Directory source scope or assess the source computers directly.",
                    required: true,
                    requirementKey: "channel:" + source.LogName.ToUpperInvariant(),
                    diagnosticKind: EventReadinessDiagnosticKind.NoEvidence));
            }
            return;
        }

        foreach (EventTargetInfo target in sourceTargets) {
            foreach (EventSourceDefinition source in sources) {
                cancellationToken.ThrowIfCancellationRequested();
                bool localTarget = IsLocalSourceTarget(target);
                string? machineName = localTarget ? null : target.ComputerName;
                NetworkCredential? credential = localTarget
                    ? null
                    : request.EventLogCredential;
                try {
                    ChannelPolicy? policy = evidenceProvider.ReadChannelPolicy(
                        source.LogName,
                        machineName,
                        request.ProbeTimeout,
                        credential,
                        request.Authentication,
                        cancellationToken);
                    if (policy == null) {
                        checks.Add(CreateChannelPolicyCheck(
                            target.ComputerName,
                            source,
                            EventReadinessStatus.Fail,
                            EventReadinessEvidenceLevel.Inspected,
                            "The event channel was not found.",
                            "Register the required channel or remove event types that depend on it.",
                            EventReadinessDiagnosticKind.Missing));
                    } else if (!policy.IsEnabled.HasValue) {
                        checks.Add(CreateChannelPolicyCheck(
                            target.ComputerName,
                            source,
                            EventReadinessStatus.Unknown,
                            EventReadinessEvidenceLevel.Unknown,
                            $"Channel exists, but enabled state was not returned; mode={policy.ModeName}; maximum size={policy.MaximumSizeInBytes?.ToString() ?? "Unknown"} bytes.",
                            "Inspect the channel configuration with an identity permitted to read channel policy.",
                            EventReadinessDiagnosticKind.NoEvidence));
                    } else {
                        checks.Add(CreateChannelPolicyCheck(
                            target.ComputerName,
                            source,
                            policy.IsEnabled.Value ? EventReadinessStatus.Pass : EventReadinessStatus.Fail,
                            EventReadinessEvidenceLevel.Inspected,
                            $"Channel enabled={policy.IsEnabled.Value}; mode={policy.ModeName}; maximum size={policy.MaximumSizeInBytes?.ToString() ?? "Unknown"} bytes.",
                            policy.IsEnabled.Value ? string.Empty : "Enable the required event channel before relying on its events.",
                            policy.IsEnabled.Value ? EventReadinessDiagnosticKind.None : EventReadinessDiagnosticKind.InvalidConfiguration));
                    }
                } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                    throw;
                } catch (Exception exception) {
                    EventReadinessDiagnosticKind kind = ClassifyInspectionException(exception);
                    bool missing = kind == EventReadinessDiagnosticKind.Missing;
                    checks.Add(CreateChannelPolicyCheck(
                        target.ComputerName,
                        source,
                        missing ? EventReadinessStatus.Fail : EventReadinessStatus.Unknown,
                        missing ? EventReadinessEvidenceLevel.Inspected : EventReadinessEvidenceLevel.Unknown,
                        missing ? "The event channel was not found: " + exception.Message : exception.Message,
                        missing
                            ? "Register the required channel or remove event types that depend on it."
                            : "Inspect the channel policy locally or grant read access to the selected source.",
                        kind));
                }
            }
        }
    }

    private static void AddRuleCatalogChecks(
        IEnumerable<EventType> missingSourceTypes,
        ICollection<EventReadinessCheckResult> checks) {

        foreach (EventType type in missingSourceTypes) {
            checks.Add(new EventReadinessCheckResult(
                EventReadinessLayer.Configuration,
                "RuleCatalog",
                type.ToString(),
                EventReadinessStatus.Fail,
                EventReadinessEvidenceLevel.Inspected,
                $"No active event rule/source registration exists for requested type '{type}'.",
                "Register the requested rule before catalog initialization or use a discovery mode that includes it.",
                required: true,
                requirementKey: "rule-catalog:" + type.ToString().ToUpperInvariant(),
                diagnosticKind: EventReadinessDiagnosticKind.InvalidConfiguration));
        }
    }

    private static EventReadinessCheckResult CreateChannelPolicyCheck(
        string target,
        EventSourceDefinition source,
        EventReadinessStatus status,
        EventReadinessEvidenceLevel evidenceLevel,
        string evidence,
        string remediation,
        EventReadinessDiagnosticKind diagnosticKind) => new(
            EventReadinessLayer.EventLogTransport,
            "ChannelPolicy",
            target + "/" + source.LogName,
            status,
            evidenceLevel,
            evidence,
            remediation,
            required: true,
            requirementKey: "channel:" + source.LogName.ToUpperInvariant(),
            diagnosticKind: diagnosticKind);

    private static EventReadinessDiagnosticKind ClassifyInspectionException(Exception exception) {
        for (Exception? current = exception; current != null; current = current.InnerException) {
            if (current is System.Diagnostics.Eventing.Reader.EventLogNotFoundException) {
                return EventReadinessDiagnosticKind.Missing;
            }
            if (current is UnauthorizedAccessException || current is System.Security.SecurityException) {
                return EventReadinessDiagnosticKind.AccessDenied;
            }
            if (current is TimeoutException) {
                return EventReadinessDiagnosticKind.Timeout;
            }
        }
        return EventReadinessDiagnosticKind.Error;
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
            EventTargetDiscoveryFailure? representativeFailure = SelectAggregateDiscoveryFailure(
                domain.Failures,
                discovery);
            bool indeterminate = representativeFailure != null &&
                IsIndeterminateDiscoveryFailure(representativeFailure, discovery);
            checks.Add(new EventReadinessCheckResult(
                EventReadinessLayer.TargetDiscovery,
                "DomainControllers",
                domain.DomainName,
                domain.Succeeded && domain.Targets.Count > 0
                    ? EventReadinessStatus.Pass
                    : indeterminate
                        ? EventReadinessStatus.Unknown
                        : EventReadinessStatus.Fail,
                indeterminate ? EventReadinessEvidenceLevel.Unknown : EventReadinessEvidenceLevel.Inspected,
                domain.Succeeded
                    ? $"Discovered {domain.Targets.Count} domain controller(s)."
                    : $"Discovered {domain.Targets.Count} domain controller(s) with {domain.Failures.Count} failure(s).",
                domain.Succeeded && domain.Targets.Count > 0
                    ? string.Empty
                    : "Review the per-domain discovery failures, DNS, trust direction, and directory permissions.",
                required: true,
                diagnosticKind: representativeFailure == null
                    ? domain.Targets.Count == 0
                        ? EventReadinessDiagnosticKind.Missing
                        : EventReadinessDiagnosticKind.None
                    : MapDiscoveryFailure(representativeFailure, discovery)));
        }
        foreach (EventTargetDiscoveryFailure failure in discovery.Failures) {
            bool indeterminate = IsIndeterminateDiscoveryFailure(failure, discovery);
            checks.Add(new EventReadinessCheckResult(
                EventReadinessLayer.TargetDiscovery,
                failure.Stage,
                failure.Scope,
                indeterminate ? EventReadinessStatus.Unknown : EventReadinessStatus.Fail,
                indeterminate ? EventReadinessEvidenceLevel.Unknown : EventReadinessEvidenceLevel.Inspected,
                failure.Message,
                "Review directory membership, DNS, reachability, permissions, and the explicit discovery scope.",
                required: true,
                diagnosticKind: MapDiscoveryFailure(failure, discovery)));
        }
        if (discovery.Targets.Count == 0) {
            EventTargetDiscoveryFailure? representativeFailure = SelectAggregateDiscoveryFailure(
                discovery.Failures.Concat(discovery.Domains.SelectMany(static domain => domain.Failures)),
                discovery);
            bool indeterminate = representativeFailure != null &&
                IsIndeterminateDiscoveryFailure(representativeFailure, discovery);
            checks.Add(new EventReadinessCheckResult(
                EventReadinessLayer.TargetDiscovery,
                "ResolvedTargets",
                discovery.RequestedName ?? discovery.Scope.ToString(),
                indeterminate ? EventReadinessStatus.Unknown : EventReadinessStatus.Fail,
                indeterminate ? EventReadinessEvidenceLevel.Unknown : EventReadinessEvidenceLevel.Inspected,
                representativeFailure == null
                    ? "No event-log target was resolved."
                    : "No event-log target was resolved because discovery did not complete: " + representativeFailure.Message,
                "Correct the explicit scope or use the default local-machine assessment.",
                required: true,
                diagnosticKind: representativeFailure == null
                    ? EventReadinessDiagnosticKind.Missing
                    : MapDiscoveryFailure(representativeFailure, discovery)));
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

    private static EventTargetDiscoveryFailure? SelectAggregateDiscoveryFailure(
        IEnumerable<EventTargetDiscoveryFailure> failures,
        EventTargetDiscoveryResult discovery) {

        EventTargetDiscoveryFailure[] snapshot = failures.ToArray();
        return snapshot.FirstOrDefault(failure => IsIndeterminateDiscoveryFailure(failure, discovery)) ??
            snapshot.FirstOrDefault();
    }

    private static bool IsIndeterminateDiscoveryFailure(
        EventTargetDiscoveryFailure failure,
        EventTargetDiscoveryResult discovery) {

        if (failure.Kind is EventTargetDiscoveryFailureKind.AccessDenied or
            EventTargetDiscoveryFailureKind.Timeout or
            EventTargetDiscoveryFailureKind.LimitReached or
            EventTargetDiscoveryFailureKind.Error) {
            return true;
        }
        if (failure.Kind != EventTargetDiscoveryFailureKind.NotFound) {
            return false;
        }
        string expectedStage = discovery.Scope switch {
            EventTargetDiscoveryScope.Domain => "ResolveDomain",
            EventTargetDiscoveryScope.Forest => "ResolveForest",
            _ => string.Empty
        };
        return string.IsNullOrWhiteSpace(discovery.RequestedName) ||
            !string.Equals(failure.Stage, expectedStage, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(failure.Scope, discovery.RequestedName, StringComparison.OrdinalIgnoreCase);
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
            .Select(EventRequirementCatalog.MergePrerequisites)
            .ToArray();
        foreach (EventTargetInfo target in targets) {
            foreach (EventPrerequisite requirement in requirements) {
                EventReadinessConfigurationEvidence evidence;
                if (target.Kind == EventTargetKind.DomainController &&
                    string.Equals(
                        requirement.Key,
                        "target-role:domain-controller",
                        StringComparison.OrdinalIgnoreCase)) {
                    evidence = new EventReadinessConfigurationEvidence(
                        EventReadinessStatus.Pass,
                        "Active Directory discovery identified this target as a domain controller.",
                        string.Empty);
                } else if (IsLocalSourceTarget(target)) {
                    evidence = evidenceProvider.ReadLocalConfiguration(requirement.Key);
                } else {
                    evidence = new EventReadinessConfigurationEvidence(
                        EventReadinessStatus.Unknown,
                        $"The collector query does not prove the required source role '{requirement.Name}'.",
                        "Confirm the required Windows role on each forwarding source and retain per-source runtime evidence.",
                        EventReadinessDiagnosticKind.NoEvidence);
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
            .Select(EventRequirementCatalog.MergePrerequisites)
            .ToArray();
        Guid[] guids = auditRequirements
            .Where(static requirement => requirement.AuditSubcategoryGuid.HasValue)
            .Select(static requirement => requirement.AuditSubcategoryGuid!.Value)
            .Distinct()
            .ToArray();
        bool needsLocalPolicy = targets.Any(IsLocalSourceTarget);
        IReadOnlyDictionary<Guid, EffectiveAuditPolicyResult> localPolicy = guids.Length == 0 || !needsLocalPolicy
            ? new Dictionary<Guid, EffectiveAuditPolicyResult>()
            : QueryAuditPolicySafely(evidenceProvider, guids);

        foreach (EventTargetInfo target in targets) {
            foreach (EventPrerequisite requirement in auditRequirements) {
                bool local = IsLocalSourceTarget(target);
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
            .Select(EventRequirementCatalog.MergePrerequisites)
            .ToArray();
        foreach (EventTargetInfo target in targets) {
            foreach (EventPrerequisite requirement in requirements) {
                EventReadinessConfigurationEvidence evidence = IsLocalSourceTarget(target)
                    ? evidenceProvider.ReadLocalConfiguration(requirement.Key)
                    : new EventReadinessConfigurationEvidence(
                        EventReadinessStatus.Unknown,
                        "This provider-specific configuration was not read on the remote source.",
                        "Inspect the documented setting on the source computer with an appropriately scoped identity.",
                        EventReadinessDiagnosticKind.NoEvidence);
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

    private static EventReadinessDiagnosticKind MapDiscoveryFailure(
        EventTargetDiscoveryFailure failure,
        EventTargetDiscoveryResult discovery) => failure.Kind switch {
        EventTargetDiscoveryFailureKind.AccessDenied => EventReadinessDiagnosticKind.AccessDenied,
        EventTargetDiscoveryFailureKind.Timeout => EventReadinessDiagnosticKind.Timeout,
        EventTargetDiscoveryFailureKind.LimitReached => EventReadinessDiagnosticKind.Truncated,
        EventTargetDiscoveryFailureKind.NotFound => IsIndeterminateDiscoveryFailure(failure, discovery)
            ? EventReadinessDiagnosticKind.Unavailable
            : EventReadinessDiagnosticKind.Missing,
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
