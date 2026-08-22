namespace EventViewerX;

/// <summary>Windows Event Collector readiness composition.</summary>
public static partial class EventReadinessEngine {
    private static void AddCollectorChecks(
        EventReadinessRequest request,
        EventTargetDiscoveryResult? discovery,
        IReadOnlyList<EventSourceDefinition> sources,
        IEventReadinessEvidenceProvider evidenceProvider,
        List<EventReadinessCheckResult> checks,
        CancellationToken cancellationToken) {

        string collector = NormalizeCollectorTarget(request.Collector!);
        bool localCollector = EventLogTarget.IsLocalMachine(collector);
        if (localCollector) {
            try {
                CollectorReadinessStatus readiness = evidenceProvider.ReadLocalCollectorReadiness(cancellationToken);
                AddCollectorBooleanCheck(
                    checks,
                    collector,
                    "CollectorService",
                    readiness.CollectorServiceDiagnosticKind == EventReadinessDiagnosticKind.None
                        ? readiness.CollectorServiceInstalled && readiness.CollectorServiceRunning
                        : null,
                    $"Wecsvc installed={readiness.CollectorServiceInstalled}; running={readiness.CollectorServiceRunning}; start mode={readiness.CollectorServiceStartMode}.",
                    "Install and start the Windows Event Collector service before scheduling collection.",
                    EventReadinessDiagnosticKind.Missing,
                    readiness.CollectorServiceDiagnosticKind);
                AddCollectorBooleanCheck(
                    checks,
                    collector,
                    "WinRMService",
                    readiness.WinRmDiagnosticKind == EventReadinessDiagnosticKind.None
                        ? readiness.WinRmServiceRunning
                        : null,
                    $"WinRM running={readiness.WinRmServiceRunning}.",
                    "Install and start WinRM before configuring Windows Event Forwarding.",
                    EventReadinessDiagnosticKind.Missing,
                    readiness.WinRmDiagnosticKind);
                AddCollectorBooleanCheck(
                    checks,
                    collector,
                    "WinRMListener",
                    readiness.WinRmListenerDiagnosticKind == EventReadinessDiagnosticKind.None
                        ? readiness.WinRmListenerAvailable
                        : null,
                    $"WinRM listener available={readiness.WinRmListenerAvailable}.",
                    "Configure the required scoped WinRM listener and firewall policy for Windows Event Forwarding.",
                    EventReadinessDiagnosticKind.InvalidConfiguration,
                    readiness.WinRmListenerDiagnosticKind);
                AddCollectorBooleanCheck(
                    checks,
                    collector,
                    "ForwardedEvents",
                    readiness.ForwardedEventsDiagnosticKind == EventReadinessDiagnosticKind.None
                        ? readiness.ForwardedEventsExists && readiness.ForwardedEventsEnabled
                        : null,
                    $"ForwardedEvents exists={readiness.ForwardedEventsExists}; enabled={readiness.ForwardedEventsEnabled}.",
                    "Register and enable ForwardedEvents before scheduling collection.",
                    readiness.ForwardedEventsExists
                        ? EventReadinessDiagnosticKind.InvalidConfiguration
                        : EventReadinessDiagnosticKind.Missing,
                    readiness.ForwardedEventsDiagnosticKind);
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch (UnauthorizedAccessException exception) {
                checks.Add(new EventReadinessCheckResult(
                    EventReadinessLayer.WindowsEventCollector,
                    "CollectorHostReadiness",
                    collector,
                    EventReadinessStatus.Unknown,
                    EventReadinessEvidenceLevel.Unknown,
                    exception.Message,
                    "Run the read-only assessment with an identity permitted to inspect local services, WinRM, and ForwardedEvents.",
                    required: true,
                    diagnosticKind: EventReadinessDiagnosticKind.AccessDenied));
            } catch (Exception exception) {
                checks.Add(new EventReadinessCheckResult(
                    EventReadinessLayer.WindowsEventCollector,
                    "CollectorHostReadiness",
                    collector,
                    EventReadinessStatus.Unknown,
                    EventReadinessEvidenceLevel.Unknown,
                    exception.Message,
                    "Inspect Wecsvc, WinRM, its listener, and ForwardedEvents on the collector.",
                    required: true,
                    diagnosticKind: EventReadinessDiagnosticKind.Error));
            }
        } else {
            checks.Add(new EventReadinessCheckResult(
                EventReadinessLayer.WindowsEventCollector,
                "CollectorHostReadiness",
                collector,
                EventReadinessStatus.Unknown,
                EventReadinessEvidenceLevel.Unknown,
                "Local service and WinRM state cannot be inspected through Event Log RPC on a remote collector.",
                "Run Test-EVXReadiness locally on the collector or use an explicitly authorized remote execution boundary.",
                required: true,
                diagnosticKind: EventReadinessDiagnosticKind.NoEvidence));
        }

        if (request.SubscriptionName == null) {
            checks.Add(new EventReadinessCheckResult(
                EventReadinessLayer.WindowsEventCollector,
                "SubscriptionSelection",
                collector,
                EventReadinessStatus.Unknown,
                EventReadinessEvidenceLevel.Unknown,
                "No WEC subscription name was supplied, so configuration and source enrollment cannot be assessed.",
                "Supply -SubscriptionName and either explicit -ExpectedSource values or opt-in Active Directory discovery.",
                required: true,
                diagnosticKind: EventReadinessDiagnosticKind.NoEvidence));
            return;
        }

        CollectorSubscriptionSnapshot? subscription = null;
        bool subscriptionInspected = false;
        try {
            subscription = evidenceProvider.ReadCollectorSubscription(
                request.SubscriptionName,
                localCollector ? null : collector,
                request.ProbeTimeout,
                cancellationToken);
            subscriptionInspected = true;
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) when (IsAccessDeniedException(exception)) {
            checks.Add(new EventReadinessCheckResult(
                EventReadinessLayer.WindowsEventCollector,
                "SubscriptionConfiguration",
                collector + "/" + request.SubscriptionName,
                EventReadinessStatus.Unknown,
                EventReadinessEvidenceLevel.Unknown,
                exception.Message,
                "Grant read access to the collector subscription registry or run the assessment locally on the collector.",
                required: true,
                diagnosticKind: EventReadinessDiagnosticKind.AccessDenied));
        } catch (Exception exception) {
            EventReadinessDiagnosticKind diagnosticKind = ClassifyInspectionException(exception);
            checks.Add(new EventReadinessCheckResult(
                EventReadinessLayer.WindowsEventCollector,
                "SubscriptionConfiguration",
                collector + "/" + request.SubscriptionName,
                EventReadinessStatus.Unknown,
                EventReadinessEvidenceLevel.Unknown,
                exception.Message,
                "Inspect the named subscription locally on the collector.",
                required: true,
                diagnosticKind: diagnosticKind));
        }
        if (subscriptionInspected && subscription == null) {
            checks.Add(new EventReadinessCheckResult(
                EventReadinessLayer.WindowsEventCollector,
                "SubscriptionConfiguration",
                collector + "/" + request.SubscriptionName,
                EventReadinessStatus.Fail,
                EventReadinessEvidenceLevel.Inspected,
                "The named subscription was not found.",
                "Create the subscription or correct the supplied subscription name.",
                required: true,
                diagnosticKind: EventReadinessDiagnosticKind.Missing));
            return;
        } else if (subscription != null) {
            AddCollectorBooleanCheck(
                checks,
                collector + "/" + request.SubscriptionName,
                "SubscriptionEnabled",
                subscription.IsEnabled,
                $"Subscription enabled={subscription.IsEnabled?.ToString() ?? "Unknown"}.",
                "Enable the subscription after validating its source ACL and query definition.",
                EventReadinessDiagnosticKind.InvalidConfiguration);
            AddCollectorBooleanCheck(
                checks,
                collector + "/" + request.SubscriptionName,
                "SubscriptionDefinition",
                subscription.HasXml && subscription.QueryCount > 0,
                $"Subscription XML present={subscription.HasXml}; query count={subscription.QueryCount}.",
                "Apply a typed EventViewerX subscription definition containing the selected event sources.",
                EventReadinessDiagnosticKind.Missing);
            CollectorSubscriptionCoverageResult coverage = CollectorSubscriptionCoverageEvaluator.Evaluate(
                subscription,
                sources);
            checks.Add(new EventReadinessCheckResult(
                EventReadinessLayer.WindowsEventCollector,
                "SubscriptionCoverage",
                collector + "/" + request.SubscriptionName,
                coverage.Status,
                coverage.Status == EventReadinessStatus.Unknown
                    ? EventReadinessEvidenceLevel.Unknown
                    : EventReadinessEvidenceLevel.Inspected,
                coverage.Evidence,
                coverage.Remediation,
                required: true,
                diagnosticKind: coverage.DiagnosticKind));
        }

        string[] expectedSources = BuildExpectedSourceSet(
            request.ExpectedSources.Concat(
                discovery?.Targets.Select(static target => target.ComputerName) ?? Array.Empty<string>()));
        if (expectedSources.Length == 0) {
            checks.Add(new EventReadinessCheckResult(
                EventReadinessLayer.WindowsEventCollector,
                "ExpectedSourceSet",
                collector + "/" + request.SubscriptionName,
                EventReadinessStatus.Unknown,
                EventReadinessEvidenceLevel.Unknown,
                "No expected source set was supplied or discovered; runtime output cannot reveal a source that never enrolled.",
                "Supply -ExpectedSource or explicitly opt in with -ActiveDirectory CurrentDomain, CurrentForest, Domain, or Forest.",
                required: true,
                diagnosticKind: EventReadinessDiagnosticKind.NoEvidence));
        }
        if (!localCollector) {
            checks.Add(new EventReadinessCheckResult(
                EventReadinessLayer.WindowsEventCollector,
                "ExpectedSourceRuntime",
                collector + "/" + request.SubscriptionName,
                EventReadinessStatus.Unknown,
                EventReadinessEvidenceLevel.Unknown,
                $"{expectedSources.Length} expected source(s) are known, but wecutil runtime status is local-only.",
                "Run the same readiness command locally on the collector to compare expected sources with runtime enrollment.",
                required: true,
                diagnosticKind: EventReadinessDiagnosticKind.NoEvidence));
            return;
        }

        CollectorSubscriptionRuntimeStatus runtime;
        try {
            runtime = evidenceProvider.ReadLocalCollectorRuntime(request.SubscriptionName, cancellationToken);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) when (IsAccessDeniedException(exception)) {
            checks.Add(new EventReadinessCheckResult(
                EventReadinessLayer.WindowsEventCollector,
                "SubscriptionRuntime",
                collector + "/" + request.SubscriptionName,
                EventReadinessStatus.Unknown,
                EventReadinessEvidenceLevel.Unknown,
                exception.Message,
                "Run the assessment with an identity permitted to read local WEC runtime status.",
                required: true,
                diagnosticKind: EventReadinessDiagnosticKind.AccessDenied));
            return;
        } catch (Exception exception) {
            checks.Add(new EventReadinessCheckResult(
                EventReadinessLayer.WindowsEventCollector,
                "SubscriptionRuntime",
                collector + "/" + request.SubscriptionName,
                EventReadinessStatus.Unknown,
                EventReadinessEvidenceLevel.Unknown,
                exception.Message,
                "Run 'wecutil gr' or Get-EVXCollectorSubscription -IncludeRuntimeStatus locally and inspect the Windows error.",
                required: true,
                diagnosticKind: EventReadinessDiagnosticKind.Error));
            return;
        }
        bool runtimeHasDefinitiveError =
            runtime.LastErrorCode.HasValue && runtime.LastErrorCode.Value != 0 ||
            !string.IsNullOrWhiteSpace(runtime.ErrorMessage);
        bool runtimeStateConclusive = !string.IsNullOrWhiteSpace(runtime.Status) || runtimeHasDefinitiveError;
        bool runtimeIsHealthy =
            string.Equals(runtime.Status, "Active", StringComparison.OrdinalIgnoreCase) &&
            !runtimeHasDefinitiveError;
        AddCollectorBooleanCheck(
            checks,
            collector + "/" + request.SubscriptionName,
            "SubscriptionRuntime",
            runtimeStateConclusive ? runtimeIsHealthy : null,
            $"Runtime status={runtime.Status}; events processed={runtime.EventsProcessed}; last error={runtime.LastErrorCode}.",
            runtimeStateConclusive
                ? "Inspect the subscription and each source runtime error before accepting collection coverage."
                : "Run 'wecutil gr' locally and confirm that Windows returned runtime evidence for the subscription.",
            EventReadinessDiagnosticKind.InvalidConfiguration);
        if ((!runtimeStateConclusive && runtime.Sources.Count == 0) || expectedSources.Length == 0) {
            return;
        }
        CollectorSubscriptionSourceRuntimeStatus[] runtimeSources = runtime.Sources
            .Where(static source => !string.IsNullOrWhiteSpace(source.Address))
            .GroupBy(
                static source => NormalizeSourceAddress(source.Address),
                StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
        foreach (string expectedSource in expectedSources) {
            CollectorSubscriptionSourceRuntimeStatus? source = FindRuntimeSource(
                expectedSource,
                expectedSources,
                runtimeSources);
            if (source == null) {
                checks.Add(new EventReadinessCheckResult(
                    EventReadinessLayer.WindowsEventCollector,
                    "ExpectedSourceRuntime",
                    expectedSource,
                    EventReadinessStatus.Fail,
                    EventReadinessEvidenceLevel.Inspected,
                    "The expected source is absent from the subscription runtime source set.",
                    "Verify source policy, subscription ACL, WinRM reachability, and forwarding-client operational logs.",
                    required: true,
                    diagnosticKind: EventReadinessDiagnosticKind.Missing));
                continue;
            }
            bool sourceHasDefinitiveError =
                source.LastErrorCode.HasValue && source.LastErrorCode.Value != 0 ||
                !string.IsNullOrWhiteSpace(source.ErrorMessage);
            bool sourceStateConclusive = !string.IsNullOrWhiteSpace(source.Status) || sourceHasDefinitiveError;
            bool sourceIsHealthy =
                string.Equals(source.Status, "Active", StringComparison.OrdinalIgnoreCase) &&
                !sourceHasDefinitiveError;
            checks.Add(new EventReadinessCheckResult(
                EventReadinessLayer.WindowsEventCollector,
                "ExpectedSourceRuntime",
                expectedSource,
                !sourceStateConclusive
                    ? EventReadinessStatus.Unknown
                    : sourceIsHealthy
                        ? EventReadinessStatus.Pass
                        : EventReadinessStatus.Fail,
                sourceStateConclusive
                    ? EventReadinessEvidenceLevel.Inspected
                    : EventReadinessEvidenceLevel.Unknown,
                $"Runtime status={source.Status}; events processed={source.EventsProcessed}; last heartbeat={source.LastHeartbeatTime:O}; last error={source.LastErrorCode}.",
                sourceStateConclusive && sourceIsHealthy
                    ? string.Empty
                    : "Inspect this source's WEF operational log, WinRM path, and subscription authorization.",
                required: true,
                diagnosticKind: !sourceStateConclusive
                    ? EventReadinessDiagnosticKind.NoEvidence
                    : sourceIsHealthy
                        ? EventReadinessDiagnosticKind.None
                        : EventReadinessDiagnosticKind.InvalidConfiguration));
        }
    }

    private static string[] BuildExpectedSourceSet(IEnumerable<string> sources) => sources
        .Where(static source => !string.IsNullOrWhiteSpace(source))
        .Select(NormalizeSourceAddress)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .GroupBy(GetSourceLeaf, StringComparer.OrdinalIgnoreCase)
        .SelectMany(static group => {
            string[] names = group.ToArray();
            string[] qualified = names
                .Where(IsQualifiedSourceAddress)
                .ToArray();
            return qualified.Length == 1
                ? qualified
                : names;
        })
        .OrderBy(static source => source, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static CollectorSubscriptionSourceRuntimeStatus? FindRuntimeSource(
        string expectedSource,
        IReadOnlyList<string> expectedSources,
        IReadOnlyList<CollectorSubscriptionSourceRuntimeStatus> runtimeSources) {

        string normalizedExpected = NormalizeSourceAddress(expectedSource);
        CollectorSubscriptionSourceRuntimeStatus? exact = runtimeSources.FirstOrDefault(source =>
            string.Equals(
                NormalizeSourceAddress(source.Address),
                normalizedExpected,
                StringComparison.OrdinalIgnoreCase));
        if (exact != null) {
            return exact;
        }

        string leaf = GetSourceLeaf(normalizedExpected);
        if (expectedSources.Count(source =>
                string.Equals(GetSourceLeaf(source), leaf, StringComparison.OrdinalIgnoreCase)) != 1) {
            return null;
        }
        CollectorSubscriptionSourceRuntimeStatus[] candidates = runtimeSources
            .Where(source => string.Equals(
                GetSourceLeaf(source.Address),
                leaf,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (IsQualifiedSourceAddress(normalizedExpected)) {
            candidates = candidates
                .Where(static source => !IsQualifiedSourceAddress(source.Address))
                .ToArray();
            return candidates.Length == 1 ? candidates[0] : null;
        }

        string[] qualifiedCandidates = candidates
            .Select(static source => NormalizeSourceAddress(source.Address))
            .Where(IsQualifiedSourceAddress)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (qualifiedCandidates.Length > 1) {
            return null;
        }
        if (qualifiedCandidates.Length == 1) {
            return candidates.First(source => string.Equals(
                NormalizeSourceAddress(source.Address),
                qualifiedCandidates[0],
                StringComparison.OrdinalIgnoreCase));
        }
        return candidates.Length == 1 ? candidates[0] : null;
    }

    private static string NormalizeSourceAddress(string source) =>
        EventLogTarget.IsLocalMachine(source)
            ? EventLogTarget.LocalMachineName
            : source.Trim().TrimEnd('.');

    private static string GetSourceLeaf(string source) {
        string normalized = NormalizeSourceAddress(source);
        int separator = normalized.IndexOf('.');
        return separator < 0 ? normalized : normalized.Substring(0, separator);
    }

    private static bool IsQualifiedSourceAddress(string source) =>
        NormalizeSourceAddress(source).IndexOf('.') >= 0;

    private static void AddCollectorBooleanCheck(
        List<EventReadinessCheckResult> checks,
        string target,
        string check,
        bool? succeeded,
        string evidence,
        string remediation,
        EventReadinessDiagnosticKind failureKind,
        EventReadinessDiagnosticKind unknownKind = EventReadinessDiagnosticKind.NoEvidence) => checks.Add(new EventReadinessCheckResult(
            EventReadinessLayer.WindowsEventCollector,
            check,
            target,
            succeeded.HasValue
                ? succeeded.Value
                    ? EventReadinessStatus.Pass
                    : EventReadinessStatus.Fail
                : EventReadinessStatus.Unknown,
            succeeded.HasValue
                ? EventReadinessEvidenceLevel.Inspected
                : EventReadinessEvidenceLevel.Unknown,
            evidence,
            succeeded == true ? string.Empty : remediation,
            required: true,
            diagnosticKind: succeeded.HasValue
                ? succeeded.Value
                    ? EventReadinessDiagnosticKind.None
                    : failureKind
                : unknownKind));

    private static bool IsAccessDeniedException(Exception exception) {
        for (Exception? current = exception; current != null; current = current.InnerException) {
            if (current is UnauthorizedAccessException || current is System.Security.SecurityException) {
                return true;
            }
            if (current.Message.IndexOf("access is denied", StringComparison.OrdinalIgnoreCase) >= 0) {
                return true;
            }
        }
        return false;
    }

}
