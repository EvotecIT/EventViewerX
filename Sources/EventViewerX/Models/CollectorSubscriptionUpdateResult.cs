namespace EventViewerX;

/// <summary>
/// Verified result of changing a supported Windows Event Collector
/// subscription property.
/// </summary>
public sealed class CollectorSubscriptionUpdateResult {
    /// <summary>Collector subscription name.</summary>
    public string SubscriptionName { get; set; } =
        string.Empty;
    /// <summary>Whether Windows saved and verification confirmed the request.</summary>
    public bool Success { get; set; }
    /// <summary>Whether persisted state changed.</summary>
    public bool Changed { get; set; }
    /// <summary>Snapshot read before mutation.</summary>
    public CollectorSubscriptionSnapshot Before {
        get;
        set;
    } = null!;
    /// <summary>Snapshot read after mutation or the same snapshot when unchanged.</summary>
    public CollectorSubscriptionSnapshot After {
        get;
        set;
    } = null!;
}
