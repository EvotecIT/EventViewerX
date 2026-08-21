namespace EventViewerX;

public static partial class CollectorSubscriptionManager {
    private const int RemovalVerificationAttempts = 20;
    private static readonly TimeSpan RemovalVerificationDelay =
        TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Removes a local Windows Event Collector subscription and verifies that
    /// it is absent. Removing an already-absent name succeeds without change.
    /// </summary>
    public static CollectorSubscriptionRemovalResult
        RemoveCollectorSubscription(
            string name,
            CancellationToken cancellationToken = default) {

        return RemoveCollectorSubscription(
            name,
            static value =>
                GetCollectorSubscriptionSnapshot(value),
            RunWecUtil,
            RemovalVerificationAttempts,
            RemovalVerificationDelay,
            cancellationToken);
    }

    internal static CollectorSubscriptionRemovalResult
        RemoveCollectorSubscription(
            string name,
            Func<string, CollectorSubscriptionSnapshot?>
                snapshotResolver,
            Func<IReadOnlyList<string>, CancellationToken, string>
                wecUtilRunner,
            int verificationAttempts,
            TimeSpan verificationDelay,
            CancellationToken cancellationToken) {

        ValidateSubscriptionName(name);
        if (snapshotResolver == null) {
            throw new ArgumentNullException(nameof(snapshotResolver));
        }
        if (wecUtilRunner == null) {
            throw new ArgumentNullException(nameof(wecUtilRunner));
        }
        if (verificationAttempts <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(verificationAttempts));
        }
        if (verificationDelay < TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(
                nameof(verificationDelay));
        }

        string subscriptionName = name.Trim();
        CollectorSubscriptionSnapshot? before =
            snapshotResolver(subscriptionName);
        if (before == null) {
            return new CollectorSubscriptionRemovalResult {
                SubscriptionName = subscriptionName,
                Success = true,
                Changed = false,
                Before = null,
                After = null
            };
        }

        wecUtilRunner(
            new[] { "ds", subscriptionName },
            cancellationToken);
        for (int attempt = 0;
             attempt < verificationAttempts;
             attempt++) {
            cancellationToken.ThrowIfCancellationRequested();
            CollectorSubscriptionSnapshot? after =
                snapshotResolver(subscriptionName);
            if (after == null) {
                return new CollectorSubscriptionRemovalResult {
                    SubscriptionName = subscriptionName,
                    Success = true,
                    Changed = true,
                    Before = before,
                    After = null
                };
            }
            if (attempt + 1 < verificationAttempts &&
                cancellationToken.WaitHandle.WaitOne(
                    verificationDelay)) {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        throw new InvalidOperationException(
            $"Windows reported success but collector subscription '{subscriptionName}' is still present.");
    }
}
