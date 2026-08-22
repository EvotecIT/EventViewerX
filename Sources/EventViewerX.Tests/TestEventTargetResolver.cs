using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventTargetResolver {
    [Fact]
    public void DefaultRequestReturnsOnlyLocalMachineWithoutDirectoryDiscovery() {
        var provider = new RecordingProvider();

        EventTargetDiscoveryResult result = EventTargetResolver.Resolve(
            new EventTargetDiscoveryRequest(),
            provider,
            CancellationToken.None);

        EventTargetInfo target = Assert.Single(result.Targets);
        Assert.Equal(EventTargetKind.LocalMachine, target.Kind);
        Assert.Equal(EventLogTarget.LocalMachineName, target.ComputerName);
        Assert.False(provider.Called);
        Assert.True(result.IsComplete);
        Assert.NotEmpty(result.Fingerprint);
    }

    [Fact]
    public void ExplicitForestPreservesPerDomainSuccessAndFailure() {
        var domainFailure = new EventTargetDiscoveryFailure(
            "child.example.com",
            "EnumerateDomainControllers",
            EventTargetDiscoveryFailureKind.AccessDenied,
            "Denied");
        var provider = new RecordingProvider(new ActiveDirectoryTopologySnapshot(
            new[] {
                new EventTargetDomainResult(
                    "example.com",
                    "example.com",
                    new[] { new EventTargetInfo("dc1.example.com", EventTargetKind.DomainController, "example.com", "example.com") },
                    Array.Empty<EventTargetDiscoveryFailure>()),
                new EventTargetDomainResult(
                    "child.example.com",
                    "example.com",
                    Array.Empty<EventTargetInfo>(),
                    new[] { domainFailure })
            },
            Array.Empty<EventTargetDiscoveryFailure>()));

        EventTargetDiscoveryResult result = EventTargetResolver.Resolve(
            new EventTargetDiscoveryRequest { Scope = EventTargetDiscoveryScope.CurrentForest },
            provider,
            CancellationToken.None);

        Assert.True(provider.Called);
        Assert.Single(result.Targets);
        Assert.Equal(2, result.Domains.Count);
        Assert.False(result.IsComplete);
        Assert.Same(domainFailure, Assert.Single(result.Domains[1].Failures));
    }

    [Fact]
    public void FingerprintIsStableAcrossProviderOrderingAndDuplicateTargets() {
        EventTargetDiscoveryRequest request = new() { Scope = EventTargetDiscoveryScope.Domain, Name = "example.com" };
        EventTargetDomainResult first = new(
            "example.com",
            "example.com",
            new[] {
                new EventTargetInfo("dc2.example.com", EventTargetKind.DomainController),
                new EventTargetInfo("DC1.example.com", EventTargetKind.DomainController)
            },
            Array.Empty<EventTargetDiscoveryFailure>());
        EventTargetDomainResult second = new(
            "EXAMPLE.COM",
            "example.com",
            new[] {
                new EventTargetInfo("dc1.example.com", EventTargetKind.DomainController),
                new EventTargetInfo("DC2.EXAMPLE.COM", EventTargetKind.DomainController),
                new EventTargetInfo("dc1.example.com", EventTargetKind.DomainController)
            },
            Array.Empty<EventTargetDiscoveryFailure>());

        EventTargetDiscoveryResult a = EventTargetResolver.Resolve(
            request,
            new RecordingProvider(new ActiveDirectoryTopologySnapshot(new[] { first }, Array.Empty<EventTargetDiscoveryFailure>())),
            CancellationToken.None);
        EventTargetDiscoveryResult b = EventTargetResolver.Resolve(
            request,
            new RecordingProvider(new ActiveDirectoryTopologySnapshot(new[] { second }, Array.Empty<EventTargetDiscoveryFailure>())),
            CancellationToken.None);

        Assert.Equal(a.Fingerprint, b.Fingerprint);
        Assert.Equal(2, a.Targets.Count);
        Assert.Equal(2, b.Targets.Count);
    }

    [Theory]
    [InlineData(EventTargetDiscoveryScope.Domain)]
    [InlineData(EventTargetDiscoveryScope.Forest)]
    public void NamedScopeRequiresName(EventTargetDiscoveryScope scope) {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => EventTargetResolver.Resolve(
            new EventTargetDiscoveryRequest { Scope = scope },
            new RecordingProvider(),
            CancellationToken.None));

        Assert.Contains("Name is required", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TrustedTraversalRequiresForestScope() {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => EventTargetResolver.Resolve(
            new EventTargetDiscoveryRequest {
                Scope = EventTargetDiscoveryScope.CurrentDomain,
                IncludeTrustedForests = true
            },
            new RecordingProvider(),
            CancellationToken.None));

        Assert.Contains("Trusted forest traversal", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DomainWithoutControllersIsIncomplete() {
        var provider = new RecordingProvider(new ActiveDirectoryTopologySnapshot(
            new[] {
                new EventTargetDomainResult(
                    "example.com",
                    "example.com",
                    Array.Empty<EventTargetInfo>(),
                    Array.Empty<EventTargetDiscoveryFailure>())
            },
            Array.Empty<EventTargetDiscoveryFailure>()));

        EventTargetDiscoveryResult result = EventTargetResolver.Resolve(
            new EventTargetDiscoveryRequest {
                Scope = EventTargetDiscoveryScope.Domain,
                Name = "example.com"
            },
            provider,
            CancellationToken.None);

        Assert.False(result.IsComplete);
        Assert.False(Assert.Single(result.Domains).Succeeded);
        Assert.Empty(result.Targets);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void DiscoveryBoundsMustBePositive(int maximumDomains, int maximumTargets) {
        Assert.Throws<ArgumentOutOfRangeException>(() => EventTargetResolver.Resolve(
            new EventTargetDiscoveryRequest {
                MaximumDomainCount = maximumDomains,
                MaximumTargetCount = maximumTargets
            },
            new RecordingProvider(),
            CancellationToken.None));
    }

    [Fact]
    public void LimitFailureMarksResultTruncated() {
        var provider = new RecordingProvider(new ActiveDirectoryTopologySnapshot(
            Array.Empty<EventTargetDomainResult>(),
            new[] {
                new EventTargetDiscoveryFailure(
                    "example.com",
                    "MaximumDomainCount",
                    EventTargetDiscoveryFailureKind.LimitReached,
                    "Limit reached")
            }));

        EventTargetDiscoveryResult result = EventTargetResolver.Resolve(
            new EventTargetDiscoveryRequest {
                Scope = EventTargetDiscoveryScope.Forest,
                Name = "example.com"
            },
            provider,
            CancellationToken.None);

        Assert.True(result.IsTruncated);
        Assert.False(result.IsComplete);
    }

    private sealed class RecordingProvider : IActiveDirectoryTopologyProvider {
        private readonly ActiveDirectoryTopologySnapshot _snapshot;

        internal RecordingProvider(ActiveDirectoryTopologySnapshot? snapshot = null) {
            _snapshot = snapshot ?? new ActiveDirectoryTopologySnapshot(
                Array.Empty<EventTargetDomainResult>(),
                Array.Empty<EventTargetDiscoveryFailure>());
        }

        internal bool Called { get; private set; }

        public ActiveDirectoryTopologySnapshot Discover(EventTargetDiscoveryRequest request) {
            Called = true;
            return _snapshot;
        }
    }
}
