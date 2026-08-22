using System.ComponentModel;
using System.DirectoryServices.ActiveDirectory;
using System.Net;

namespace EventViewerX;

internal sealed class ActiveDirectoryTopologyProvider : IActiveDirectoryTopologyProvider {
    public ActiveDirectoryTopologySnapshot Discover(
        EventTargetDiscoveryRequest request,
        CancellationToken cancellationToken,
        Action<EventTargetDomainResult> domainCompleted,
        Action<EventTargetDiscoveryFailure> failureReported) {

        var domains = new List<EventTargetDomainResult>();
        var failures = new ReportingFailureCollection(cancellationToken, failureReported);
        cancellationToken.ThrowIfCancellationRequested();
        switch (request.Scope) {
            case EventTargetDiscoveryScope.CurrentDomain:
                DiscoverCurrentDomain(request, domains, failures, cancellationToken, domainCompleted);
                break;
            case EventTargetDiscoveryScope.Domain:
                DiscoverNamedDomain(request, domains, failures, cancellationToken, domainCompleted);
                break;
            case EventTargetDiscoveryScope.CurrentForest:
                DiscoverCurrentForest(request, domains, failures, cancellationToken, domainCompleted);
                break;
            case EventTargetDiscoveryScope.Forest:
                DiscoverNamedForest(request, domains, failures, cancellationToken, domainCompleted);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(request.Scope));
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new ActiveDirectoryTopologySnapshot(domains, failures);
    }

    private static void DiscoverCurrentDomain(
        EventTargetDiscoveryRequest request,
        List<EventTargetDomainResult> domains,
        ICollection<EventTargetDiscoveryFailure> failures,
        CancellationToken cancellationToken,
        Action<EventTargetDomainResult> domainCompleted) {

        try {
            cancellationToken.ThrowIfCancellationRequested();
            using Domain domain = Domain.GetComputerDomain();
            cancellationToken.ThrowIfCancellationRequested();
            DiscoverDomain(domain, TryGetForestName(domain), request, domains, failures, cancellationToken, domainCompleted);
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception exception) {
            failures.Add(CreateFailure("CurrentDomain", "ResolveDomain", exception));
        }
    }

    private static void DiscoverNamedDomain(
        EventTargetDiscoveryRequest request,
        List<EventTargetDomainResult> domains,
        ICollection<EventTargetDiscoveryFailure> failures,
        CancellationToken cancellationToken,
        Action<EventTargetDomainResult> domainCompleted) {

        string name = request.Name!;
        try {
            cancellationToken.ThrowIfCancellationRequested();
            using Domain domain = Domain.GetDomain(CreateContext(DirectoryContextType.Domain, name, request.Credential));
            cancellationToken.ThrowIfCancellationRequested();
            DiscoverDomain(domain, TryGetForestName(domain), request, domains, failures, cancellationToken, domainCompleted);
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception exception) {
            failures.Add(CreateFailure(name, "ResolveDomain", exception));
        }
    }

    private static void DiscoverCurrentForest(
        EventTargetDiscoveryRequest request,
        List<EventTargetDomainResult> domains,
        ICollection<EventTargetDiscoveryFailure> failures,
        CancellationToken cancellationToken,
        Action<EventTargetDomainResult> domainCompleted) {

        try {
            cancellationToken.ThrowIfCancellationRequested();
            using Domain computerDomain = Domain.GetComputerDomain();
            using Forest forest = computerDomain.Forest;
            cancellationToken.ThrowIfCancellationRequested();
            DiscoverForest(forest, request, domains, failures, cancellationToken, domainCompleted);
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception exception) {
            failures.Add(CreateFailure("CurrentForest", "ResolveForest", exception));
        }
    }

    private static void DiscoverNamedForest(
        EventTargetDiscoveryRequest request,
        List<EventTargetDomainResult> domains,
        ICollection<EventTargetDiscoveryFailure> failures,
        CancellationToken cancellationToken,
        Action<EventTargetDomainResult> domainCompleted) {

        string name = request.Name!;
        try {
            cancellationToken.ThrowIfCancellationRequested();
            using Forest forest = Forest.GetForest(CreateContext(DirectoryContextType.Forest, name, request.Credential));
            cancellationToken.ThrowIfCancellationRequested();
            DiscoverForest(forest, request, domains, failures, cancellationToken, domainCompleted);
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception exception) {
            failures.Add(CreateFailure(name, "ResolveForest", exception));
        }
    }

