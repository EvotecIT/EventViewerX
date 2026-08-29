namespace EventViewerX.Storage;

/// <summary>Explicit age-based retention and compaction policy for optional local history.</summary>
public sealed class EventStoreRetentionPolicy {
    /// <summary>Maximum age of source events. Null retains them indefinitely.</summary>
    public TimeSpan? EventRetention { get; set; }
    /// <summary>Maximum age of durable findings. Null retains them indefinitely.</summary>
    public TimeSpan? FindingRetention { get; set; }
    /// <summary>Whether to compact free pages after pruning.</summary>
    public bool VacuumAfterPrune { get; set; }

    internal EventStoreRetentionPolicy Snapshot() {
        ValidateDuration(EventRetention, nameof(EventRetention));
        ValidateDuration(FindingRetention, nameof(FindingRetention));
        if (!EventRetention.HasValue && !FindingRetention.HasValue) {
            throw new ArgumentException("At least one retention duration is required.");
        }
        return new EventStoreRetentionPolicy {
            EventRetention = EventRetention,
            FindingRetention = FindingRetention,
            VacuumAfterPrune = VacuumAfterPrune
        };
    }

    private static void ValidateDuration(TimeSpan? value, string name) {
        if (value.HasValue && (value <= TimeSpan.Zero || value > TimeSpan.FromDays(36500))) {
            throw new ArgumentOutOfRangeException(name, "Retention must be greater than zero and no longer than 100 years.");
        }
    }
}
