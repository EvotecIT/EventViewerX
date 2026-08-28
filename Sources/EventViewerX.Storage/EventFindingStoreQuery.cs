namespace EventViewerX.Storage;

/// <summary>Defines a bounded query over durable detection findings.</summary>
public sealed class EventFindingStoreQuery {
    /// <summary>Inclusive UTC lower boundary for finding evidence windows.</summary>
    public DateTime? StartTime { get; set; }
    /// <summary>Inclusive UTC upper boundary for finding evidence windows.</summary>
    public DateTime? EndTime { get; set; }
    /// <summary>Rule identifiers to include.</summary>
    public IReadOnlyList<string>? RuleIds { get; set; }
    /// <summary>Pack identifiers to include.</summary>
    public IReadOnlyList<string>? PackIds { get; set; }
    /// <summary>Effective severities to include.</summary>
    public IReadOnlyList<EventDetectionSeverity>? Severities { get; set; }
    /// <summary>Finding statuses to include.</summary>
    public IReadOnlyList<EventDetectionFindingStatus>? Statuses { get; set; }
    /// <summary>Optional exact entity field name, used together with EntityValue.</summary>
    public string? EntityField { get; set; }
    /// <summary>Optional exact entity value, used together with EntityField.</summary>
    public string? EntityValue { get; set; }
    /// <summary>Maximum findings returned. Zero is unlimited.</summary>
    public int MaxFindings { get; set; } = 1000;
    /// <summary>Returns oldest findings first.</summary>
    public bool Oldest { get; set; }

    internal EventFindingStoreQuery Snapshot() {
        if (MaxFindings < 0) {
            throw new ArgumentOutOfRangeException(nameof(MaxFindings));
        }
        DateTime? start = StartTime?.ToUniversalTime();
        DateTime? end = EndTime?.ToUniversalTime();
        if (start.HasValue && end.HasValue && start > end) {
            throw new ArgumentException("StartTime cannot be later than EndTime.");
        }
        bool hasField = !string.IsNullOrWhiteSpace(EntityField);
        bool hasValue = !string.IsNullOrWhiteSpace(EntityValue);
        if (hasField != hasValue) {
            throw new ArgumentException("EntityField and EntityValue must be supplied together.");
        }
        return new EventFindingStoreQuery {
            StartTime = start,
            EndTime = end,
            RuleIds = EventStoreQuery.NormalizeTextValues(RuleIds),
            PackIds = EventStoreQuery.NormalizeTextValues(PackIds),
            Severities = Severities?.Distinct().ToArray(),
            Statuses = Statuses?.Distinct().ToArray(),
            EntityField = hasField ? EntityField!.Trim() : null,
            EntityValue = hasValue ? EntityValue!.Trim() : null,
            MaxFindings = MaxFindings,
            Oldest = Oldest
        };
    }
}