    private static void DiscoverForest(
        Forest forest,
        EventTargetDiscoveryRequest request,
        List<EventTargetDomainResult> domains,
        ICollection<EventTargetDiscoveryFailure> failures,
        CancellationToken cancellationToken,
        Action<EventTargetDomainResult> domainCompleted) {

        var forestNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { forest.Name };
        DiscoverForestDomains(forest, request, domains, failures, cancellationToken, domainCompleted);
        if (!request.IncludeTrustedForests) {
            return;
        }

        TrustRelationshipInformationCollection trusts;
        try {
            cancellationToken.ThrowIfCancellationRequested();
            trusts = forest.GetAllTrustRelationships();
            cancellationToken.ThrowIfCancellationRequested();
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception exception) {
            failures.Add(CreateFailure(forest.Name, "EnumerateForestTrusts", exception));
            return;
        }
        foreach (TrustRelationshipInformation trust in trusts) {
            cancellationToken.ThrowIfCancellationRequested();
            if (domains.Count >= request.MaximumDomainCount) {
                AddLimitFailure(
                    failures,
                    forest.Name,
                    "MaximumDomainCount",
                    $"Discovery stopped after {request.MaximumDomainCount} domain(s).");
                break;
            }
            string targetName = trust.TargetName?.Trim().TrimEnd('.') ?? string.Empty;
            if (targetName.Length == 0 || !forestNames.Add(targetName)) {
                continue;
            }
            try {
                using Forest trustedForest = Forest.GetForest(
                    CreateContext(DirectoryContextType.Forest, targetName, request.Credential));
                cancellationToken.ThrowIfCancellationRequested();
                forestNames.Add(trustedForest.Name);
                DiscoverForestDomains(trustedForest, request, domains, failures, cancellationToken, domainCompleted);
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception exception) {
                failures.Add(CreateFailure(targetName, "ResolveTrustedForest", exception));
            }
        }
    }

    private static void DiscoverForestDomains(
        Forest forest,
        EventTargetDiscoveryRequest request,
        List<EventTargetDomainResult> domains,
        ICollection<EventTargetDiscoveryFailure> failures,
        CancellationToken cancellationToken,
        Action<EventTargetDomainResult> domainCompleted) {

        DomainCollection forestDomains;
        try {
            cancellationToken.ThrowIfCancellationRequested();
            forestDomains = forest.Domains;
            cancellationToken.ThrowIfCancellationRequested();
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception exception) {
            failures.Add(CreateFailure(forest.Name, "EnumerateDomains", exception));
            return;
        }

        foreach (Domain domain in forestDomains) {
            using (domain) {
                cancellationToken.ThrowIfCancellationRequested();
                if (domains.Count >= request.MaximumDomainCount) {
                    AddLimitFailure(
                        failures,
                        forest.Name,
                        "MaximumDomainCount",
                        $"Discovery stopped after {request.MaximumDomainCount} domain(s).");
                    break;
                }
                if (CountTargets(domains) >= request.MaximumTargetCount) {
                    AddLimitFailure(
                        failures,
                        forest.Name,
                        "MaximumTargetCount",
                        $"Discovery stopped after {request.MaximumTargetCount} target(s).");
                    break;
                }
                DiscoverDomain(domain, forest.Name, request, domains, failures, cancellationToken, domainCompleted);
            }
        }
    }

    private static void DiscoverDomain(
        Domain domain,
        string? forestName,
        EventTargetDiscoveryRequest request,
        List<EventTargetDomainResult> domains,
        ICollection<EventTargetDiscoveryFailure> globalFailures,
        CancellationToken cancellationToken,
        Action<EventTargetDomainResult> domainCompleted) {

        if (domains.Count >= request.MaximumDomainCount) {
            AddLimitFailure(
                globalFailures,
                forestName ?? request.Name ?? request.Scope.ToString(),
                "MaximumDomainCount",
                $"Discovery stopped after {request.MaximumDomainCount} domain(s).");
            return;
        }
        string domainName;
        try {
            cancellationToken.ThrowIfCancellationRequested();
            domainName = domain.Name;
            cancellationToken.ThrowIfCancellationRequested();
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception exception) {
            globalFailures.Add(CreateFailure(
                forestName ?? request.Name ?? request.Scope.ToString(),
                "ReadDomainName",
                exception));
            return;
        }
        var targets = new List<EventTargetInfo>();
        var failures = new List<EventTargetDiscoveryFailure>();
        int existingTargetCount = CountTargets(domains);
        try {
            foreach (DomainController domainController in domain.DomainControllers) {
                using (domainController) {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (existingTargetCount + targets.Count >= request.MaximumTargetCount) {
                        var failure = new EventTargetDiscoveryFailure(
                            domainName,
                            "MaximumTargetCount",
                            EventTargetDiscoveryFailureKind.LimitReached,
                            $"Discovery stopped after {request.MaximumTargetCount} target(s).");
                        failures.Add(failure);
                        AddLimitFailure(
                            globalFailures,
                            domainName,
                            failure.Stage,
                            failure.Message);
                        break;
                    }
                    bool? isGlobalCatalog = null;
                    try {
                        isGlobalCatalog = domainController.IsGlobalCatalog();
                    } catch {
                    }
                    targets.Add(new EventTargetInfo(
                        domainController.Name,
                        EventTargetKind.DomainController,
                        domainName,
                        forestName,
                        TryGetSiteName(domainController),
                        isGlobalCatalog));
                }
            }
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception exception) {
            failures.Add(CreateFailure(domainName, "EnumerateDomainControllers", exception));
        }
        cancellationToken.ThrowIfCancellationRequested();
        var result = new EventTargetDomainResult(domainName, forestName, targets, failures);
        domains.Add(result);
        domainCompleted(result);
    }

