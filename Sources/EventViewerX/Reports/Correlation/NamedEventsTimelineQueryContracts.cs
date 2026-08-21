using System;
using System.Collections.Generic;

namespace EventViewerX.Reports.Correlation;

/// <summary>
/// Query contract for building timeline and correlation views from named-event detections.
/// </summary>
internal sealed class NamedEventsTimelineQueryRequest {
    /// <summary>
    /// Named events to query.
    /// </summary>
    public IReadOnlyList<EventType>? EventType { get; set; }

    /// <summary>
    /// Optional remote machine names. Null/empty targets local machine.
    /// </summary>
    public IReadOnlyList<string>? MachineNames { get; set; }

    /// <summary>
    /// Optional UTC lower bound.
    /// </summary>
    public DateTime? StartTimeUtc { get; set; }

    /// <summary>
    /// Optional UTC upper bound.
    /// </summary>
    public DateTime? EndTimeUtc { get; set; }

    /// <summary>
    /// Optional relative time period. When set, takes precedence over explicit UTC bounds.
    /// </summary>
    public TimePeriod? TimePeriod { get; set; }

    /// <summary>
    /// Optional exact log name filter.
    /// </summary>
    public string? LogName { get; set; }

    /// <summary>
    /// Optional event IDs filter.
    /// </summary>
    public IReadOnlyList<int>? EventIds { get; set; }

    /// <summary>
    /// Maximum events returned by the query.
    /// </summary>
    public int MaxEvents { get; set; } = 500;

    /// <summary>
    /// Maximum raw event candidates evaluated before named-event and correlation filtering. Zero disables the scan cap.
    /// </summary>
    public int MaxEventsScanned { get; set; } = 5000;

    /// <summary>
    /// Maximum query concurrency.
    /// </summary>
    public int MaxThreads { get; set; } = 4;

    /// <summary>
    /// Optional per-named-event cap.
    /// </summary>
    public int? MaxEventsPerNamedEvent { get; set; }

    /// <summary>
    /// Correlation key dimensions.
    /// </summary>
    public IReadOnlyList<string>? CorrelationKeys { get; set; }

    /// <summary>
    /// When false, rows missing all correlation dimensions are excluded.
    /// </summary>
    public bool IncludeUncorrelated { get; set; } = true;

    /// <summary>
    /// Maximum correlation groups returned.
    /// </summary>
    public int MaxGroups { get; set; } = 250;

    /// <summary>
    /// Bucket size in minutes used by density view.
    /// </summary>
    public int BucketMinutes { get; set; } = 15;

    /// <summary>
    /// When true, payload values are included in timeline rows.
    /// </summary>
    public bool IncludePayload { get; set; }

    /// <summary>
    /// Optional payload key allow-list (snake_case).
    /// </summary>
    public IReadOnlyList<string>? PayloadKeys { get; set; }
}

/// <summary>
/// Canonical failure kinds produced by timeline queries.
/// </summary>
internal enum NamedEventsTimelineQueryFailureKind {
    /// <summary>
    /// Invalid request arguments.
    /// </summary>
    InvalidArgument,

    /// <summary>
    /// Query execution failed with an expected runtime failure.
    /// </summary>
    QueryFailed,

    /// <summary>
    /// Unexpected runtime failure.
    /// </summary>
    Exception
}

/// <summary>
/// Failure payload produced by timeline queries.
/// </summary>
internal sealed class NamedEventsTimelineQueryFailure {
    /// <summary>
    /// Failure kind.
    /// </summary>
    public NamedEventsTimelineQueryFailureKind Kind { get; set; }

    /// <summary>
    /// Failure message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Timeline row emitted by named-events correlation query.
/// </summary>
internal sealed class NamedEventsTimelineEventRow {
    /// <summary>
    /// Sequence number after timeline ordering.
    /// </summary>
    public int Sequence { get; set; }

