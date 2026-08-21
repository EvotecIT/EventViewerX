namespace EventViewerX.Storage;

/// <summary>Durable consumer checkpoint committed atomically with stored events.</summary>
public sealed class EventStoreCheckpoint {
    /// <summary>Stable reader or watcher identity.</summary>
    public string Consumer { get; set; } = "default";
    /// <summary>Source or collector computer.</summary>
    public string Computer { get; set; } = string.Empty;
    /// <summary>Container channel or offline source.</summary>
    public string Container { get; set; } = string.Empty;
    /// <summary>Last committed record identifier.</summary>
    public long? RecordId { get; set; }
    /// <summary>Last committed native bookmark.</summary>
    public string? BookmarkXml { get; set; }
    /// <summary>UTC checkpoint update time.</summary>
    public DateTime UpdatedAtUtc { get; set; }
}
