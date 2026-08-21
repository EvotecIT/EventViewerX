using System;
using System.Collections.Generic;

namespace EventViewerX.Reports.Stats;

/// <summary>
/// Query contract for EVTX statistics aggregation.
/// </summary>
internal sealed class EvtxStatsQueryRequest {
    /// <summary>
    /// EVTX file path (absolute or relative).
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Optional event IDs to include.
    /// </summary>
    public IReadOnlyList<int>? EventIds { get; set; }

    /// <summary>
    /// Optional provider name filter.
    /// </summary>
    public string? ProviderName { get; set; }

    /// <summary>
    /// Optional UTC lower bound.
    /// </summary>
    public DateTime? StartTimeUtc { get; set; }

    /// <summary>
    /// Optional UTC upper bound.
    /// </summary>
    public DateTime? EndTimeUtc { get; set; }

    /// <summary>
    /// Maximum number of scanned events.
    /// </summary>
    public int MaxEventsScanned { get; set; }

    /// <summary>
    /// Read direction. When true, reads oldest to newest.
    /// </summary>
    public bool OldestFirst { get; set; }

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
}

/// <summary>
/// Top event ID count row.
/// </summary>
internal sealed class EvtxStatsTopEventIdRow {
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
internal sealed class EvtxStatsTopProviderRow {
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
internal sealed class EvtxStatsTopComputerRow {
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
/// Query result for EVTX stats aggregation.
/// </summary>
internal sealed class EvtxStatsQueryResult {
    /// <summary>
    /// Queried EVTX path.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Effective provider filter (empty when not set).
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Read direction used for query.
    /// </summary>
    public bool OldestFirst { get; set; }

    /// <summary>
    /// Maximum scanned events cap used by query.
    /// </summary>
    public int MaxEventsScanned { get; set; }

    /// <summary>
    /// Number of scanned events.
    /// </summary>
    public int ScannedEvents { get; set; }

    /// <summary>
    /// Number of scanned events whose source record did not expose a level.
    /// </summary>
    public long EventsWithoutLevel { get; set; }

    /// <summary>
    /// Indicates whether scanning stopped at cap.
    /// </summary>
    public bool Truncated { get; set; }

    /// <summary>
    /// Minimum event time in UTC.
    /// </summary>
    public DateTime? TimeCreatedUtcMin { get; set; }

    /// <summary>
    /// Maximum event time in UTC.
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
    /// Event IDs used by query.
    /// </summary>
    public IReadOnlyList<int>? EventIds { get; set; }

    /// <summary>
    /// Top event ID rows.
    /// </summary>
    public IReadOnlyList<EvtxStatsTopEventIdRow> TopEventIds { get; set; } = Array.Empty<EvtxStatsTopEventIdRow>();

    /// <summary>
    /// Top provider rows.
    /// </summary>
    public IReadOnlyList<EvtxStatsTopProviderRow> TopProviders { get; set; } = Array.Empty<EvtxStatsTopProviderRow>();

    /// <summary>
    /// Top computer rows.
    /// </summary>
    public IReadOnlyList<EvtxStatsTopComputerRow> TopComputers { get; set; } = Array.Empty<EvtxStatsTopComputerRow>();

    /// <summary>
    /// Top level rows.
    /// </summary>
    public IReadOnlyList<EvtxLevelStats> TopLevels { get; set; } = Array.Empty<EvtxLevelStats>();
}
