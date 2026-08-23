namespace EventViewerX;

/// <summary>Resolved state of an object at a requested event time.</summary>
public enum EventContextState {
    /// <summary>No applicable fact is available.</summary>
    Unknown = 0,
    /// <summary>The object is known at the latest stored point in its timeline.</summary>
    Current = 1,
    /// <summary>The requested time precedes a later stored point in the object's timeline.</summary>
    Historical = 2,
    /// <summary>The object was deleted at or before the requested time.</summary>
    Deleted = 3,
    /// <summary>Facts with the same effective time disagree about material state.</summary>
    Ambiguous = 4
}
