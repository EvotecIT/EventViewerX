using EventViewerX.Rules.ActiveDirectory;
using DnsClientX;
using System.Runtime.CompilerServices;
using Xunit;

namespace EventViewerX.Tests;

public class TestNamedEventDnsEnrichment {
    [Fact]
    public void SmbProjectionDoesNotPerformDnsInItsConstructor() {
        SMBServerAudit projected = CreateSmbEvent("\\\\10.0.0.5");

        Assert.Equal(ReverseDnsResolutionStatus.NotRequested, projected.ClientDnsResolutionStatus);
        Assert.Equal(string.Empty, projected.ClientDNSName);
    }

    [Fact]
    public async Task SmbEnrichmentUsesOneNormalizedPtrQueryAndRetainsTheEvent() {
        SMBServerAudit projected = CreateSmbEvent("\\\\10.0.0.5");
        int calls = 0;
        string? queriedAddress = null;
        using var enricher = new EventEnricher(
            new EventEnrichmentOptions { ResolveDns = true },
            (address, _) => {
                calls++;
                queriedAddress = address;
                return Task.FromResult(new DnsResponse {
                    Status = DnsResponseCode.NoError,
                    Answers = new[] {
                        new DnsAnswer { Type = DnsRecordType.PTR, DataRaw = "client.ad.evotec.xyz." },
                        new DnsAnswer { Type = DnsRecordType.PTR, DataRaw = "CLIENT.ad.evotec.xyz." }
                    }
                });
            });

        await enricher.EnrichAsync(projected, CancellationToken.None);

        Assert.Equal(1, calls);
        Assert.Equal("10.0.0.5", queriedAddress);
        Assert.Equal("client.ad.evotec.xyz", projected.ClientDNSName, ignoreCase: true);
        Assert.Equal(ReverseDnsResolutionStatus.Resolved, projected.ClientDnsResolutionStatus);
        Assert.Equal(string.Empty, projected.ClientDnsResolutionError);
    }

    [Fact]
    public async Task ExistingHostNameDoesNotIssueAPtrQuery() {
        SMBServerAudit projected = CreateSmbEvent("client.ad.evotec.xyz.");
        int calls = 0;
        using var enricher = new EventEnricher(
            new EventEnrichmentOptions { ResolveDns = true },
            (_, _) => {
                calls++;
                return Task.FromResult(new DnsResponse());
            });

        await enricher.EnrichAsync(projected, CancellationToken.None);

        Assert.Equal(0, calls);
        Assert.Equal("client.ad.evotec.xyz", projected.ClientDNSName);
        Assert.Equal(ReverseDnsResolutionStatus.AlreadyNamed, projected.ClientDnsResolutionStatus);
    }

    [Fact]
    public async Task DnsTimeoutIsReportedWithoutDroppingTheProjectedEvent() {
        SMBServerAudit projected = CreateSmbEvent("10.0.0.6");
        using var enricher = new EventEnricher(
            new EventEnrichmentOptions { ResolveDns = true },
            (_, _) => Task.FromException<DnsResponse>(new TimeoutException("PTR lookup timed out.")));

        await enricher.EnrichAsync(projected, CancellationToken.None);

        Assert.Equal(ReverseDnsResolutionStatus.TimedOut, projected.ClientDnsResolutionStatus);
        Assert.Equal("PTR lookup timed out.", projected.ClientDnsResolutionError);
        Assert.Equal(string.Empty, projected.ClientDNSName);
    }

    [Fact]
    public async Task ResolverCancellationIsReportedAsTimeoutWhenCallerDidNotCancel() {
        SMBServerAudit projected = CreateSmbEvent("10.0.0.8");
        using var resolverTimeout = new CancellationTokenSource();
        resolverTimeout.Cancel();
        using var enricher = new EventEnricher(
            new EventEnrichmentOptions { ResolveDns = true },
            (_, _) => Task.FromCanceled<DnsResponse>(resolverTimeout.Token));

        await enricher.EnrichAsync(projected, CancellationToken.None);

        Assert.Equal(ReverseDnsResolutionStatus.TimedOut, projected.ClientDnsResolutionStatus);
        Assert.NotEmpty(projected.ClientDnsResolutionError);
        Assert.Equal(string.Empty, projected.ClientDNSName);
    }

