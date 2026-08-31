namespace EventViewerX.Reporting;

/// <summary>Mutable delivery state stored separately from an immutable outbox batch.</summary>
public sealed class EventNotificationDeliveryState {
    /// <summary>Number of failed transport attempts.</summary>
    public int FailedAttempts { get; set; }
    /// <summary>UTC time of the latest attempt.</summary>
    public DateTime? LastAttemptUtc { get; set; }
    /// <summary>Sanitized latest transport error.</summary>
    public string? LastError { get; set; }
    /// <summary>UTC time at which the downstream transport acknowledged the payload.</summary>
    public DateTime? TransportAcknowledgedUtc { get; set; }
    /// <summary>UTC time at which transport and any owned checkpoint boundaries completed.</summary>
    public DateTime? DeliveredUtc { get; set; }
}
