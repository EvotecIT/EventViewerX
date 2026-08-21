namespace EventViewerX;

/// <summary>
/// Tracks terminal failures across the native subscriptions that form one
/// logical watcher.
/// </summary>
internal sealed class EventLogSubscriptionLifetime {
    private readonly bool[] _terminalSubscriptions;
    private int _activeSubscriptions;

    internal EventLogSubscriptionLifetime(
        int subscriptionCount) {

        if (subscriptionCount <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(subscriptionCount),
                "Subscription count must be positive.");
        }
        _terminalSubscriptions =
            new bool[subscriptionCount];
        _activeSubscriptions =
            subscriptionCount;
    }

    /// <summary>
    /// Marks one subscription terminal and returns true only when the last
    /// active subscription has ended.
    /// </summary>
    internal bool MarkTerminal(
        int subscriptionIndex) {

        if (subscriptionIndex < 0 ||
            subscriptionIndex >=
            _terminalSubscriptions.Length) {
            throw new ArgumentOutOfRangeException(
                nameof(subscriptionIndex));
        }
        if (_terminalSubscriptions[
                subscriptionIndex]) {
            return false;
        }

        _terminalSubscriptions[
            subscriptionIndex] = true;
        _activeSubscriptions--;
        return _activeSubscriptions == 0;
    }
}
