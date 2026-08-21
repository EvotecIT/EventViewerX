using System;
using System.Collections.Generic;
using EventViewerX.Reports.Stats;

namespace EventViewerX.Reports.Live;

/// <summary>
/// Query contract for aggregating live event log statistics.
/// </summary>
internal sealed class LiveStatsQueryRequest {
    /// <summary>
    /// Log name (for example <c>Security</c>, <c>System</c>, <c>Application</c>).
    /// </summary>
    public string LogName { get; set; } = string.Empty;

    /// <summary>
    /// Optional XPath query. Defaults to <c>*</c>.
    /// </summary>
    public string? XPath { get; set; }

    /// <summary>
    /// Optional target machine name. Null/empty targets local machine.
    /// </summary>
    public string? MachineName { get; set; }

    /// <summary>
    /// Maximum events scanned from the live channel.
    /// </summary>
    public long MaxEventsScanned { get; set; }

    /// <summary>
    /// Read direction. When true, reads oldest to newest.
    /// </summary>
    public bool OldestFirst { get; set; }

    /// <summary>
    /// Optional UTC lower bound for matched events.
    /// </summary>
    public DateTime? StartTimeUtc { get; set; }

    /// <summary>
    /// Optional UTC upper bound for matched events.
    /// </summary>
    public DateTime? EndTimeUtc { get; set; }

    /// <summary>
    /// Number of top event IDs to include.
    /// </summary>
    public int TopEventIds { get; set; } = 10;

    /// <summary>
    /// Number of top providers to include.
    /// </summary>
    public int TopProviders { get; set; } = 10;

    /// <summary>
    /// Number of top levels to include.
    /// </summary>
    public int TopLevels { get; set; } = 10;

    /// <summary>
    /// Number of top computers to include.
    /// </summary>
    public int TopComputers { get; set; } = 10;

    /// <summary>
    /// Optional remote session-establishment and per-read timeout override in
    /// milliseconds.
    /// </summary>
    public int? SessionTimeoutMs { get; set; }
}

/// <summary>
/// Canonical failure kinds produced by live stats queries.
/// </summary>
internal enum LiveStatsQueryFailureKind {
    /// <summary>
    /// Invalid request arguments.
    /// </summary>
    InvalidArgument,

    /// <summary>The supplied XPath expression is invalid.</summary>
    InvalidQuery,

    /// <summary>The requested event log channel does not exist.</summary>
    LogNotFound,

    /// <summary>
    /// Access to event logs was denied.
    /// </summary>
    AccessDenied,

    /// <summary>
    /// Event log session or read timed out.
    /// </summary>
    Timeout,

    /// <summary>The target host or Event Log RPC endpoint is unavailable.</summary>
    HostUnavailable,

    /// <summary>
    /// Unexpected failure.
    /// </summary>
    Exception
}

/// <summary>
/// Failure payload produced by live stats queries.
/// </summary>
internal sealed class LiveStatsQueryFailure {
    /// <summary>
    /// Gets or sets failure kind.
    /// </summary>
    public LiveStatsQueryFailureKind Kind { get; set; }

    /// <summary>
    /// Gets or sets failure message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Top event ID count row.
/// </summary>
internal sealed class TopEventIdRow {
    /// <summary>
    /// Event ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Count.
    /// </summary>
    public long Count { get; set; }
}

/// <summary>
/// Top provider count row.
/// </summary>
internal sealed class TopProviderRow {
    /// <summary>
    /// Provider name.
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Count.
    /// </summary>
    public long Count { get; set; }
}

/// <summary>
/// Top computer count row.
/// </summary>
internal sealed class TopComputerRow {
    /// <summary>
    /// Computer name.
    /// </summary>
    public string ComputerName { get; set; } = string.Empty;

    /// <summary>
    /// Count.
    /// </summary>
    public long Count { get; set; }
}

/// <summary>
/// Query result for live stats reads.
/// </summary>
internal sealed class LiveStatsQueryResult {
    /// <summary>
    /// Effective machine queried. The local machine name is used when no remote target was supplied.
    /// </summary>
    public string MachineName { get; set; } = string.Empty;

    /// <summary>
    /// Queried log name.
    /// </summary>
    public string LogName { get; set; } = string.Empty;

    /// <summary>
    /// Effective XPath query.
    /// </summary>
    public string XPath { get; set; } = string.Empty;

    /// <summary>
    /// Read direction used for query.
    /// </summary>
    public bool OldestFirst { get; set; }

    /// <summary>
    /// Maximum scanned events cap used by query.
    /// </summary>
    public long MaxEventsScanned { get; set; }

    /// <summary>
    /// Number of scanned events.
    /// </summary>
    public long ScannedEvents { get; set; }

    /// <summary>
    /// Number of matched events whose source record did not expose a level.
    /// </summary>
    public long EventsWithoutLevel { get; set; }

    /// <summary>
    /// Number of matched events (after time filters).
    /// </summary>
    public long MatchedEvents { get; set; }

    /// <summary>
    /// Indicates whether scanning stopped at cap.
    /// </summary>
    public bool Truncated { get; set; }

    /// <summary>
    /// Minimum matched event time in UTC.
    /// </summary>
    public DateTime? TimeCreatedUtcMin { get; set; }

    /// <summary>
    /// Maximum matched event time in UTC.
    /// </summary>
    public DateTime? TimeCreatedUtcMax { get; set; }

    /// <summary>
    /// Effective query start bound.
    /// </summary>
    public DateTime? StartTimeUtc { get; set; }

    /// <summary>
    /// Effective query end bound.
    /// </summary>
    public DateTime? EndTimeUtc { get; set; }

    /// <summary>
    /// Top event ID rows.
    /// </summary>
    public IReadOnlyList<TopEventIdRow> TopEventIds { get; set; } = Array.Empty<TopEventIdRow>();

    /// <summary>
    /// Top provider rows.
    /// </summary>
    public IReadOnlyList<TopProviderRow> TopProviders { get; set; } = Array.Empty<TopProviderRow>();

    /// <summary>
    /// Top computer rows.
    /// </summary>
    public IReadOnlyList<TopComputerRow> TopComputers { get; set; } = Array.Empty<TopComputerRow>();

    /// <summary>
    /// Top level rows.
    /// </summary>
    public IReadOnlyList<EvtxLevelStats> TopLevels { get; set; } = Array.Empty<EvtxLevelStats>();
}
