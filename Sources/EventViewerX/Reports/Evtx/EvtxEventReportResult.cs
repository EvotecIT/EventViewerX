using System;
using System.Collections.Generic;

namespace EventViewerX.Reports.Evtx;

/// <summary>
/// Single event row projected from EVTX query output.
/// </summary>
internal sealed class EvtxEventReportRow {
    /// <summary>
    /// Event creation time in UTC (ISO-8601), when present.
    /// </summary>
    public string? TimeCreatedUtc { get; set; }

    /// <summary>
    /// Event ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Event record ID, when present.
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
    /// Numeric level, when present.
    /// </summary>
    public long? Level { get; set; }

    /// <summary>
    /// Localized level name.
    /// </summary>
    public string LevelDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Event computer name.
    /// </summary>
    public string ComputerName { get; set; } = string.Empty;

    /// <summary>
    /// Queried machine name.
    /// </summary>
    public string QueriedMachine { get; set; } = string.Empty;

    /// <summary>
    /// Data source marker.
    /// </summary>
    public string GatheredFrom { get; set; } = string.Empty;

    /// <summary>
    /// Parsed message subject.
    /// </summary>
    public string MessageSubject { get; set; } = string.Empty;

    /// <summary>
    /// User SID value.
    /// </summary>
    public string UserSid { get; set; } = string.Empty;

    /// <summary>
    /// Event XML data dictionary.
    /// </summary>
    public IReadOnlyDictionary<string, string> Data { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Parsed message data dictionary.
    /// </summary>
    public IReadOnlyDictionary<string, string> MessageData { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Optional formatted message text.
    /// </summary>
    public string? Message { get; set; }
}

/// <summary>
/// EVTX event report result.
/// </summary>
internal sealed class EvtxEventReportResult {
    /// <summary>
    /// Queried EVTX path.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Number of returned rows.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Indicates whether output was capped.
    /// </summary>
    public bool Truncated { get; set; }

    /// <summary>
    /// Projected event rows.
    /// </summary>
    public IReadOnlyList<EvtxEventReportRow> Events { get; set; } = Array.Empty<EvtxEventReportRow>();
}
