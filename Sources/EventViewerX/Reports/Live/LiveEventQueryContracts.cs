using System;
using System.Collections.Generic;

namespace EventViewerX.Reports.Live;

/// <summary>
/// Query contract for reading events from a live event log channel.
/// </summary>
internal sealed class LiveEventQueryRequest {
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
    /// Maximum events to return. Defaults to 1,000; set 0 explicitly for unlimited materialization.
    /// </summary>
    public int MaxEvents { get; set; } = 1000;

    /// <summary>
    /// Read direction. When true, reads oldest to newest.
    /// </summary>
    public bool OldestFirst { get; set; }

    /// <summary>
    /// When true, includes formatted event message text.
    /// </summary>
    public bool IncludeMessage { get; set; }

    /// <summary>
    /// Maximum characters kept in formatted message text.
    /// </summary>
    public int MaxMessageChars { get; set; } = 4000;

    /// <summary>
    /// Optional remote session-establishment and per-read timeout override in
    /// milliseconds.
    /// </summary>
    public int? SessionTimeoutMs { get; set; }
}

/// <summary>
/// Canonical failure kinds produced by live event queries.
/// </summary>
internal enum LiveEventQueryFailureKind {
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
/// Failure payload produced by live event queries.
/// </summary>
internal sealed class LiveEventQueryFailure {
    /// <summary>
    /// Gets or sets failure kind.
    /// </summary>
    public LiveEventQueryFailureKind Kind { get; set; }

    /// <summary>
    /// Gets or sets failure message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Single event row returned by live event queries.
/// </summary>
internal sealed class LiveEventRow {
    /// <summary>
    /// Event creation time in UTC (ISO-8601), or <see langword="null"/> when the source record has no timestamp.
    /// </summary>
    public string? TimeCreatedUtc { get; set; }

    /// <summary>
    /// Event ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Event record ID, or <see langword="null"/> when the source record does not expose one.
    /// </summary>
    public long? RecordId { get; set; }

    /// <summary>
    /// Source log name.
    /// </summary>
    public string LogName { get; set; } = string.Empty;

    /// <summary>
    /// Provider name.
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Numeric event level, or <see langword="null"/> when the source record
    /// does not expose one.
    /// </summary>
    public long? Level { get; set; }

    /// <summary>
    /// Localized level name.
    /// </summary>
    public string LevelDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Task value, or <see langword="null"/> when the source record does not
    /// expose one.
    /// </summary>
    public long? Task { get; set; }

    /// <summary>
    /// Opcode value, or <see langword="null"/> when the source record does not
    /// expose one.
    /// </summary>
    public long? Opcode { get; set; }

    /// <summary>
    /// Keywords bitmask value, or <see langword="null"/> when the source record
    /// does not expose one.
    /// </summary>
    public long? Keywords { get; set; }

    /// <summary>
    /// Computer name.
    /// </summary>
    public string MachineName { get; set; } = string.Empty;

    /// <summary>
    /// User SID value.
    /// </summary>
    public string UserSid { get; set; } = string.Empty;

    /// <summary>
    /// Optional formatted event message.
    /// </summary>
    public string? Message { get; set; }
}

/// <summary>
/// Query result for live event reads.
/// </summary>
internal sealed class LiveEventQueryResult {
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
    /// Number of returned events.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Indicates whether output was truncated to the request cap.
    /// </summary>
    public bool Truncated { get; set; }

    /// <summary>
    /// Returned event rows.
    /// </summary>
    public IReadOnlyList<LiveEventRow> Events { get; set; } = Array.Empty<LiveEventRow>();
}
