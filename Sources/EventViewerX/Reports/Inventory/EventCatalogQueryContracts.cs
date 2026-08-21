using System.Collections.Generic;

namespace EventViewerX.Reports.Inventory;

/// <summary>
/// Shared request for event log catalog queries.
/// </summary>
internal sealed class EventCatalogQueryRequest {
    /// <summary>
    /// Optional target machine name. When null/empty, local machine is used.
    /// </summary>
    public string? MachineName { get; set; }

    /// <summary>
    /// Optional case-insensitive substring filter for names.
    /// </summary>
    public string? NameContains { get; set; }

    /// <summary>
    /// Maximum rows to return. Use 0 for no explicit cap.
    /// </summary>
    public int MaxResults { get; set; }

    /// <summary>
    /// Optional session timeout override in milliseconds.
    /// </summary>
    public int? SessionTimeoutMs { get; set; }
}

/// <summary>
/// Canonical error kinds produced by event catalog queries.
/// </summary>
internal enum EventCatalogFailureKind {
    /// <summary>
    /// Invalid request arguments.
    /// </summary>
    InvalidArgument,

    /// <summary>
    /// Access to event log catalog was denied.
    /// </summary>
    AccessDenied,

    /// <summary>
    /// Event Log session creation exceeded its configured deadline.
    /// </summary>
    Timeout,

    /// <summary>
    /// The target host is unreachable or temporarily negative-cached.
    /// </summary>
    HostUnavailable,

    /// <summary>
    /// Unexpected failure.
    /// </summary>
    Exception
}

/// <summary>
/// Failure payload produced by event catalog queries.
/// </summary>
internal sealed class EventCatalogFailure {
    /// <summary>
    /// Gets or sets failure kind.
    /// </summary>
    public EventCatalogFailureKind Kind { get; set; }

    /// <summary>
    /// Gets or sets failure message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Event log channel row.
/// </summary>
internal sealed class EventChannelRow {
    /// <summary>
    /// Channel name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Event provider row.
/// </summary>
internal sealed class EventProviderRow {
    /// <summary>
    /// Provider name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Query result for channel listing.
/// </summary>
internal sealed class EventChannelListResult {
    /// <summary>
    /// Returned channel count.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Indicates whether output was truncated to a cap.
    /// </summary>
    public bool Truncated { get; set; }

    /// <summary>
    /// Channel rows.
    /// </summary>
    public IReadOnlyList<EventChannelRow> Channels { get; set; } = new List<EventChannelRow>();
}

/// <summary>
/// Query result for provider listing.
/// </summary>
internal sealed class EventProviderListResult {
    /// <summary>
    /// Returned provider count.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Indicates whether output was truncated to a cap.
    /// </summary>
    public bool Truncated { get; set; }

    /// <summary>
    /// Provider rows.
    /// </summary>
    public IReadOnlyList<EventProviderRow> Providers { get; set; } = new List<EventProviderRow>();
}
