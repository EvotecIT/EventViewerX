namespace EventViewerX.Reporting;

/// <summary>Transport-neutral checkpoint boundary persisted with a durable notification batch.</summary>
public sealed class EventNotificationCheckpointBoundary {
    /// <summary>Stable checkpoint consumer identity.</summary>
    public string Consumer { get; set; } = string.Empty;
    /// <summary>Source or collector computer.</summary>
    public string Computer { get; set; } = string.Empty;
    /// <summary>Container channel and query identity.</summary>
    public string Container { get; set; } = string.Empty;
    /// <summary>Record identifier acknowledged by this batch.</summary>
    public long? RecordId { get; set; }
    /// <summary>Native bookmark acknowledged by this batch.</summary>
    public string? BookmarkXml { get; set; }
    /// <summary>Whether an earlier checkpoint existed when this batch was persisted.</summary>
    public bool ExpectedExists { get; set; }
    /// <summary>Expected prior record identifier used for compare-and-swap recovery.</summary>
    public long? ExpectedRecordId { get; set; }
    /// <summary>Expected prior native bookmark used for compare-and-swap recovery.</summary>
    public string? ExpectedBookmarkXml { get; set; }
    /// <summary>Expected prior checkpoint update time used for compare-and-swap recovery.</summary>
    public DateTime? ExpectedUpdatedAtUtc { get; set; }
}
