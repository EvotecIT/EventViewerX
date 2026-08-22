using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Net;
using EventViewerX.Native;
using Microsoft.Win32;

namespace EventViewerX;

internal sealed class EventReadinessEvidenceProvider : IEventReadinessEvidenceProvider {
    public EventTargetDiscoveryResult ResolveTargets(
        EventTargetDiscoveryRequest request,
        CancellationToken cancellationToken) => EventTargetResolver.Resolve(request, cancellationToken);

    public EventLogProbeResult Probe(
        string logName,
        string xpath,
        string? machineName,
        TimeSpan timeout,
        int maxEventsToScan,
        NetworkCredential? credential,
        EventLogAuthentication authentication,
        CancellationToken cancellationToken) => EventLogProbe.ProbeLatestEvent(
            logName,
            xpath,
            machineName,
            timeout,
            maxEventsToScan,
            credential,
            authentication,
            cancellationToken);

    public EventLogProbeResult ProbeTypedCollectorSource(
        IReadOnlyList<EventType> types,
        EventSourceDefinition source,
        string collector,
        TimeSpan timeout,
        int maxEventsToScan,
        NetworkCredential? credential,
        EventLogAuthentication authentication,
        CancellationToken cancellationToken) => ProbeTypedCollectorSourceAsync(
            types,
            source,
            collector,
            timeout,
            maxEventsToScan,
            credential,
            authentication,
            cancellationToken).GetAwaiter().GetResult();

    public EventLogProbeResult ProbeTypedDirectSource(
        IReadOnlyList<EventType> types,
        EventSourceDefinition source,
        string? machineName,
        TimeSpan timeout,
        int maxEventsToScan,
        NetworkCredential? credential,
        EventLogAuthentication authentication,
        CancellationToken cancellationToken) => ProbeTypedSourceAsync(
            types,
            source,
            machineName,
            collector: false,
            timeout,
            maxEventsToScan,
            credential,
            authentication,
            cancellationToken).GetAwaiter().GetResult();

    public IReadOnlyList<EffectiveAuditPolicyResult> QueryAuditPolicy(
        IReadOnlyList<Guid> subcategoryGuids) => AuditPolicyReader.Query(subcategoryGuids);

    public ChannelPolicy? ReadChannelPolicy(
        string logName,
        string? machineName,
        TimeSpan timeout,
        NetworkCredential? credential,
        EventLogAuthentication authentication,
        CancellationToken cancellationToken) => EventLogChannelPolicyService.Get(
            logName,
            new EventLogCatalogQuery {
                MachineName = machineName,
                Credential = credential,
                Authentication = authentication,
                ConnectionTimeoutMilliseconds = checked((int)Math.Ceiling(timeout.TotalMilliseconds))
            },
            cancellationToken);

