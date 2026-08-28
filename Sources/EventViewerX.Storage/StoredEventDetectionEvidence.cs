namespace EventViewerX.Storage;

/// <summary>Durable, report-ready evidence metadata retained with a detection finding.</summary>
public sealed class StoredEventDetectionEvidence {
    internal StoredEventDetectionEvidence(
        string identity,
        string typeName,
        int eventId,
        long? recordId,
        string providerName,
        string sourceLog,
        string containerLog,
        string sourceComputer,
        string collectorComputer,
        DateTime eventTimeUtc,
        DateTime receivedTimeUtc,
        DateTime processedTimeUtc) {

        Identity = identity;
        TypeName = typeName;
        EventId = eventId;
        RecordId = recordId;
        ProviderName = providerName;
        SourceLog = sourceLog;
        ContainerLog = containerLog;
        SourceComputer = sourceComputer;
        CollectorComputer = collectorComputer;
        EventTimeUtc = eventTimeUtc;
        ReceivedTimeUtc = receivedTimeUtc;
        ProcessedTimeUtc = processedTimeUtc;
    }

    /// <summary>Stable EventViewerX evidence identity.</summary>
    public string Identity { get; }
    /// <summary>Projected event type name.</summary>
    public string TypeName { get; }
    /// <summary>Windows event identifier.</summary>
    public int EventId { get; }
    /// <summary>Source record identifier when available.</summary>
    public long? RecordId { get; }
    /// <summary>Provider that emitted the event.</summary>
    public string ProviderName { get; }
    /// <summary>Original source channel.</summary>
    public string SourceLog { get; }
    /// <summary>Channel or file containing the event.</summary>
    public string ContainerLog { get; }
    /// <summary>Computer that emitted the event.</summary>
    public string SourceComputer { get; }
    /// <summary>Collector or direct query target.</summary>
    public string CollectorComputer { get; }
    /// <summary>UTC source-event time.</summary>
    public DateTime EventTimeUtc { get; }
    /// <summary>UTC ingestion time.</summary>
    public DateTime ReceivedTimeUtc { get; }
    /// <summary>UTC processing time.</summary>
    public DateTime ProcessedTimeUtc { get; }
}