    /// <summary>
    /// Stable correlation identifier derived from correlation key values.
    /// Uses a compact 64-bit (8-byte) SHA-256 prefix intended for investigation-scale grouping.
    /// High-volume archival scenarios should also retain raw <see cref="Correlation"/> key/value data.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Correlation key/value map.
    /// </summary>
    public IReadOnlyDictionary<string, string> Correlation { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Named-event alias in snake_case.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Concrete rule type name.
    /// </summary>
    public string RuleType { get; set; } = string.Empty;

    /// <summary>
    /// Event ID.
    /// </summary>
    public int EventId { get; set; }

    /// <summary>
    /// Event record ID.
    /// </summary>
    public long? RecordId { get; set; }

    /// <summary>
    /// Source machine name.
    /// </summary>
    public string GatheredFrom { get; set; } = string.Empty;

    /// <summary>
    /// Source log name.
    /// </summary>
    public string GatheredLogName { get; set; } = string.Empty;

    /// <summary>
    /// Event time (UTC ISO-8601 string) when available.
    /// </summary>
    public string? WhenUtc { get; set; }

    /// <summary>
    /// Normalized actor identity when available.
    /// </summary>
    public string? Who { get; set; }

    /// <summary>
    /// Normalized object identity when available.
    /// </summary>
    public string? ObjectAffected { get; set; }

    /// <summary>
    /// Normalized computer field when available.
    /// </summary>
    public string? Computer { get; set; }

    /// <summary>
    /// Normalized action field when available.
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// Projected payload values.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Payload { get; set; } = new Dictionary<string, object?>();
}

/// <summary>
/// Correlation group row emitted by named-events correlation query.
/// </summary>
internal sealed class NamedEventsTimelineGroupRow {
    /// <summary>
    /// Correlation identifier.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Correlation key/value map.
    /// </summary>
    public IReadOnlyDictionary<string, string> Correlation { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Number of events in the correlation group.
    /// </summary>
    public int EventCount { get; set; }

    /// <summary>
    /// Earliest event time in UTC when available.
    /// </summary>
    public string? FirstSeenUtc { get; set; }

    /// <summary>
    /// Latest event time in UTC when available.
    /// </summary>
    public string? LastSeenUtc { get; set; }

    /// <summary>
    /// Group duration in minutes when both endpoints are available.
    /// </summary>
    public double? DurationMinutes { get; set; }

    /// <summary>
    /// Distinct named events in the group.
    /// </summary>
    public IReadOnlyList<string> EventType { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Distinct event IDs in the group.
    /// </summary>
    public IReadOnlyList<int> EventIds { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Distinct source machines in the group.
    /// </summary>
    public IReadOnlyList<string> Machines { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Timeline density bucket row emitted by named-events correlation query.
/// </summary>
internal sealed class NamedEventsTimelineBucketRow {
    /// <summary>
    /// Bucket start in UTC ISO-8601 format.
    /// </summary>
    public string BucketStartUtc { get; set; } = string.Empty;

    /// <summary>
    /// Bucket end in UTC ISO-8601 format.
    /// </summary>
    public string BucketEndUtc { get; set; } = string.Empty;

    /// <summary>
    /// Number of events in the bucket.
    /// </summary>
    public int EventCount { get; set; }

    /// <summary>
    /// Number of distinct correlation IDs in the bucket.
    /// </summary>
    public int CorrelationCount { get; set; }
}

/// <summary>
/// Result payload produced by timeline correlation query.
/// </summary>
internal sealed class NamedEventsTimelineQueryResult {
    /// <summary>
    /// Effective named-event aliases requested.
    /// </summary>
    public IReadOnlyList<string> RequestedNamedEvents { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Effective target machines.
    /// </summary>
    public IReadOnlyList<string> EffectiveMachines { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Effective query start bound.
    /// </summary>
    public DateTime? StartTimeUtc { get; set; }

    /// <summary>
    /// Effective query end bound.
    /// </summary>
    public DateTime? EndTimeUtc { get; set; }

    /// <summary>
    /// Maximum events cap used by the query.
    /// </summary>
    public int MaxEvents { get; set; }

    /// <summary>
    /// Maximum raw candidate scan cap used by the query. Zero means unlimited.
    /// </summary>
    public int MaxEventsScanned { get; set; }

    /// <summary>
    /// Number of raw event candidates evaluated by named-event rules.
    /// </summary>
    public long EventsScanned { get; set; }

    /// <summary>
    /// Effective per-named-event output cap. Null means no per-name cap.
    /// </summary>
    public int? MaxEventsPerNamedEvent { get; set; }

    /// <summary>
    /// Maximum worker concurrency used by the query.
    /// </summary>
    public int MaxThreads { get; set; }

    /// <summary>
    /// Effective correlation keys.
    /// </summary>
    public IReadOnlyList<string> CorrelationKeys { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Indicates whether uncorrelated rows were included.
    /// </summary>
    public bool IncludeUncorrelated { get; set; }

    /// <summary>
    /// Bucket size in minutes used by density view.
    /// </summary>
    public int BucketMinutes { get; set; }

    /// <summary>
    /// Indicates an output/scan/per-name cap or an isolated target failure made the result incomplete.
    /// </summary>
    public bool Truncated { get; set; }

    /// <summary>
    /// Indicates another matching timeline row existed beyond the output cap.
    /// </summary>
    public bool OutputTruncated { get; set; }

    /// <summary>
    /// Indicates another raw candidate existed beyond the candidate scan cap.
    /// </summary>
    public bool ScanTruncated { get; set; }

    /// <summary>
    /// Indicates one or more named-event categories had additional rows beyond the per-name cap.
    /// </summary>
    public bool PerNamedEventTruncated { get; set; }

    /// <summary>
    /// Named-event categories that had additional rows beyond the per-name cap.
    /// </summary>
    public IReadOnlyList<string> TruncatedNamedEvents { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Indicates that one or more requested remote targets could not be queried.
    /// </summary>
    public bool Incomplete { get; set; }

    /// <summary>
    /// Expected per-target failures isolated while other requested targets continued.
    /// </summary>
    public IReadOnlyList<EventLogQueryTargetFailure> TargetFailures { get; set; } =
        Array.Empty<EventLogQueryTargetFailure>();

    /// <summary>
    /// Indicates correlation groups were truncated by max group cap.
    /// </summary>
    public bool GroupsTruncated { get; set; }

    /// <summary>
    /// Total correlation groups before max group truncation.
    /// </summary>
    public int GroupsTotal { get; set; }

    /// <summary>
    /// Number of rows filtered by post-query filters.
    /// </summary>
    public int FilteredOut { get; set; }

    /// <summary>
    /// Number of rows filtered for missing correlation values when uncorrelated rows were disabled.
    /// </summary>
    public int FilteredUncorrelated { get; set; }

    /// <summary>
    /// Ordered timeline rows.
    /// </summary>
    public IReadOnlyList<NamedEventsTimelineEventRow> Timeline { get; set; } = Array.Empty<NamedEventsTimelineEventRow>();

    /// <summary>
    /// Correlation group rows.
    /// </summary>
    public IReadOnlyList<NamedEventsTimelineGroupRow> CorrelationGroups { get; set; } = Array.Empty<NamedEventsTimelineGroupRow>();

    /// <summary>
    /// Timeline density buckets.
    /// </summary>
    public IReadOnlyList<NamedEventsTimelineBucketRow> Buckets { get; set; } = Array.Empty<NamedEventsTimelineBucketRow>();
}
