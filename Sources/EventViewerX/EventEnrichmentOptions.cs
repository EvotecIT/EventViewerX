namespace EventViewerX;

/// <summary>
/// Controls optional work performed after a raw event has been projected into an event type.
/// </summary>
public sealed class EventEnrichmentOptions {
    /// <summary>
    /// Gets or sets whether typed events with client addresses should be enriched with reverse-DNS names.
    /// </summary>
    public bool ResolveDns { get; set; }

    /// <summary>
    /// Gets or sets the whole-lookup timeout in milliseconds, including dependency retries.
    /// </summary>
    public int DnsTimeoutMilliseconds { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the maximum number of reverse-DNS lookups that may run concurrently.
    /// Projected events and checkpoints remain ordered even when lookups overlap.
    /// </summary>
    public int DnsMaxConcurrency { get; set; } = 8;

    /// <summary>
    /// Gets or sets whether transient DNS failures may be retried. Disabled by default so enrichment cannot
    /// unexpectedly multiply the event-query latency.
    /// </summary>
    public bool RetryDnsOnTransient { get; set; }

    internal void Validate() {
        if (ResolveDns && DnsTimeoutMilliseconds <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(DnsTimeoutMilliseconds),
                "DNS timeout must be greater than zero.");
        }
        if (ResolveDns && (DnsMaxConcurrency <= 0 || DnsMaxConcurrency > 64)) {
            throw new ArgumentOutOfRangeException(
                nameof(DnsMaxConcurrency),
                "DNS concurrency must be between 1 and 64.");
        }
    }
}
