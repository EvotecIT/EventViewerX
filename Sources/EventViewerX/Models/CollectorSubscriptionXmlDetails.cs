namespace EventViewerX;

using System;
using System.Collections.Generic;

/// <summary>
/// Normalized details extracted from a collector subscription XML payload.
/// </summary>
public sealed class CollectorSubscriptionXmlDetails {
    /// <summary>The normalized XML payload safe for comparison and write-back.</summary>
    public string NormalizedXml { get; set; } = string.Empty;

    /// <summary>Description extracted from the XML payload.</summary>
    public string? Description { get; set; }

    /// <summary>Normalized event queries extracted from the XML payload.</summary>
    public IReadOnlyList<string> Queries { get; set; } = Array.Empty<string>();
}