    public EventReadinessConfigurationEvidence ReadLocalConfiguration(string requirementKey) {
        if (string.Equals(
                requirementKey,
                "target-role:domain-controller",
                StringComparison.OrdinalIgnoreCase)) {
            return ReadLocalDomainControllerRole();
        }
        if (string.Equals(
                requirementKey,
                "target-role:certification-authority",
                StringComparison.OrdinalIgnoreCase)) {
            return ReadLocalCertificateAuthorityRole();
        }
        if (string.Equals(
                requirementKey,
                "configuration:certification-authority-audit-filter-requests",
                StringComparison.OrdinalIgnoreCase)) {
            return ReadLocalCertificateAuthorityAuditFilter();
        }
        if (!string.Equals(
                requirementKey,
                "configuration:ntds-ldap-interface-events-2",
                StringComparison.OrdinalIgnoreCase)) {
            return new EventReadinessConfigurationEvidence(
                EventReadinessStatus.Unknown,
                "This requirement needs an explicit object or provider-specific inspection scope.",
                "Inspect the requirement documentation and supply the affected object scope.",
                EventReadinessDiagnosticKind.NoEvidence);
        }
        try {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Services\NTDS\Diagnostics",
                writable: false);
            object? value = key?.GetValue("16 LDAP Interface Events");
            if (value == null) {
                return new EventReadinessConfigurationEvidence(
                    EventReadinessStatus.Fail,
                    "NTDS diagnostic value '16 LDAP Interface Events' is absent.",
                    "On each intended domain controller, set the diagnostic value to 2 only after reviewing volume and privacy impact.",
                    EventReadinessDiagnosticKind.Missing);
            }
            int level = Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
            return level >= 2
                ? new EventReadinessConfigurationEvidence(
                    EventReadinessStatus.Pass,
                    $"NTDS LDAP Interface Events diagnostic level is {level}.",
                    string.Empty)
                : new EventReadinessConfigurationEvidence(
                    EventReadinessStatus.Fail,
                    $"NTDS LDAP Interface Events diagnostic level is {level}; event 2889 requires level 2 or higher.",
                    "Set the diagnostic value to 2 only after reviewing volume and privacy impact.",
                    EventReadinessDiagnosticKind.InvalidConfiguration);
        } catch (UnauthorizedAccessException exception) {
            return new EventReadinessConfigurationEvidence(
                EventReadinessStatus.Unknown,
                "The current identity cannot read the NTDS diagnostic registry value: " + exception.Message,
                "Run the assessment with an identity allowed to read the local NTDS diagnostics key.",
                EventReadinessDiagnosticKind.AccessDenied);
        } catch (Exception exception) {
            return new EventReadinessConfigurationEvidence(
                EventReadinessStatus.Unknown,
                "The NTDS diagnostic registry value could not be inspected: " + exception.Message,
                "Verify that the target is a domain controller and inspect the setting manually.",
                EventReadinessDiagnosticKind.Error);
        }
    }

    private static async Task<EventLogProbeResult> ProbeTypedCollectorSourceAsync(
        IReadOnlyList<EventType> types,
        EventSourceDefinition source,
        string collector,
        TimeSpan timeout,
        int maxEventsToScan,
        NetworkCredential? credential,
        EventLogAuthentication authentication,
        CancellationToken cancellationToken) => await ProbeTypedSourceAsync(
            types,
            source,
            collector,
            collector: true,
            timeout,
            maxEventsToScan,
            credential,
            authentication,
            cancellationToken).ConfigureAwait(false);

    private static async Task<EventLogProbeResult> ProbeTypedSourceAsync(
        IReadOnlyList<EventType> types,
        EventSourceDefinition source,
        string? target,
        bool collector,
        TimeSpan timeout,
        int maxEventsToScan,
        NetworkCredential? credential,
        EventLogAuthentication authentication,
        CancellationToken cancellationToken) {

        var stopwatch = Stopwatch.StartNew();
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(timeout);
        var executionInfo = new EventTypeQueryExecutionInfo();
        var query = new EventTypeQuery(types) {
            MachineNames = new[] { target },
            CollectorLogName = collector ? "ForwardedEvents" : null,
            SourceLogName = source.LogName,
            SourceEventIds = source.EventIds,
            MaxEvents = 0,
            MaxCandidates = maxEventsToScan,
            Credential = EventLogTarget.IsLocalMachine(target) ? null : credential,
            Authentication = authentication,
            ContinueOnRemoteFailure = false,
            RemoteConnectionTimeoutMilliseconds = checked((int)Math.Ceiling(timeout.TotalMilliseconds)),
            RemoteReadTimeoutMilliseconds = checked((int)Math.Ceiling(timeout.TotalMilliseconds))
        };
        try {
            await using IAsyncEnumerator<EventTypeRecord> enumerator = EventTypeEngine
                .ReadAsync(query, executionInfo, timeoutCancellation.Token)
                .GetAsyncEnumerator(timeoutCancellation.Token);
            bool typedMatchFound = false;
            DateTime? eventTimeUtc = null;
            while (await enumerator.MoveNextAsync().ConfigureAwait(false)) {
                typedMatchFound = true;
                if (enumerator.Current.TimeCreated != DateTime.MinValue) {
                    eventTimeUtc = enumerator.Current.TimeCreated.ToUniversalTime();
                    break;
                }
            }
            EventLogQueryTargetFailure? failure = executionInfo.TargetFailures.FirstOrDefault();
            if (failure != null) {
                EventLogProbeStatus failureStatus = failure.Kind switch {
                    EventLogRemoteQueryFailureKind.AccessDenied => EventLogProbeStatus.AccessDenied,
                    EventLogRemoteQueryFailureKind.Timeout => EventLogProbeStatus.Timeout,
                    EventLogRemoteQueryFailureKind.HostUnavailable => EventLogProbeStatus.HostUnavailable,
                    _ => EventLogProbeStatus.Error
                };
                return new EventLogProbeResult(
                    collector ? "ForwardedEvents" : source.LogName,
                    target ?? EventLogTarget.LocalMachineName,
                    null,
                    failureStatus,
                    failure.Message,
                    checked((int)executionInfo.EventsScanned),
                    null,
                    stopwatch.Elapsed,
                    nativeQueryVerified: false);
            }
            EventLogProbeStatus status = ClassifyTypedProbe(
                eventTimeUtc,
                typedMatchFound,
                executionInfo.ScanLimitReached);
            return new EventLogProbeResult(
                collector ? "ForwardedEvents" : source.LogName,
                target ?? EventLogTarget.LocalMachineName,
                eventTimeUtc,
                status,
                eventTimeUtc.HasValue
                    ? collector
                        ? "A matching typed ForwardedEvents record was observed through the managed-safe collector path."
                        : "A matching typed source record was observed through the managed projection path."
                    : executionInfo.ScanLimitReached
                        ? typedMatchFound
                            ? $"The first {executionInfo.EventsScanned} candidates included typed matches but none had a usable timestamp, and additional candidates remain."
                            : $"The first {executionInfo.EventsScanned} {(collector ? "ForwardedEvents" : source.LogName)} candidates did not match this source requirement."
                        : typedMatchFound
                            ? $"All {executionInfo.EventsScanned} candidates were scanned; typed matches were found, but none had a usable timestamp."
                            : $"No matching typed {(collector ? "ForwardedEvents" : source.LogName)} record was observed.",
                checked((int)executionInfo.EventsScanned),
                null,
                stopwatch.Elapsed,
                nativeQueryVerified: true);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (OperationCanceledException) {
            return new EventLogProbeResult(
                collector ? "ForwardedEvents" : source.LogName,
                target ?? EventLogTarget.LocalMachineName,
                null,
                EventLogProbeStatus.Timeout,
                $"Timed out after {timeout.TotalMilliseconds:F0} ms.",
                checked((int)executionInfo.EventsScanned),
                null,
                stopwatch.Elapsed,
                nativeQueryVerified: false);
        } catch (Exception exception) {
            return new EventLogProbeResult(
                collector ? "ForwardedEvents" : source.LogName,
                target ?? EventLogTarget.LocalMachineName,
                null,
                EventLogProbe.ClassifyFailure(target, exception),
                exception.Message,
                checked((int)executionInfo.EventsScanned),
                null,
                stopwatch.Elapsed,
                nativeQueryVerified: false);
        }
    }

    internal static EventLogProbeStatus ClassifyTypedProbe(
        DateTime? eventTimeUtc,
        bool typedMatchFound,
        bool scanLimitReached) => eventTimeUtc.HasValue
            ? EventLogProbeStatus.Ok
            : scanLimitReached
                ? EventLogProbeStatus.LimitReached
                : typedMatchFound
                    ? EventLogProbeStatus.NoUsableTimestamp
                    : EventLogProbeStatus.NoEvent;

    public CollectorSubscriptionSnapshot? ReadCollectorSubscription(
        string name,
        string? machineName,
        TimeSpan timeout,
        CancellationToken cancellationToken) {

        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(machineName) || EventLogTarget.IsLocalMachine(machineName)) {
            return CollectorSubscriptionManager.GetCollectorSubscriptionSnapshot(name, machineName);
        }
        return RunBoundedRemoteInspection(
            () => CollectorSubscriptionManager.GetCollectorSubscriptionSnapshot(name, machineName),
            timeout,
            cancellationToken);
    }

    internal static T RunBoundedRemoteInspection<T>(
        Func<T> operation,
        TimeSpan timeout,
        CancellationToken cancellationToken) {

        if (operation == null) {
            throw new ArgumentNullException(nameof(operation));
        }
        if (timeout <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }
        cancellationToken.ThrowIfCancellationRequested();
        int timeoutMilliseconds = timeout.TotalMilliseconds >= int.MaxValue
            ? int.MaxValue
            : Math.Max(1, (int)Math.Ceiling(timeout.TotalMilliseconds));
        string timeoutMessage =
            $"Remote subscription registry inspection timed out after {timeout.TotalMilliseconds:F0} ms.";
        return BoundedNativeOperation.Execute(
            operation,
            timeoutMilliseconds,
            timeoutMessage,
            cancellationToken);
    }

    public CollectorReadinessStatus ReadLocalCollectorReadiness(CancellationToken cancellationToken) =>
        CollectorSubscriptionManager.GetCollectorReadiness(cancellationToken);

    public CollectorSubscriptionRuntimeStatus ReadLocalCollectorRuntime(
        string subscriptionName,
        CancellationToken cancellationToken) =>
        CollectorSubscriptionManager.GetCollectorSubscriptionRuntimeStatus(subscriptionName, cancellationToken);

    private static EventReadinessConfigurationEvidence ReadLocalDomainControllerRole() {
        try {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\ProductOptions",
                writable: false);
            string? productType = key?.GetValue("ProductType") as string;
            if (string.IsNullOrWhiteSpace(productType)) {
                return new EventReadinessConfigurationEvidence(
                    EventReadinessStatus.Unknown,
                    "The local Windows product role could not be read.",
                    "Confirm that the local event source is a domain controller or select domain controllers explicitly.",
                    EventReadinessDiagnosticKind.NoEvidence);
            }
            return string.Equals(productType, "LanmanNT", StringComparison.OrdinalIgnoreCase)
                ? new EventReadinessConfigurationEvidence(
                    EventReadinessStatus.Pass,
                    "The local Windows product role is Domain Controller.",
                    string.Empty)
                : new EventReadinessConfigurationEvidence(
                    EventReadinessStatus.Fail,
                    $"The local Windows product role is '{productType}', not Domain Controller.",
                    "Select domain controllers explicitly with -ActiveDirectory, or assess a Windows Event Collector that receives their events.",
                    EventReadinessDiagnosticKind.InvalidConfiguration);
        } catch (UnauthorizedAccessException exception) {
            return new EventReadinessConfigurationEvidence(
                EventReadinessStatus.Unknown,
                "The current identity cannot inspect the local Windows product role: " + exception.Message,
                "Run with an identity allowed to read the local ProductOptions registry key.",
                EventReadinessDiagnosticKind.AccessDenied);
        } catch (Exception exception) {
            return new EventReadinessConfigurationEvidence(
                EventReadinessStatus.Unknown,
                "The local Windows product role could not be inspected: " + exception.Message,
                "Confirm the source role manually or select domain controllers explicitly.",
                EventReadinessDiagnosticKind.Error);
        }
    }

    private static EventReadinessConfigurationEvidence ReadLocalCertificateAuthorityRole() {
        try {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Services\CertSvc\Configuration",
                writable: false);
            string? activeAuthority = key?.GetValue("Active") as string;
            return key != null && !string.IsNullOrWhiteSpace(activeAuthority)
                ? new EventReadinessConfigurationEvidence(
                    EventReadinessStatus.Pass,
                    $"The local Active Directory Certificate Services authority is '{activeAuthority}'.",
                    string.Empty)
                : new EventReadinessConfigurationEvidence(
                    EventReadinessStatus.Fail,
                    "No active local Active Directory Certificate Services Certification Authority was found.",
                    "Assess the Certification Authority that emits the selected certificate events.",
                    EventReadinessDiagnosticKind.Missing);
        } catch (UnauthorizedAccessException exception) {
            return new EventReadinessConfigurationEvidence(
                EventReadinessStatus.Unknown,
                "The current identity cannot inspect the local Certification Authority role: " + exception.Message,
                "Run with an identity allowed to read the local CertSvc configuration registry key.",
                EventReadinessDiagnosticKind.AccessDenied);
        } catch (Exception exception) {
            return new EventReadinessConfigurationEvidence(
                EventReadinessStatus.Unknown,
                "The local Certification Authority role could not be inspected: " + exception.Message,
                "Confirm the source role manually or assess the Certification Authority directly.",
                EventReadinessDiagnosticKind.Error);
        }
    }

    private static EventReadinessConfigurationEvidence ReadLocalCertificateAuthorityAuditFilter() {
        const int IssueAndManageCertificateRequests = 4;
        try {
            using RegistryKey? configuration = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Services\CertSvc\Configuration",
                writable: false);
            string? activeAuthority = configuration?.GetValue("Active") as string;
            if (string.IsNullOrWhiteSpace(activeAuthority)) {
                return new EventReadinessConfigurationEvidence(
                    EventReadinessStatus.Fail,
                    "No active local Certification Authority configuration was found.",
                    "Assess the Certification Authority that emits the selected certificate events.",
                    EventReadinessDiagnosticKind.Missing);
            }
            using RegistryKey? authority = configuration!.OpenSubKey(activeAuthority, writable: false);
            object? value = authority?.GetValue("AuditFilter");
            if (value == null) {
                return new EventReadinessConfigurationEvidence(
                    EventReadinessStatus.Fail,
                    $"Certification Authority '{activeAuthority}' has no readable AuditFilter value.",
                    "Enable 'Issue and manage certificate requests' in the Certification Authority auditing properties.",
                    EventReadinessDiagnosticKind.Missing);
            }
            int filter = Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
            return (filter & IssueAndManageCertificateRequests) != 0
                ? new EventReadinessConfigurationEvidence(
                    EventReadinessStatus.Pass,
                    $"Certification Authority '{activeAuthority}' AuditFilter includes issue/manage certificate requests (value {filter}).",
                    string.Empty)
                : new EventReadinessConfigurationEvidence(
                    EventReadinessStatus.Fail,
                    $"Certification Authority '{activeAuthority}' AuditFilter value {filter} does not include issue/manage certificate requests (bit 4).",
                    "Enable 'Issue and manage certificate requests' in the Certification Authority auditing properties.",
                    EventReadinessDiagnosticKind.InvalidConfiguration);
        } catch (UnauthorizedAccessException exception) {
            return new EventReadinessConfigurationEvidence(
                EventReadinessStatus.Unknown,
                "The current identity cannot inspect the Certification Authority audit filter: " + exception.Message,
                "Run with an identity allowed to read the local CertSvc configuration registry key.",
                EventReadinessDiagnosticKind.AccessDenied);
        } catch (Exception exception) {
            return new EventReadinessConfigurationEvidence(
                EventReadinessStatus.Unknown,
                "The Certification Authority audit filter could not be inspected: " + exception.Message,
                "Inspect the Certification Authority auditing properties manually.",
                EventReadinessDiagnosticKind.Error);
        }
    }
}
