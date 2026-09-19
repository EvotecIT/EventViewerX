using System.ComponentModel;

namespace EventViewerX;

/// <summary>Reads the collector's authoritative source-authorization configuration.</summary>
public static partial class CollectorSubscriptionManager {
    /// <summary>
    /// Reads domain-computer source authorization from a local WEC subscription through wecutil.
    /// A collector-initiated subscription returns a not-applicable result. A failed read returns
    /// an unavailable result with a diagnostic instead of an empty or permissive ACL.
    /// </summary>
    public static CollectorSourceAuthorization GetCollectorSourceAuthorization(
        string subscriptionName,
        CancellationToken cancellationToken = default) =>
        ReadCollectorSourceAuthorization(subscriptionName, RunWecUtil, cancellationToken);

    internal static CollectorSourceAuthorization ReadCollectorSourceAuthorization(
        string subscriptionName,
        Func<IReadOnlyList<string>, CancellationToken, string> wecUtilRunner,
        CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(subscriptionName)) {
            throw new ArgumentException("Subscription name cannot be empty.", nameof(subscriptionName));
        }
        if (wecUtilRunner == null) {
            throw new ArgumentNullException(nameof(wecUtilRunner));
        }
        cancellationToken.ThrowIfCancellationRequested();

        string xml;
        try {
            xml = wecUtilRunner(new[] { "gs", subscriptionName.Trim(), "/f:XML" }, cancellationToken);
        } catch (Exception exception) when (exception is Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException or TimeoutException or PlatformNotSupportedException) {
            return CollectorSourceAuthorization.Unavailable(
                $"Could not read WEC subscription '{subscriptionName}': {exception.Message}");
        }

        if (!CollectorSubscriptionXml.TryNormalize(xml, out CollectorSubscriptionXmlDetails? details, out string? error)) {
            return CollectorSourceAuthorization.Unavailable(
                $"WEC subscription '{subscriptionName}' returned invalid XML: {error}");
        }

        if (!string.Equals(details!.SubscriptionId, subscriptionName.Trim(), StringComparison.OrdinalIgnoreCase)) {
            return CollectorSourceAuthorization.Unavailable(
                $"WEC returned subscription identity '{details.SubscriptionId ?? "<missing>"}' while reading '{subscriptionName}'.");
        }

        if (string.Equals(details.SubscriptionType, "CollectorInitiated", StringComparison.OrdinalIgnoreCase)) {
            return CollectorSourceAuthorization.NotApplicable();
        }
        if (!string.Equals(details.SubscriptionType, "SourceInitiated", StringComparison.OrdinalIgnoreCase)) {
            return CollectorSourceAuthorization.Unavailable(
                $"WEC subscription '{subscriptionName}' did not identify a supported subscription type.");
        }

        return new CollectorSourceAuthorization(
            CollectorSourceAuthorizationStatus.Available,
            CollectorDomainSourceAccess.Inspect(details.AllowedSourceDomainComputersSddl),
            details.RawNonDomainSourceXml,
            details.AllowedIssuerCAs,
            details.AllowedSubjects,
            details.DeniedSubjects);
    }
}
