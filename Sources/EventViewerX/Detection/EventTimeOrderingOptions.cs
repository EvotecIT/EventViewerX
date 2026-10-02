namespace EventViewerX;

/// <summary>Action when an observation arrives before the event-time watermark.</summary>
public enum EventLateArrivalPolicy {
    /// <summary>Skip the observation and emit an incomplete-evaluation finding.</summary>
    SkipWithDiagnostic,
    /// <summary>Terminate execution with an InvalidDataException.</summary>
    Throw
}

/// <summary>Immutable bounded event-time ordering policy for a live detection stream.</summary>
public sealed class EventTimeOrderingOptions {
    /// <summary>Creates an ordering window; a zero window preserves immediate evaluation.</summary>
    public EventTimeOrderingOptions(TimeSpan reorderWindow, int maximumBufferedObservations = 4096,
        long maximumBufferedBytes = 16L * 1024 * 1024,
        EventLateArrivalPolicy lateArrivalPolicy = EventLateArrivalPolicy.SkipWithDiagnostic) {
        if (reorderWindow < TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(reorderWindow));
        }
        if (maximumBufferedObservations <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maximumBufferedObservations));
        }
        if (maximumBufferedBytes <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maximumBufferedBytes));
        }
        if (!Enum.IsDefined(typeof(EventLateArrivalPolicy), lateArrivalPolicy)) {
            throw new ArgumentOutOfRangeException(nameof(lateArrivalPolicy));
        }
        ReorderWindow = reorderWindow;
        MaximumBufferedObservations = maximumBufferedObservations;
        MaximumBufferedBytes = maximumBufferedBytes;
        LateArrivalPolicy = lateArrivalPolicy;
    }
    /// <summary>Accepted event-time lateness relative to the largest timestamp observed so far.</summary>
    public TimeSpan ReorderWindow { get; }
    /// <summary>Maximum observations retained in addition to correlation state.</summary>
    public int MaximumBufferedObservations { get; }
    /// <summary>Maximum estimated bytes retained in the reorder buffer.</summary>
    public long MaximumBufferedBytes { get; }
    /// <summary>Action for observations outside the accepted event-time window.</summary>
    public EventLateArrivalPolicy LateArrivalPolicy { get; }
}