namespace EventViewerX;

/// <summary>Outcome represented by a detection finding.</summary>
public enum EventDetectionFindingStatus {
    /// <summary>The rule matched with complete required evidence.</summary>
    Matched,
    /// <summary>A safety bound prevented a complete result.</summary>
    Incomplete,
    /// <summary>Rule evaluation failed for the supplied evidence.</summary>
    Error
}
