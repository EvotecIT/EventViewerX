namespace EventViewerX;

/// <summary>Origin of a context fact.</summary>
public enum EventContextProvenance {
    /// <summary>The fact was carried by a selected Windows event.</summary>
    Event = 1,
    /// <summary>The fact was supplied by an explicit, bounded compiled lookup.</summary>
    LiveLookup = 2,
    /// <summary>The fact was supplied by a versioned external evidence import.</summary>
    Imported = 3
}
