namespace EventViewerX;

/// <summary>Starting position for a live Windows Event Log subscription.</summary>
public enum EventLogSubscriptionStart {
    /// <summary>Only events written after the subscription starts.</summary>
    Future = 0,

    /// <summary>Existing matching events are delivered before new events.</summary>
    Oldest = 1,

    /// <summary>Delivery resumes immediately after BookmarkXml.</summary>
    AfterBookmark = 2
}