    [Fact]
    public async Task ResolverFailureIsVisibleOnTheEvent() {
        SMBServerAudit projected = CreateSmbEvent("10.0.0.9");
        using var enricher = new EventEnricher(
            new EventEnrichmentOptions { ResolveDns = true },
            (_, _) => throw new InvalidOperationException("No usable system resolver."));

        await enricher.EnrichAsync(projected, CancellationToken.None);

        Assert.Equal(ReverseDnsResolutionStatus.Failed, projected.ClientDnsResolutionStatus);
        Assert.Equal("No usable system resolver.", projected.ClientDnsResolutionError);
    }

    [Fact]
    public async Task FailedResponseAfterCallerCancellationDoesNotCrossTheCheckpointBoundary() {
        SMBServerAudit projected = CreateSmbEvent("10.0.0.10");
        using var cancellation = new CancellationTokenSource();
        using var enricher = new EventEnricher(
            new EventEnrichmentOptions { ResolveDns = true },
            (_, _) => {
                cancellation.Cancel();
                return Task.FromResult(new DnsResponse {
                    Status = DnsResponseCode.ServerFailure,
                    Error = "Dependency converted cancellation into a response."
                });
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => enricher.EnrichAsync(projected, cancellation.Token));

        Assert.Equal(ReverseDnsResolutionStatus.Cancelled, projected.ClientDnsResolutionStatus);
    }

    [Fact]
    public async Task DnsRequestsAreDeduplicatedAndConcurrencyIsBounded() {
        int calls = 0;
        int active = 0;
        int maximumActive = 0;
        using var enricher = new EventEnricher(
            new EventEnrichmentOptions {
                ResolveDns = true,
                DnsMaxConcurrency = 2,
                DnsTimeoutMilliseconds = 5000
            },
            async (address, token) => {
                Interlocked.Increment(ref calls);
                int nowActive = Interlocked.Increment(ref active);
                int observedMaximum;
                do {
                    observedMaximum = Volatile.Read(ref maximumActive);
                } while (nowActive > observedMaximum &&
                         Interlocked.CompareExchange(ref maximumActive, nowActive, observedMaximum) != observedMaximum);
                try {
                    await Task.Delay(40, token);
                    return new DnsResponse {
                        Status = DnsResponseCode.NoError,
                        Answers = new[] {
                            new DnsAnswer { Type = DnsRecordType.PTR, DataRaw = address + ".example.test." }
                        }
                    };
                } finally {
                    Interlocked.Decrement(ref active);
                }
            });
        SMBServerAudit[] projected = {
            CreateSmbEvent("10.0.0.11"),
            CreateSmbEvent("10.0.0.12"),
            CreateSmbEvent("10.0.0.13"),
            CreateSmbEvent("10.0.0.11")
        };

        await Task.WhenAll(projected.Select(item => enricher.EnrichAsync(item, CancellationToken.None)));

        Assert.Equal(3, calls);
        Assert.Equal(2, maximumActive);
        Assert.All(projected, item => Assert.Equal(ReverseDnsResolutionStatus.Resolved, item.ClientDnsResolutionStatus));
    }

    [Fact]
    public async Task ConfiguredTimeoutBoundsTheWholeResolverCall() {
        SMBServerAudit projected = CreateSmbEvent("10.0.0.14");
        using var enricher = new EventEnricher(
            new EventEnrichmentOptions {
                ResolveDns = true,
                DnsTimeoutMilliseconds = 30
            },
            async (_, token) => {
                await Task.Delay(TimeSpan.FromSeconds(30), token);
                return new DnsResponse();
            });

        Task enrichment = enricher.EnrichAsync(
            projected,
            CancellationToken.None);

        await enrichment.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(ReverseDnsResolutionStatus.TimedOut, projected.ClientDnsResolutionStatus);
    }

    [Fact]
    public async Task CancelAwareResolverReleasesItsLeaseAfterTimeout() {
        int calls = 0;
        using var enricher = new EventEnricher(
            new EventEnrichmentOptions {
                ResolveDns = true,
                DnsMaxConcurrency = 1,
                DnsTimeoutMilliseconds = 100
            },
            async (address, token) => {
                int call = Interlocked.Increment(
                    ref calls);
                if (call == 1) {
                    await Task.Delay(TimeSpan.FromSeconds(30), token);
                }
                return new DnsResponse {
                    Status = DnsResponseCode.NoError,
                    Answers = new[] {
                        new DnsAnswer {
                            Type = DnsRecordType.PTR,
                            DataRaw = address + ".example.test."
                        }
                    }
                };
            });
        SMBServerAudit first =
            CreateSmbEvent("10.0.0.31");
        SMBServerAudit second =
            CreateSmbEvent("10.0.0.32");

        await enricher.EnrichAsync(
            first,
            CancellationToken.None);
        await enricher.EnrichAsync(
            second,
            CancellationToken.None).WaitAsync(
                TimeSpan.FromSeconds(5));

        Assert.Equal(
            ReverseDnsResolutionStatus.TimedOut,
            first.ClientDnsResolutionStatus);
        Assert.Equal(
            ReverseDnsResolutionStatus.Resolved,
            second.ClientDnsResolutionStatus);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ConcurrentEnrichmentPreservesSourceOrder() {
        var projectedRecordIds = new List<long?>();
        EventObject[] sourceEvents = {
            CreateSmbEventObject("10.0.0.21", 21),
            CreateSmbEventObject("10.0.0.22", 22),
            CreateSmbEventObject("10.0.0.23", 23)
        };
        using var enricher = new EventEnricher(
            new EventEnrichmentOptions {
                ResolveDns = true,
                DnsMaxConcurrency = 3,
                DnsTimeoutMilliseconds = 1000
            },
            async (address, token) => {
                int lastOctet = int.Parse(address.Substring(address.LastIndexOf('.') + 1));
                await Task.Delay((24 - lastOctet) * 20, token);
                return new DnsResponse { Status = DnsResponseCode.NoError };
            });

        await foreach (EventTypeEngine.EventTypeProjection projection in EventTypeEngine.ProjectCandidatesInOrderAsync(
                           YieldEventsAsync(sourceEvents),
                           new List<EventType> { EventType.ADSMBServerAuditV1 },
                           enricher,
                           () => true,
                           CancellationToken.None)) {
            projectedRecordIds.Add(projection.Source.RecordId);
        }

        Assert.Equal(new long?[] { 21, 22, 23 }, projectedRecordIds);
    }

    [Fact]
    public async Task DependencySwallowedCancellationNeverYieldsAProjection() {
        bool projected = false;
        using var cancellation = new CancellationTokenSource();
        using var enricher = new EventEnricher(
            new EventEnrichmentOptions { ResolveDns = true },
            (_, _) => {
                cancellation.Cancel();
                return Task.FromResult(new DnsResponse {
                    Status = DnsResponseCode.ServerFailure,
                    Error = "Dependency converted cancellation into a response."
                });
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => {
            await foreach (EventTypeEngine.EventTypeProjection _ in EventTypeEngine.ProjectCandidatesInOrderAsync(
                               YieldEventsAsync(new[] { CreateSmbEventObject("10.0.0.24", 24) }),
                               new List<EventType> { EventType.ADSMBServerAuditV1 },
                               enricher,
                               () => true,
                               cancellation.Token)) {
                projected = true;
            }
        });

        Assert.False(projected);
    }

    [Fact]
    public void ResultLimitProofDoesNotAdvanceTheTypedCheckpointObserver() {
        EventObject firstSource = CreateSmbEventObject("10.0.0.21", 21);
        EventObject secondSource = CreateSmbEventObject("10.0.0.22", 22);
        var observedRecordIds = new List<long?>();
        var info = new EventTypeQueryExecutionInfo();
        info.Reset(maxEventsScanned: 0);
        long emitted = 0;

        EventTypeEngine.EventTypeProjectionDisposition first = EventTypeEngine.ClassifyProjectionForEmission(
            new EventTypeEngine.EventTypeProjection(firstSource, new SMBServerAudit(firstSource)),
            typedPredicate: null,
            resultPredicate: null,
            maxEvents: 1,
            ref emitted,
            info,
            source => observedRecordIds.Add(source.RecordId));
        EventTypeEngine.EventTypeProjectionDisposition second = EventTypeEngine.ClassifyProjectionForEmission(
            new EventTypeEngine.EventTypeProjection(secondSource, new SMBServerAudit(secondSource)),
            typedPredicate: null,
            resultPredicate: null,
            maxEvents: 1,
            ref emitted,
            info,
            source => observedRecordIds.Add(source.RecordId));

        Assert.Equal(EventTypeEngine.EventTypeProjectionDisposition.Emit, first);
        Assert.Equal(EventTypeEngine.EventTypeProjectionDisposition.Stop, second);
        Assert.Equal(new long?[] { 21 }, observedRecordIds);
        Assert.Equal(1, info.EventsEmitted);
        Assert.True(info.ResultLimitReached);
    }

    [Fact]
    public void ParameterlessReverseDnsMethodRemainsAvailableForBinaryCompatibility() {
        Type type = typeof(SMBServerAudit);

        Assert.NotNull(type.GetMethod(nameof(SMBServerAudit.ResolveClientDnsNameAsync), Type.EmptyTypes));
        Assert.NotNull(type.GetMethod(
            nameof(SMBServerAudit.ResolveClientDnsNameAsync),
            new[] { typeof(CancellationToken) }));
    }

    [Fact]
    public async Task CallerCancellationStillStopsEnrichment() {
        SMBServerAudit projected = CreateSmbEvent("10.0.0.7");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        using var enricher = new EventEnricher(
            new EventEnrichmentOptions { ResolveDns = true },
            (_, token) => Task.FromCanceled<DnsResponse>(token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => enricher.EnrichAsync(projected, cts.Token));
        Assert.Equal(ReverseDnsResolutionStatus.Cancelled, projected.ClientDnsResolutionStatus);
    }

    [Fact]
    public void ProjectionFailureIsReportedInsteadOfSilentlyReturningNull() {
        EventObject eventObject = CreateEventObject(
            eventId: 4713,
            logName: "Security",
            providerName: "Microsoft-Windows-Security-Auditing",
            data: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                ["KerberosPolicyChange"] = "KerProxy:0xFFFFFFFFFFFFFFFFFFFFFFFF"
            });

        EventRuleProjectionException exception = Assert.Throws<EventRuleProjectionException>(() =>
            EventTypeCatalog.CreateEventRule(eventObject, new List<EventType> { EventType.KerberosPolicyChange }));

        Assert.Equal(4713, exception.EventId);
        Assert.Equal(42L, exception.RecordId);
        Assert.Equal("Security", exception.LogName);
        Assert.Contains(exception.RuleNames, name => name.Contains("KerberosPolicyChange", StringComparison.Ordinal));
        Assert.Contains("projection failed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static SMBServerAudit CreateSmbEvent(string clientAddress) {
        return new SMBServerAudit(CreateSmbEventObject(clientAddress, 42));
    }

    private static EventObject CreateSmbEventObject(string clientAddress, long recordId) {
        return CreateEventObject(
            eventId: 3000,
            logName: "Microsoft-Windows-SMBServer/Audit",
            providerName: "Microsoft-Windows-SMBServer",
            data: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                ["ClientName"] = clientAddress
            },
            recordId: recordId);
    }

    private static EventObject CreateEventObject(
        int eventId,
        string logName,
        string providerName,
        Dictionary<string, string> data,
        long recordId = 42) {
        var eventObject = (EventObject)RuntimeHelpers.GetUninitializedObject(typeof(EventObject));
        SetSnapshotProperty(eventObject, nameof(EventObject.RecordId), (long?)recordId);
        SetSnapshotProperty(eventObject, nameof(EventObject.TimeCreated), DateTime.UtcNow);
        SetSnapshotProperty(eventObject, nameof(EventObject.Id), eventId);
        SetSnapshotProperty(eventObject, nameof(EventObject.LogName), logName);
        SetSnapshotProperty(eventObject, nameof(EventObject.ProviderName), providerName);
        SetSnapshotProperty(eventObject, nameof(EventObject.MachineName), "test-machine");
        foreach (KeyValuePair<string, string> field in data) {
            eventObject.Data[field.Key] = field.Value;
        }
        eventObject.ContainerLog = logName;
        eventObject.QueriedMachine = "test-machine";
        eventObject.MessageSubject = "SMB audit";
        return eventObject;
    }

    private static async IAsyncEnumerable<EventObject> YieldEventsAsync(IEnumerable<EventObject> events) {
        foreach (EventObject eventObject in events) {
            yield return eventObject;
            await Task.Yield();
        }
    }

    private static void SetSnapshotProperty<T>(EventObject eventObject, string propertyName, T value) {
        typeof(EventObject)
            .GetField($"<{propertyName}>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(eventObject, value);
    }
}
