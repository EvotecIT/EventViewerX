using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using EventViewerX.Native;

namespace EventViewerX;

/// <summary>Resolves local or explicitly requested Active Directory event targets.</summary>
public static class EventTargetResolver {
    /// <summary>Resolves a bounded target request while preserving partial per-domain results.</summary>
    public static EventTargetDiscoveryResult Resolve(
        EventTargetDiscoveryRequest? request = null,
        CancellationToken cancellationToken = default) {

        return Resolve(request ?? new EventTargetDiscoveryRequest(), new ActiveDirectoryTopologyProvider(), cancellationToken);
    }

    internal static EventTargetDiscoveryResult Resolve(
        EventTargetDiscoveryRequest request,
        IActiveDirectoryTopologyProvider provider,
        CancellationToken cancellationToken) {

        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }
        if (provider == null) {
            throw new ArgumentNullException(nameof(provider));
        }
        EventTargetDiscoveryRequest snapshot = request.Snapshot();
        var stopwatch = Stopwatch.StartNew();
        if (snapshot.Scope == EventTargetDiscoveryScope.LocalMachine) {
            EventTargetInfo[] localTargets = {
                new(EventLogTarget.LocalMachineName, EventTargetKind.LocalMachine)
            };
            return CreateResult(snapshot, localTargets, Array.Empty<EventTargetDomainResult>(),
                Array.Empty<EventTargetDiscoveryFailure>(), stopwatch.Elapsed);
        }

        try {
            int timeoutMilliseconds = checked((int)Math.Ceiling(snapshot.Timeout.TotalMilliseconds));
            ActiveDirectoryTopologySnapshot topology = BoundedNativeOperation.Execute(
                () => provider.Discover(snapshot),
                timeoutMilliseconds,
                $"Active Directory target discovery exceeded {timeoutMilliseconds} ms.",
                cancellationToken);
            EventTargetInfo[] targets = topology.Domains
                .SelectMany(static domain => domain.Targets)
                .GroupBy(static target => target.ComputerName, StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.First())
                .OrderBy(static target => target.ComputerName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return CreateResult(snapshot, targets, topology.Domains, topology.Failures, stopwatch.Elapsed);
        } catch (OperationCanceledException) {
            throw;
        } catch (TimeoutException exception) {
            EventTargetDiscoveryFailure[] failures = {
                new(snapshot.Name ?? snapshot.Scope.ToString(), "Discovery", EventTargetDiscoveryFailureKind.Timeout, exception.Message)
            };
            return CreateResult(snapshot, Array.Empty<EventTargetInfo>(), Array.Empty<EventTargetDomainResult>(), failures, stopwatch.Elapsed);
        } catch (Exception exception) {
            EventTargetDiscoveryFailure[] failures = {
                ActiveDirectoryTopologyProvider.CreateFailure(
                    snapshot.Name ?? snapshot.Scope.ToString(),
                    "Discovery",
                    exception)
            };
            return CreateResult(snapshot, Array.Empty<EventTargetInfo>(), Array.Empty<EventTargetDomainResult>(), failures, stopwatch.Elapsed);
        }
    }

    private static EventTargetDiscoveryResult CreateResult(
        EventTargetDiscoveryRequest request,
        IReadOnlyList<EventTargetInfo> targets,
        IReadOnlyList<EventTargetDomainResult> domains,
        IReadOnlyList<EventTargetDiscoveryFailure> failures,
        TimeSpan duration) {

        string fingerprint = ComputeFingerprint(request, targets, domains);
        return new EventTargetDiscoveryResult(
            request.Scope,
            request.Name,
            targets,
            domains,
            failures,
            fingerprint,
            duration);
    }

    private static string ComputeFingerprint(
        EventTargetDiscoveryRequest request,
        IReadOnlyList<EventTargetInfo> targets,
        IReadOnlyList<EventTargetDomainResult> domains) {

        var identity = new StringBuilder();
        identity.Append(request.Scope).Append('|').Append(request.Name?.ToUpperInvariant()).Append('|');
        foreach (EventTargetDomainResult domain in domains.OrderBy(static item => item.DomainName, StringComparer.OrdinalIgnoreCase)) {
            identity.Append(domain.DomainName.ToUpperInvariant()).Append(';');
        }
        identity.Append('|');
        foreach (EventTargetInfo target in targets.OrderBy(static item => item.ComputerName, StringComparer.OrdinalIgnoreCase)) {
            identity.Append(target.ComputerName.ToUpperInvariant()).Append(';');
        }
        using SHA256 sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(identity.ToString()));
        return BitConverter.ToString(hash).Replace("-", string.Empty);
    }
}
