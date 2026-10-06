namespace EventViewerX;

public partial class WatcherInfo {
    private readonly EventWatcherDelivery _nativeDelivery = new();
    private EventWatcherDelivery? _hostDelivery;
    internal EventWatcherDelivery? HostDelivery => _hostDelivery;

    /// <summary>Snapshot of accepted action work, including work still draining after collection stops.</summary>
    public EventWatcherHealth Health => (_hostDelivery ?? _nativeDelivery).GetHealth();

    /// <summary>Completes when collection admission closes and accepted host actions are acknowledged.</summary>
    public Task DrainCompletion => (_hostDelivery ?? _nativeDelivery).DrainCompletion;

    internal void AttachHostDelivery(EventWatcherDelivery delivery) {
        _hostDelivery = delivery ?? throw new ArgumentNullException(nameof(delivery));
    }
}
