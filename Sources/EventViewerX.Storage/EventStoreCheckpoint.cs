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

    /// <summary>
    /// Validates that the saved record boundary is still contiguous with the
    /// currently retained event-log range.
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// The channel was cleared, its retained history has a gap after the
    /// checkpoint, or the supplied range is inconsistent.
    /// </exception>
    public void ValidateAvailableRange(long? oldestRecordId, long? newestRecordId) {
        if (!RecordId.HasValue) {
            return;
        }
        if (RecordId.Value < 0) {
            throw new InvalidDataException(
                "A checkpoint record ID must be greater than or equal to zero.");
        }
        if (!oldestRecordId.HasValue || !newestRecordId.HasValue) {
            throw new InvalidDataException(
                $"Checkpoint {RecordId.Value} cannot be reconciled because the retained channel range is empty or incomplete.");
        }
        if (oldestRecordId.Value < 0 ||
            newestRecordId.Value < oldestRecordId.Value) {
            throw new InvalidDataException(
                $"The retained channel range {oldestRecordId}..{newestRecordId} is invalid.");
        }
        if (RecordId.Value > newestRecordId.Value) {
            throw new InvalidDataException(
                $"Checkpoint {RecordId.Value} is newer than the channel's newest retained record {newestRecordId.Value}; the channel was cleared or replaced.");
        }
        if (oldestRecordId.Value > RecordId.Value &&
            oldestRecordId.Value - RecordId.Value > 1) {
            throw new InvalidDataException(
                $"The channel's oldest retained record {oldestRecordId.Value} follows checkpoint {RecordId.Value} with a gap; retained events were lost before collection resumed.");
        }
    }
}
