using EventViewerX.Reporting;

namespace EventViewerX.Storage;

/// <summary>Builds a validated detached selector for normalized event history.</summary>
public sealed class EventStoreQueryBuilder {
    /// <summary>Built-in typed definitions to include.</summary>
    public IEnumerable<EventType>? Types { get; set; }
    /// <summary>Stable built-in or custom definition names.</summary>
    public IEnumerable<string>? DefinitionNames { get; set; }
    /// <summary>Detached custom definition schemas.</summary>
    public IEnumerable<EventReportSectionSchema>? DefinitionSchemas { get; set; }
    /// <summary>Absolute lower timestamp boundary.</summary>
    public DateTime? StartTime { get; set; }
    /// <summary>Absolute upper timestamp boundary.</summary>
    public DateTime? EndTime { get; set; }
    /// <summary>Reusable relative timestamp selection.</summary>
    public TimePeriod? TimePeriod { get; set; }
    /// <summary>Exact event identifiers.</summary>
    public IEnumerable<int>? EventIds { get; set; }
    /// <summary>Exact source record identifiers.</summary>
    public IEnumerable<long>? RecordIds { get; set; }
    /// <summary>Original source computers.</summary>
    public IEnumerable<string>? SourceComputers { get; set; }
    /// <summary>Original source channels.</summary>
    public IEnumerable<string>? SourceLogs { get; set; }
    /// <summary>Provider names.</summary>
    public IEnumerable<string>? Providers { get; set; }
    /// <summary>Exact typed predicate evaluated against normalized fields.</summary>
    public EventPredicate? Predicate { get; set; }
    /// <summary>Maximum returned rows. Zero is unlimited.</summary>
    public long MaxEvents { get; set; }
    /// <summary>Maximum managed-verification candidates. Zero is unlimited.</summary>
    public long MaxCandidates { get; set; } = 100_000;
    /// <summary>Whether rows are returned oldest first.</summary>
    public bool Oldest { get; set; }

    /// <summary>Loads the typed selection owned by one monitoring preset.</summary>
    public EventStoreQueryBuilder FromPreset(EventMonitoringPreset preset) {
        EventStoreQuery query = EventStoreQuery.ForPreset(preset);
        Types = query.Types;
        Predicate = query.Predicate;
        return this;
    }

    /// <summary>Validates, normalizes, and detaches the current selector.</summary>
    public EventStoreQuery Build() => new EventStoreQuery {
        Types = Types?.ToArray(),
        DefinitionNames = DefinitionNames?.ToArray(),
        DefinitionSchemas = DefinitionSchemas?.ToArray(),
        StartTime = StartTime,
        EndTime = EndTime,
        TimePeriod = TimePeriod,
        EventIds = EventIds?.ToArray(),
        RecordIds = RecordIds?.ToArray(),
        SourceComputers = SourceComputers?.ToArray(),
        SourceLogs = SourceLogs?.ToArray(),
        Providers = Providers?.ToArray(),
        Predicate = Predicate?.Clone(),
        MaxEvents = MaxEvents,
        MaxCandidates = MaxCandidates,
        Oldest = Oldest
    }.Snapshot();
}

/// <summary>Builds a validated detached selector for durable detection findings.</summary>
public sealed class EventFindingStoreQueryBuilder {
    /// <summary>Inclusive UTC lower evidence boundary.</summary>
    public DateTime? StartTime { get; set; }
    /// <summary>Inclusive UTC upper evidence boundary.</summary>
    public DateTime? EndTime { get; set; }
    /// <summary>Rule identifiers to include.</summary>
    public IEnumerable<string>? RuleIds { get; set; }
    /// <summary>Pack identifiers to include.</summary>
    public IEnumerable<string>? PackIds { get; set; }
    /// <summary>Effective severities to include.</summary>
    public IEnumerable<EventDetectionSeverity>? Severities { get; set; }
    /// <summary>Finding statuses to include.</summary>
    public IEnumerable<EventDetectionFindingStatus>? Statuses { get; set; }
    /// <summary>Exact entity field name.</summary>
    public string? EntityField { get; set; }
    /// <summary>Exact entity value.</summary>
    public string? EntityValue { get; set; }
    /// <summary>Maximum findings returned. Zero is unlimited.</summary>
    public int MaxFindings { get; set; } = 1000;
    /// <summary>Whether findings are returned oldest first.</summary>
    public bool Oldest { get; set; }

    /// <summary>Validates, normalizes, and detaches the current selector.</summary>
    public EventFindingStoreQuery Build() => new EventFindingStoreQuery {
        StartTime = StartTime,
        EndTime = EndTime,
        RuleIds = RuleIds?.ToArray(),
        PackIds = PackIds?.ToArray(),
        Severities = Severities?.ToArray(),
        Statuses = Statuses?.ToArray(),
        EntityField = EntityField,
        EntityValue = EntityValue,
        MaxFindings = MaxFindings,
        Oldest = Oldest
    }.Snapshot();
}
