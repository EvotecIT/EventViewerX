using System.Diagnostics.Eventing.Reader;
using System.Net;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventReadinessEngine {
    [Fact]
    public void LocalReadinessCombinesTransportAndEffectivePolicyWithoutMutation() {
        var evidence = new FakeEvidenceProvider {
            TargetResult = LocalTargetResult(),
            ProbeResult = CreateProbe(EventLogProbeStatus.NoEvent, nativeQueryVerified: true),
            AuditOutcomes = EventAuditOutcome.Success
        };

        EventReadinessReport report = EventReadinessEngine.Evaluate(
            new EventReadinessRequest { Types = new[] { EventType.ADUserLogonNTLMv1 } },
            evidence,
            CancellationToken.None);

        Assert.True(report.IsReady);
        Assert.True(report.IsComplete);
        Assert.Contains(report.Checks, static check =>
            check.Layer == EventReadinessLayer.EventLogTransport &&
            check.Status == EventReadinessStatus.Warning &&
            check.EvidenceLevel == EventReadinessEvidenceLevel.Transport);
        Assert.Contains(report.Checks, static check =>
            check.RequirementKey == "audit:logon-success" &&
            check.Status == EventReadinessStatus.Pass &&
            check.EvidenceLevel == EventReadinessEvidenceLevel.Effective);
        Assert.Equal(1, evidence.AuditQueryCount);
    }

    [Fact]
    public void MissingEffectiveOutcomeFailsReadinessButOtherChecksStillRun() {
        var evidence = new FakeEvidenceProvider {
            TargetResult = LocalTargetResult(),
            ProbeResult = CreateProbe(EventLogProbeStatus.Ok, nativeQueryVerified: true),
            AuditOutcomes = EventAuditOutcome.Failure
        };

        EventReadinessReport report = EventReadinessEngine.Evaluate(
            new EventReadinessRequest { Types = new[] { EventType.ADUserLogonNTLMv1 } },
            evidence,
            CancellationToken.None);

        Assert.False(report.IsReady);
        Assert.True(report.IsComplete);
        Assert.Single(report.RequiredFailures);
        Assert.Contains(report.Checks, static check => check.Layer == EventReadinessLayer.EventLogTransport);
    }

    [Fact]
    public void CollectorKeepsRemoteAuditEvidenceUnknownAndDoesNotReadLocalPolicy() {
        var evidence = new FakeEvidenceProvider {
            ProbeResult = CreateProbe(EventLogProbeStatus.Ok, nativeQueryVerified: true),
            AuditOutcomes = EventAuditOutcome.Success
        };

        EventReadinessReport report = EventReadinessEngine.Evaluate(
            new EventReadinessRequest {
                Types = new[] { EventType.ADUserLogonNTLMv1 },
                Collector = "wec01.example.com"
            },
            evidence,
            CancellationToken.None);

        Assert.Null(report.TargetDiscovery);
        Assert.Equal(EventTargetKind.Collector, Assert.Single(report.Targets).Kind);
        Assert.False(report.IsReady);
        Assert.False(report.IsComplete);
        Assert.Equal(0, evidence.AuditQueryCount);
        Assert.Contains(report.UnknownRequiredChecks, static check =>
            check.Layer == EventReadinessLayer.AuditPolicy &&
            check.EvidenceLevel == EventReadinessEvidenceLevel.Unknown);
        Assert.All(evidence.ProbeCalls, static call => Assert.Equal("ForwardedEvents", call.LogName));
    }

    [Fact]
    public void AccessDeniedRemainsUnknownAndDoesNotStopRemainingLayers() {
        var evidence = new FakeEvidenceProvider {
            TargetResult = LocalTargetResult(),
            ProbeResult = CreateProbe(EventLogProbeStatus.AccessDenied, nativeQueryVerified: false),
            AuditOutcomes = EventAuditOutcome.Success
        };

        EventReadinessReport report = EventReadinessEngine.Evaluate(
            new EventReadinessRequest { Types = new[] { EventType.ADLdapBindingDetails } },
            evidence,
            CancellationToken.None);

        Assert.Contains(report.UnknownRequiredChecks, static check =>
            check.Layer == EventReadinessLayer.EventLogTransport &&
            check.DiagnosticKind == EventReadinessDiagnosticKind.AccessDenied);
        Assert.Contains(report.Checks, static check => check.Layer == EventReadinessLayer.Configuration);
    }

    [Fact]
    public void ScenarioAndExplicitTypesAreMutuallyExclusive() {
        var evidence = new FakeEvidenceProvider { TargetResult = LocalTargetResult() };

        Assert.Throws<ArgumentException>(() => EventReadinessEngine.Evaluate(
            new EventReadinessRequest {
                Types = new[] { EventType.ADUserLogonNTLMv1 },
                Scenario = EventReadinessScenario.AuthenticationMonitoring
            },
            evidence,
            CancellationToken.None));
    }

    [Fact]
    public void RemoteCredentialIsNotPassedToTheDefaultLocalProbe() {
        var evidence = new FakeEvidenceProvider {
            TargetResult = LocalTargetResult(),
            AuditOutcomes = EventAuditOutcome.Success
        };

        EventReadinessEngine.Evaluate(
            new EventReadinessRequest {
                Types = new[] { EventType.ADUserLogonNTLMv1 },
                EventLogCredential = new NetworkCredential("reader", "secret", "example")
            },
            evidence,
            CancellationToken.None);

        Assert.All(evidence.ProbeCalls, static call => Assert.Null(call.Credential));
    }

    [Fact]
    public void UnexpectedProbeFailureBecomesEvidenceAndDoesNotAbortTheReport() {
        var evidence = new FakeEvidenceProvider {
            TargetResult = LocalTargetResult(),
            ProbeException = new InvalidOperationException("probe failed"),
            AuditOutcomes = EventAuditOutcome.Success
        };

        EventReadinessReport report = EventReadinessEngine.Evaluate(
            new EventReadinessRequest { Types = new[] { EventType.ADUserLogonNTLMv1 } },
            evidence,
            CancellationToken.None);

        Assert.Contains(report.RequiredFailures, static check =>
            check.Layer == EventReadinessLayer.EventLogTransport &&
            check.Evidence.Contains("probe failed", StringComparison.Ordinal));
        Assert.Contains(report.Checks, static check =>
            check.Layer == EventReadinessLayer.AuditPolicy &&
            check.Status == EventReadinessStatus.Pass);
    }

    [Fact]
    public void UnexpectedAuditPolicyFailureBecomesUnknownEvidence() {
        var evidence = new FakeEvidenceProvider {
            TargetResult = LocalTargetResult(),
            AuditException = new InvalidOperationException("policy failed")
        };

        EventReadinessReport report = EventReadinessEngine.Evaluate(
            new EventReadinessRequest { Types = new[] { EventType.ADUserLogonNTLMv1 } },
            evidence,
            CancellationToken.None);

        Assert.Contains(report.UnknownRequiredChecks, static check =>
            check.Layer == EventReadinessLayer.AuditPolicy &&
            check.Evidence.Contains("policy failed", StringComparison.Ordinal));
    }

    [Fact]
    public void ActiveDirectoryDiscoveryProvesDomainControllerRoleWithoutLocalInspection() {
        var evidence = new FakeEvidenceProvider {
            TargetResult = DomainControllerTargetResult(),
            AuditOutcomes = EventAuditOutcome.Success | EventAuditOutcome.Failure
        };

        EventReadinessReport report = EventReadinessEngine.Evaluate(
            new EventReadinessRequest {
                Types = new[] { EventType.KerberosServiceTicket },
                TargetDiscovery = new EventTargetDiscoveryRequest {
                    Scope = EventTargetDiscoveryScope.CurrentDomain
                }
            },
            evidence,
            CancellationToken.None);

        EventReadinessCheckResult role = Assert.Single(report.Checks, static check =>
            check.RequirementKey == "target-role:domain-controller");
        Assert.Equal(EventReadinessStatus.Pass, role.Status);
        Assert.Equal(0, evidence.ConfigurationReadCount);
    }

    [Fact]
    public void LocalCollectorComparesRuntimeAgainstExplicitlyDiscoveredSources() {
        var evidence = new FakeEvidenceProvider {
            TargetResult = DomainControllerTargetResult(),
            ProbeResult = CreateProbe(EventLogProbeStatus.Ok, nativeQueryVerified: true),
            Subscription = new CollectorSubscriptionSnapshot {
                SubscriptionName = "EventViewerX-AD",
                MachineName = EventLogTarget.LocalMachineName,
                IsEnabled = true,
                HasXml = true,
                QueryCount = 1
            },
            CollectorReadiness = new CollectorReadinessStatus {
                MachineName = EventLogTarget.LocalMachineName,
                CollectorServiceInstalled = true,
                CollectorServiceRunning = true,
                CollectorServiceStartMode = "Automatic",
                WinRmServiceRunning = true,
                WinRmListenerAvailable = true,
                ForwardedEventsExists = true,
                ForwardedEventsEnabled = true
            },
            CollectorRuntime = new CollectorSubscriptionRuntimeStatus {
                SubscriptionName = "EventViewerX-AD",
                Status = "Active",
                LastErrorCode = 0,
                Sources = new[] {
                    new CollectorSubscriptionSourceRuntimeStatus {
                        Address = "dc01.example.com",
                        Status = "Active",
                        LastErrorCode = 0
                    }
                }
            }
        };

        EventReadinessReport report = EventReadinessEngine.Evaluate(
            new EventReadinessRequest {
                Types = new[] { EventType.ADUserLogonNTLMv1 },
                Collector = ".",
                SubscriptionName = "EventViewerX-AD",
                TargetDiscovery = new EventTargetDiscoveryRequest {
                    Scope = EventTargetDiscoveryScope.CurrentDomain
                }
            },
            evidence,
            CancellationToken.None);

        Assert.Contains(report.Checks, static check =>
            check.Layer == EventReadinessLayer.WindowsEventCollector &&
            check.Check == "ExpectedSourceRuntime" &&
            check.Target == "dc01.example.com" &&
            check.Status == EventReadinessStatus.Pass);
    }

    [Fact]
    public void UnknownSubscriptionEnabledStateRemainsUnknown() {
        var evidence = CreateCollectorEvidence();
        evidence.Subscription!.IsEnabled = null;

        EventReadinessReport report = EvaluateCollector(evidence);

        EventReadinessCheckResult enabled = Assert.Single(report.Checks, static check =>
            check.Check == "SubscriptionEnabled");
        Assert.Equal(EventReadinessStatus.Unknown, enabled.Status);
        Assert.Equal(EventReadinessEvidenceLevel.Unknown, enabled.EvidenceLevel);
        Assert.Equal(EventReadinessDiagnosticKind.NoEvidence, enabled.DiagnosticKind);
    }

    [Fact]
    public void EmptyCollectorRuntimeRemainsUnknownInsteadOfInventingFailure() {
        var evidence = CreateCollectorEvidence();
        evidence.CollectorRuntime = new CollectorSubscriptionRuntimeStatus {
            SubscriptionName = "EventViewerX-AD"
        };

        EventReadinessReport report = EvaluateCollector(evidence);

        EventReadinessCheckResult runtime = Assert.Single(report.Checks, static check =>
            check.Check == "SubscriptionRuntime");
        Assert.Equal(EventReadinessStatus.Unknown, runtime.Status);
        Assert.Equal(EventReadinessDiagnosticKind.NoEvidence, runtime.DiagnosticKind);
        Assert.DoesNotContain(report.Checks, static check => check.Check == "ExpectedSourceRuntime");
    }

    [Fact]
    public void BroadScenarioPartitionsNativeProbeFiltersInsteadOfExceedingXPathLimit() {
        var evidence = new FakeEvidenceProvider {
            TargetResult = LocalTargetResult(),
            ProbeResult = CreateProbe(EventLogProbeStatus.NoEvent, nativeQueryVerified: true),
            AuditOutcomes = EventAuditOutcome.Success | EventAuditOutcome.Failure
        };

        EventReadinessReport report = EventReadinessEngine.Evaluate(
            new EventReadinessRequest {
                Scenario = EventReadinessScenario.DailyActiveDirectoryReport,
                MaxEventsToScan = 4096
            },
            evidence,
            CancellationToken.None);

        Assert.NotEmpty(report.Checks);
        Assert.True(evidence.ProbeCalls.Count > EventTypeCatalog
            .GetSources(EventReadinessScenarioCatalog.GetTypes(EventReadinessScenario.DailyActiveDirectoryReport))
            .Count);
    }

    private static EventTargetDiscoveryResult LocalTargetResult() => new(
        EventTargetDiscoveryScope.LocalMachine,
        null,
        new[] { new EventTargetInfo(EventLogTarget.LocalMachineName, EventTargetKind.LocalMachine) },
        Array.Empty<EventTargetDomainResult>(),
        Array.Empty<EventTargetDiscoveryFailure>(),
        "LOCAL",
        TimeSpan.Zero);

    private static FakeEvidenceProvider CreateCollectorEvidence() => new() {
        TargetResult = DomainControllerTargetResult(),
        ProbeResult = CreateProbe(EventLogProbeStatus.Ok, nativeQueryVerified: true),
        Subscription = new CollectorSubscriptionSnapshot {
            SubscriptionName = "EventViewerX-AD",
            MachineName = EventLogTarget.LocalMachineName,
            IsEnabled = true,
            HasXml = true,
            QueryCount = 1
        },
        CollectorReadiness = new CollectorReadinessStatus {
            MachineName = EventLogTarget.LocalMachineName,
            CollectorServiceInstalled = true,
            CollectorServiceRunning = true,
            CollectorServiceStartMode = "Automatic",
            WinRmServiceRunning = true,
            WinRmListenerAvailable = true,
            ForwardedEventsExists = true,
            ForwardedEventsEnabled = true
        },
        CollectorRuntime = new CollectorSubscriptionRuntimeStatus {
            SubscriptionName = "EventViewerX-AD",
            Status = "Active",
            LastErrorCode = 0,
            Sources = new[] {
                new CollectorSubscriptionSourceRuntimeStatus {
                    Address = "dc01.example.com",
                    Status = "Active",
                    LastErrorCode = 0
                }
            }
        }
    };

    private static EventReadinessReport EvaluateCollector(FakeEvidenceProvider evidence) =>
        EventReadinessEngine.Evaluate(
            new EventReadinessRequest {
                Types = new[] { EventType.ADUserLogonNTLMv1 },
                Collector = ".",
                SubscriptionName = "EventViewerX-AD",
                TargetDiscovery = new EventTargetDiscoveryRequest {
                    Scope = EventTargetDiscoveryScope.CurrentDomain
                }
            },
            evidence,
            CancellationToken.None);

    private static EventTargetDiscoveryResult DomainControllerTargetResult() => new(
        EventTargetDiscoveryScope.CurrentDomain,
        null,
        new[] {
            new EventTargetInfo(
                "dc01.example.com",
                EventTargetKind.DomainController,
                "example.com",
                "example.com")
        },
        new[] {
            new EventTargetDomainResult(
                "example.com",
                "example.com",
                new[] {
                    new EventTargetInfo(
                        "dc01.example.com",
                        EventTargetKind.DomainController,
                        "example.com",
                        "example.com")
                },
                Array.Empty<EventTargetDiscoveryFailure>())
        },
        Array.Empty<EventTargetDiscoveryFailure>(),
        "DOMAIN",
        TimeSpan.Zero);

    private static EventLogProbeResult CreateProbe(EventLogProbeStatus status, bool nativeQueryVerified) => new(
        "Security",
        EventLogTarget.LocalMachineName,
        status == EventLogProbeStatus.Ok ? DateTime.UtcNow : null,
        status,
        status.ToString(),
        0,
        0,
        TimeSpan.FromMilliseconds(1),
        nativeQueryVerified);

    private sealed class FakeEvidenceProvider : IEventReadinessEvidenceProvider {
        internal EventTargetDiscoveryResult? TargetResult { get; set; }
        internal EventLogProbeResult ProbeResult { get; set; } = CreateProbe(EventLogProbeStatus.NoEvent, true);
        internal EventAuditOutcome AuditOutcomes { get; set; }
        internal Exception? ProbeException { get; set; }
        internal Exception? AuditException { get; set; }
        internal int AuditQueryCount { get; private set; }
        internal int ConfigurationReadCount { get; private set; }
        internal CollectorSubscriptionSnapshot? Subscription { get; set; }
        internal CollectorReadinessStatus CollectorReadiness { get; set; } = new();
        internal CollectorSubscriptionRuntimeStatus CollectorRuntime { get; set; } = new();
        internal List<(string LogName, string XPath, string? MachineName, NetworkCredential? Credential)> ProbeCalls { get; } = new();

        public EventTargetDiscoveryResult ResolveTargets(
            EventTargetDiscoveryRequest request,
            CancellationToken cancellationToken) => TargetResult ?? LocalTargetResult();

        public EventLogProbeResult Probe(
            string logName,
            string xpath,
            string? machineName,
            TimeSpan timeout,
            int maxEventsToScan,
            NetworkCredential? credential,
            EventLogAuthentication authentication,
            CancellationToken cancellationToken) {

            ProbeCalls.Add((logName, xpath, machineName, credential));
            if (ProbeException != null) {
                throw ProbeException;
            }
            return new EventLogProbeResult(
                logName,
                machineName ?? EventLogTarget.LocalMachineName,
                ProbeResult.EventTimeUtc,
                ProbeResult.Status,
                ProbeResult.Message,
                ProbeResult.EventsScanned,
                ProbeResult.RecordCount,
                ProbeResult.Duration,
                ProbeResult.NativeQueryVerified);
        }

        public EventLogProbeResult ProbeTypedCollectorSource(
            IReadOnlyList<EventType> types,
            EventSourceDefinition source,
            string collector,
            TimeSpan timeout,
            int maxEventsToScan,
            NetworkCredential? credential,
            EventLogAuthentication authentication,
            CancellationToken cancellationToken) {

            ProbeCalls.Add(("ForwardedEvents", "<managed-typed-collector>", collector, credential));
            if (ProbeException != null) {
                throw ProbeException;
            }
            return new EventLogProbeResult(
                "ForwardedEvents",
                collector,
                ProbeResult.EventTimeUtc,
                ProbeResult.Status,
                ProbeResult.Message,
                ProbeResult.EventsScanned,
                ProbeResult.RecordCount,
                ProbeResult.Duration,
                ProbeResult.NativeQueryVerified);
        }

        public IReadOnlyList<EffectiveAuditPolicyResult> QueryAuditPolicy(IReadOnlyList<Guid> subcategoryGuids) {
            AuditQueryCount++;
            if (AuditException != null) {
                throw AuditException;
            }
            return subcategoryGuids.Select(guid => new EffectiveAuditPolicyResult(
                guid,
                true,
                AuditOutcomes,
                0,
                null)).ToArray();
        }

        public EventReadinessConfigurationEvidence ReadLocalConfiguration(string requirementKey) {
            ConfigurationReadCount++;
            return new EventReadinessConfigurationEvidence(
                EventReadinessStatus.Pass,
                "Configured",
                string.Empty);
        }

        public CollectorSubscriptionSnapshot? ReadCollectorSubscription(string name, string? machineName) =>
            Subscription;

        public CollectorReadinessStatus ReadLocalCollectorReadiness(CancellationToken cancellationToken) =>
            CollectorReadiness;

        public CollectorSubscriptionRuntimeStatus ReadLocalCollectorRuntime(
            string subscriptionName,
            CancellationToken cancellationToken) => CollectorRuntime;
    }
}
