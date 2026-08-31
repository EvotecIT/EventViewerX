namespace EventViewerX.Storage;

/// <summary>Outcome of one explicit EventStore retention run.</summary>
public sealed class EventStoreRetentionResult {
    internal EventStoreRetentionResult(
        int deletedEvents,
        int deletedFindings,
        long bytesBefore,
        long bytesAfter,
        bool vacuumed,
        DateTime completedAtUtc) {

        DeletedEvents = deletedEvents;
        DeletedFindings = deletedFindings;
        BytesBefore = bytesBefore;
        BytesAfter = bytesAfter;
        Vacuumed = vacuumed;
        CompletedAtUtc = completedAtUtc;
    }

    /// <summary>Source-event rows removed.</summary>
    public int DeletedEvents { get; }
    /// <summary>Finding rows removed; evidence and entities cascade.</summary>
    public int DeletedFindings { get; }
    /// <summary>Primary database size before retention.</summary>
    public long BytesBefore { get; }
    /// <summary>Primary database size after retention and optional compaction.</summary>
    public long BytesAfter { get; }
    /// <summary>Whether SQLite VACUUM compacted free pages.</summary>
    public bool Vacuumed { get; }
    /// <summary>UTC completion time.</summary>
    public DateTime CompletedAtUtc { get; }
}
