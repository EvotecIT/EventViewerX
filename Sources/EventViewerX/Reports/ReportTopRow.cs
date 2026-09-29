using System.Collections.Generic;

namespace EventViewerX.Reports;

/// <summary>
/// Generic key/count row used by report aggregates for top-N result shaping.
/// </summary>
public sealed class ReportTopRow {
    /// <summary>
    /// Aggregate key payload (for example: <c>{ "user": "alice" }</c>).
    /// </summary>
    public IReadOnlyDictionary<string, object?> Key { get; set; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>
    /// Count for the aggregate key.
    /// </summary>
    public long Count { get; set; }
}
