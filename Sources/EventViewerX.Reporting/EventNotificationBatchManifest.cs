namespace EventViewerX.Reporting;

/// <summary>Metadata stored with a completed notification outbox batch.</summary>
public sealed class EventNotificationBatchManifest {
    /// <summary>Stable identifier used to make batch publication idempotent.</summary>
    public string BatchId { get; set; } = string.Empty;

    /// <summary>Number of events included in the batch.</summary>
    public int EventCount { get; set; }

    /// <summary>Report title used to rebuild the transport subject during retry.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>UTC time at which the completed batch was persisted.</summary>
    public DateTime PersistedUtc { get; set; }
}
