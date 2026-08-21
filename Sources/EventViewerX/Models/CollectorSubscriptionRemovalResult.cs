namespace EventViewerX;

/// <summary>Verified result of removing a Windows Event Collector subscription.</summary>
public sealed class CollectorSubscriptionRemovalResult {
    /// <summary>Collector subscription name.</summary>
    public string SubscriptionName { get; set; } = string.Empty;

    /// <summary>Whether the requested final state was verified.</summary>
    public bool Success { get; set; }

    /// <summary>Whether a persisted subscription was removed.</summary>
    public bool Changed { get; set; }

    /// <summary>Snapshot captured before deletion, or null when already absent.</summary>
    public CollectorSubscriptionSnapshot? Before { get; set; }

    /// <summary>Snapshot after deletion. A successful result always has a null value.</summary>
    public CollectorSubscriptionSnapshot? After { get; set; }
}
