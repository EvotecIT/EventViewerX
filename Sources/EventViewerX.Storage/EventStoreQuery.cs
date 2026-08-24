using EventViewerX.Reporting;

namespace EventViewerX.Storage;

/// <summary>Defines a bounded query over locally stored normalized event rows.</summary>
public sealed class EventStoreQuery {
    /// <summary>Built-in typed definitions to include. Mutually exclusive with DefinitionNames and DefinitionSchemas.</summary>
    public IReadOnlyList<EventType>? Types { get; set; }
    /// <summary>Built-in or custom stable definition names to include. Mutually exclusive with Types.</summary>
    public IReadOnlyList<string>? DefinitionNames { get; set; }
    /// <summary>
    /// Optional detached schemas supplied by the caller for selected custom definitions. These preserve
    /// typed metadata when a fresh store contains no persisted rows or schema catalog entry.
    /// </summary>
    public IReadOnlyList<EventReportSectionSchema>? DefinitionSchemas { get; set; }
    /// <summary>Absolute lower timestamp boundary.</summary>
    public DateTime? StartTime { get; set; }
    /// <summary>Absolute upper timestamp boundary.</summary>
    public DateTime? EndTime { get; set; }
    /// <summary>Reusable relative timestamp selection.</summary>
    public TimePeriod? TimePeriod { get; set; }
    /// <summary>Exact event identifiers.</summary>
    public IReadOnlyList<int>? EventIds { get; set; }
    /// <summary>Exact source event record identifiers.</summary>
    public IReadOnlyList<long>? RecordIds { get; set; }
    /// <summary>Original source computers.</summary>
    public IReadOnlyList<string>? SourceComputers { get; set; }
    /// <summary>Original source channels.</summary>
    public IReadOnlyList<string>? SourceLogs { get; set; }
    /// <summary>Provider names.</summary>
    public IReadOnlyList<string>? Providers { get; set; }
    /// <summary>Optional exact typed predicate evaluated against normalized stored fields.</summary>
    public EventPredicate? Predicate { get; set; }
    /// <summary>Maximum rows returned. Zero is unlimited.</summary>
    public long MaxEvents { get; set; }
    /// <summary>Maximum candidate rows evaluated for managed predicates. Zero is unlimited.</summary>
    public long MaxCandidates { get; set; } = 100000;
    /// <summary>Returns oldest rows first.</summary>
    public bool Oldest { get; set; }

    internal EventStoreQuery Snapshot() {
        if (MaxEvents < 0) {
            throw new ArgumentOutOfRangeException(nameof(MaxEvents));
        }
        if (MaxCandidates < 0) {
            throw new ArgumentOutOfRangeException(nameof(MaxCandidates));
        }
        (DateTime? start, DateTime? end) = EventTimeRange.Resolve(StartTime, EndTime, TimePeriod);
        start = start?.ToUniversalTime();
        end = end?.ToUniversalTime();
        if (start.HasValue && end.HasValue && start > end) {
            throw new ArgumentException("StartTime cannot be later than EndTime.");
        }
        EventType[]? types = Types?.Distinct().ToArray();
        string[]? definitionNames = NormalizeTextValues(DefinitionNames);
        EventReportSectionSchema[]? definitionSchemas = DefinitionSchemas?
            .Select(CopySchema)
            .ToArray();
        if (definitionSchemas is { Length: 0 }) {
            definitionSchemas = null;
        }
        if (types is { Length: > 0 } &&
            (definitionNames is { Length: > 0 } || definitionSchemas is { Length: > 0 })) {
            throw new ArgumentException(
                "Types and DefinitionNames are mutually exclusive stored definition selectors; " +
                "DefinitionSchemas also cannot be combined with Types.");
        }
        if (definitionSchemas is { Length: > 0 }) {
            string[] suppliedNames = definitionSchemas.Select(static schema => schema.Name)
                .ToArray();
            if (definitionNames is { Length: > 0 } && suppliedNames.Any(name =>
                    !definitionNames.Contains(name, StringComparer.OrdinalIgnoreCase))) {
                throw new ArgumentException(
                    "Every supplied DefinitionSchema must match a selected DefinitionName.");
            }
            definitionNames ??= NormalizeTextValues(suppliedNames);
        }
        EventPredicate? predicate = Predicate?.Clone();
        predicate?.Validate();
        if (predicate != null && types != null && types.Length > 0) {
            predicate = IsEnrichedGroupPolicySelection(types)
                ? EventReportSectionSchema.FromGroupPolicyAudit().CreatePredicateBuilder().Normalize(predicate)
                : EventPredicateBuilder.ForTypes(types).Normalize(predicate);
        }
        return new EventStoreQuery {
            Types = types,
            DefinitionNames = definitionNames,
            DefinitionSchemas = definitionSchemas,
            StartTime = start,
            EndTime = end,
            EventIds = EventIds?.Distinct().ToArray(),
            RecordIds = RecordIds?.Distinct().ToArray(),
            SourceComputers = NormalizeTextValues(SourceComputers),
            SourceLogs = NormalizeTextValues(SourceLogs),
            Providers = NormalizeTextValues(Providers),
            Predicate = predicate,
            MaxEvents = MaxEvents,
            MaxCandidates = MaxCandidates,
            Oldest = Oldest
        };
    }

    internal string[] ResolveDefinitionNames() => (DefinitionNames ?? Array.Empty<string>())
        .Concat(EventTypeCatalog.Expand(Types ?? Array.Empty<EventType>())
            .SelectMany(static type => type == EventType.GroupPolicyDirectoryAudit
                ? new[] { type.ToString(), "GroupPolicyAudit" }
                : new[] { type.ToString() }))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    /// <summary>Returns true when the stored selector addresses the enriched persistent Group Policy audit surface.</summary>
    public static bool IsEnrichedGroupPolicySelection(IReadOnlyList<EventType>? types) =>
        types is { Count: 1 } && types[0] == EventType.GroupPolicyDirectoryAudit;

    /// <summary>Trims and deduplicates optional case-insensitive text selectors.</summary>
    internal static string[]? NormalizeTextValues(IReadOnlyList<string>? values) {
        if (values == null) {
            return null;
        }
        string[] normalized = values.Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return normalized.Length == 0 ? null : normalized;
    }

    private static EventReportSectionSchema CopySchema(EventReportSectionSchema schema) {
        if (schema == null) {
            throw new ArgumentException("DefinitionSchemas cannot contain null values.");
        }
        return new EventReportSectionSchema {
            Name = schema.Name,
            DisplayName = schema.DisplayName,
            Description = schema.Description,
            Kind = schema.Kind,
            Columns = (schema.Columns ?? Array.Empty<EventReportColumnSchema>())
                .Select(CopyColumn)
                .ToArray()
        };
    }

    private static EventReportColumnSchema CopyColumn(EventReportColumnSchema column) {
        if (column == null) {
            throw new ArgumentException("DefinitionSchemas cannot contain null columns.");
        }
        return new EventReportColumnSchema {
            Name = column.Name,
            DisplayName = column.DisplayName,
            ValueTypeName = column.ValueTypeName,
            Aliases = column.Aliases?.ToArray() ?? Array.Empty<string>()
        };
    }
}
