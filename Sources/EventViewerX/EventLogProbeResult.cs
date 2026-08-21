namespace EventViewerX;

/// <summary>Outcome for a bounded event-log probe.</summary>
public sealed class EventLogProbeResult {
    /// <summary>Creates a probe result.</summary>
    public EventLogProbeResult(
        string logName,
        string machine,
        DateTime? eventTimeUtc,
        EventLogProbeStatus status,
        string? message,
        int eventsScanned,
        long? recordCount,
        TimeSpan duration,
        bool nativeQueryVerified) {

        LogName = logName;
        Machine = machine;
        EventTimeUtc = eventTimeUtc;
        Status = status;
        Message = message;
        EventsScanned = eventsScanned;
        RecordCount = recordCount;
        Duration = duration;
        NativeQueryVerified =
            nativeQueryVerified;
    }

    /// <summary>Log that was queried.</summary>
    public string LogName { get; }
    /// <summary>Machine that was queried.</summary>
    public string Machine { get; }
    /// <summary>Timestamp of the newest matching event in UTC.</summary>
    public DateTime? EventTimeUtc { get; }
    /// <summary>Outcome status.</summary>
    public EventLogProbeStatus Status { get; }
    /// <summary>Optional diagnostic message.</summary>
    public string? Message { get; }
    /// <summary>Number of events inspected.</summary>
    public int EventsScanned { get; }
    /// <summary>Channel record count when available.</summary>
    public long? RecordCount { get; }
    /// <summary>Total elapsed time.</summary>
    public TimeSpan Duration { get; }
    /// <summary>Whether the owned native reader successfully executed the supplied query.</summary>
    public bool NativeQueryVerified { get; }
}
