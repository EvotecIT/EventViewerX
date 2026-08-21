using System.Collections;

namespace EventViewerX;

/// <summary>
/// Raw structured filter input supplied by hosts before normalization.
/// </summary>
public sealed class EventStructuredQueryFilterInput {
    /// <summary>
    /// Optional event IDs.
    /// </summary>
    public IReadOnlyList<int>? EventIds { get; set; }

    /// <summary>
    /// Optional provider name.
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
    /// Optional raw level name or numeric token.
    /// </summary>
    public string? Level { get; set; }

    /// <summary>
    /// Optional raw keywords name or numeric token.
    /// </summary>
    public string? Keywords { get; set; }

    /// <summary>
    /// Optional user SID/account filter.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Optional event record IDs.
    /// </summary>
    public IReadOnlyList<long>? RecordIds { get; set; }

    /// <summary>
    /// Optional EventData include filters.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>>? NamedDataFilter { get; set; }

    /// <summary>
    /// Optional EventData exclude filters.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>>? NamedDataExcludeFilter { get; set; }
}

/// <summary>
/// Normalized structured query filter used by EventViewerX query executors.
/// </summary>
public sealed class EventStructuredQueryFilter {
    /// <summary>
    /// Optional normalized event IDs.
    /// </summary>
    public IReadOnlyList<int>? EventIds { get; set; }

    /// <summary>
    /// Optional normalized provider name.
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
    /// Optional normalized standard or custom provider level in the Windows
    /// Event Log range from 0 through 255.
    /// </summary>
    public Level? Level { get; set; }

    /// <summary>
    /// Optional normalized keywords mask.
    /// </summary>
    public Keywords? Keywords { get; set; }

    /// <summary>
    /// Optional normalized user SID/account filter.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Optional normalized event record IDs.
    /// </summary>
    public IReadOnlyList<long>? RecordIds { get; set; }

    /// <summary>
    /// Optional normalized EventData include filters.
    /// </summary>
    public Hashtable? NamedDataFilter { get; set; }

    /// <summary>
    /// Optional normalized EventData exclude filters.
    /// </summary>
    public Hashtable? NamedDataExcludeFilter { get; set; }
}
