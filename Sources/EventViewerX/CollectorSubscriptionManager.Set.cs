using EventViewerX.Native;

namespace EventViewerX;

/// <summary>
/// Updates supported Windows Event Collector subscription properties through
/// the Windows Event Collector service API.
/// </summary>
public static partial class CollectorSubscriptionManager {
    /// <summary>
    /// Enables or disables an existing local collector subscription and
    /// verifies the persisted state.
    /// </summary>
    public static CollectorSubscriptionUpdateResult
        SetCollectorSubscriptionEnabled(
            string name,
            bool enabled,
            string? machineName = null) {

        ValidateSubscriptionName(name);
        if (!EventLogTarget.IsLocalMachine(
                machineName)) {
            throw new NotSupportedException(
                "The Windows Event Collector service API manages subscriptions on the local collector only. Run the operation on the remote collector instead of editing its registry remotely.");
        }

        CollectorSubscriptionSnapshot before =
            GetCollectorSubscriptionSnapshot(
                name) ??
            throw new FileNotFoundException(
                $"Collector subscription '{name}' was not found.",
                name);
        if (before.IsEnabled == enabled) {
            return new CollectorSubscriptionUpdateResult {
                SubscriptionName = name,
                Before = before,
                After = before,
                Success = true,
                Changed = false
            };
        }

        WindowsEventCollector.SetEnabled(
            name,
            enabled);
        CollectorSubscriptionSnapshot after =
            GetCollectorSubscriptionSnapshot(
                name) ??
            throw new InvalidOperationException(
                $"Collector subscription '{name}' could not be read after Windows reported a successful save.");
        if (after.IsEnabled != enabled) {
            throw new InvalidOperationException(
                $"Collector subscription '{name}' did not retain the requested Enabled={enabled} value.");
        }
        return new CollectorSubscriptionUpdateResult {
            SubscriptionName = name,
            Before = before,
            After = after,
            Success = true,
            Changed = true
        };
    }

    private static void ValidateSubscriptionName(
        string name) {

        if (string.IsNullOrWhiteSpace(name)) {
            throw new ArgumentException(
                "Subscription name cannot be null or empty.",
                nameof(name));
        }
    }
}
