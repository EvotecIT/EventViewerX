namespace EventViewerX.Reporting;

/// <summary>Validated durable notification payload ready for first delivery or retry.</summary>
public sealed class EventNotificationOutboxBatch {
    internal EventNotificationOutboxBatch(
        string directoryPath,
        EventNotificationBatchManifest manifest,
        EventNotificationDeliveryState delivery,
        string html,
        string plainText) {

        DirectoryPath = directoryPath;
        Manifest = manifest;
        Delivery = delivery;
        Html = html;
        PlainText = plainText;
    }

    /// <summary>Validated full batch directory path.</summary>
    public string DirectoryPath { get; }
    /// <summary>Immutable batch metadata.</summary>
    public EventNotificationBatchManifest Manifest { get; }
    /// <summary>Latest delivery state.</summary>
    public EventNotificationDeliveryState Delivery { get; }
    /// <summary>Persisted HTML message body.</summary>
    public string Html { get; }
    /// <summary>Persisted plain-text message body.</summary>
    public string PlainText { get; }
}