    private static int CountTargets(IEnumerable<EventTargetDomainResult> domains) =>
        domains.Sum(static domain => domain.Targets.Count);

    private static void AddLimitFailure(
        ICollection<EventTargetDiscoveryFailure> failures,
        string scope,
        string stage,
        string message) {

        if (failures.Any(failure =>
                failure.Kind == EventTargetDiscoveryFailureKind.LimitReached &&
                string.Equals(failure.Stage, stage, StringComparison.OrdinalIgnoreCase))) {
            return;
        }
        failures.Add(new EventTargetDiscoveryFailure(
            scope,
            stage,
            EventTargetDiscoveryFailureKind.LimitReached,
            message));
    }

    private static string? TryGetForestName(Domain domain) {
        try {
            using Forest forest = domain.Forest;
            return forest.Name;
        } catch {
            return null;
        }
    }

    private static string? TryGetSiteName(DomainController domainController) {
        try {
            return domainController.SiteName;
        } catch {
            return null;
        }
    }

    private static DirectoryContext CreateContext(
        DirectoryContextType type,
        string name,
        NetworkCredential? credential) {

        if (credential == null) {
            return new DirectoryContext(type, name);
        }
        string userName = credential.UserName;
        if (!string.IsNullOrWhiteSpace(credential.Domain) &&
            userName.IndexOf('\\') < 0 &&
            userName.IndexOf('@') < 0) {
            userName = credential.Domain + "\\" + userName;
        }
        return new DirectoryContext(type, name, userName, credential.Password);
    }

    internal static EventTargetDiscoveryFailure CreateFailure(
        string scope,
        string stage,
        Exception exception) {

        EventTargetDiscoveryFailureKind kind = exception switch {
            ActiveDirectoryObjectNotFoundException => EventTargetDiscoveryFailureKind.NotFound,
            UnauthorizedAccessException => EventTargetDiscoveryFailureKind.AccessDenied,
            Win32Exception win32 when win32.NativeErrorCode == 5 => EventTargetDiscoveryFailureKind.AccessDenied,
            _ when exception.GetType().Name == "ActiveDirectoryObjectNotFoundException" => EventTargetDiscoveryFailureKind.NotFound,
            _ => EventTargetDiscoveryFailureKind.Error
        };
        if (exception is ActiveDirectoryObjectNotFoundException &&
            (string.Equals(scope, "CurrentDomain", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(scope, "CurrentForest", StringComparison.OrdinalIgnoreCase))) {
            kind = EventTargetDiscoveryFailureKind.NotDomainJoined;
        }
        return new EventTargetDiscoveryFailure(scope, stage, kind, exception.Message);
    }

    private sealed class ReportingFailureCollection : ICollection<EventTargetDiscoveryFailure>, IReadOnlyList<EventTargetDiscoveryFailure> {
        private readonly List<EventTargetDiscoveryFailure> _items = new();
        private readonly CancellationToken _cancellationToken;
        private readonly Action<EventTargetDiscoveryFailure> _failureReported;

        internal ReportingFailureCollection(
            CancellationToken cancellationToken,
            Action<EventTargetDiscoveryFailure> failureReported) {

            _cancellationToken = cancellationToken;
            _failureReported = failureReported;
        }

        public int Count => _items.Count;
        public bool IsReadOnly => false;
        public EventTargetDiscoveryFailure this[int index] => _items[index];

        public void Add(EventTargetDiscoveryFailure item) {
            _items.Add(item);
            if (!_cancellationToken.IsCancellationRequested) {
                _failureReported(item);
            }
        }

        public void Clear() => _items.Clear();
        public bool Contains(EventTargetDiscoveryFailure item) => _items.Contains(item);
        public void CopyTo(EventTargetDiscoveryFailure[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
        public IEnumerator<EventTargetDiscoveryFailure> GetEnumerator() => _items.GetEnumerator();
        public bool Remove(EventTargetDiscoveryFailure item) => _items.Remove(item);
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
