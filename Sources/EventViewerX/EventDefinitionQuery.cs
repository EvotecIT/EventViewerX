using System.Globalization;
using System.Net;

namespace EventViewerX;

/// <summary>Defines an optimized query for one declarative event definition.</summary>
public sealed class EventDefinitionQuery {
    /// <summary>Creates a query.</summary>
    public EventDefinitionQuery(EventDefinition definition) {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }
    /// <summary>Definition to execute.</summary>
    public EventDefinition Definition { get; }
    /// <summary>
    /// Optional offline event-log files. The definition still owns source channels,
    /// event identifiers, providers, and projected fields.
    /// </summary>
    public IReadOnlyList<string>? Paths { get; set; }
    /// <summary>Direct targets or collector computers.</summary>
    public IReadOnlyList<string?>? MachineNames { get; set; }
    /// <summary>Collector channel, normally ForwardedEvents.</summary>
    public string? CollectorLogName { get; set; }
    /// <summary>Absolute start time.</summary>
    public DateTime? StartTime { get; set; }
    /// <summary>Absolute end time.</summary>
    public DateTime? EndTime { get; set; }
    /// <summary>Relative time window.</summary>
    public TimePeriod? TimePeriod { get; set; }
    /// <summary>Optional exact source event record identifiers.</summary>
    public IReadOnlyCollection<long>? RecordIds { get; set; }
    /// <summary>Maximum matches. Zero is unlimited.</summary>
    public long MaxEvents { get; set; }
    /// <summary>Maximum raw candidates evaluated before custom projection. Zero is unlimited.</summary>
    public long MaxCandidates { get; set; }
    /// <summary>Maximum parallel sources.</summary>
    public int MaxConcurrency { get; set; } = 8;
    /// <summary>Reads oldest matches first.</summary>
    public bool Oldest { get; set; }
    /// <summary>Source-event materialization mode.</summary>
    public EventReadMode ReadMode { get; set; } = EventReadMode.StructuredDataAndMessage;
    /// <summary>Materializes a bookmark for every source event.</summary>
    public bool IncludeBookmark { get; set; }
    /// <summary>Remote credential.</summary>
    public NetworkCredential? Credential { get; set; }
    /// <summary>Remote authentication package.</summary>
    public EventLogAuthentication Authentication { get; set; }
    /// <summary>Maximum time used to establish each remote session.</summary>
    public int RemoteConnectionTimeoutMilliseconds { get; set; } = 5000;
    /// <summary>Maximum time without remote read progress. Zero is unbounded.</summary>
    public int RemoteReadTimeoutMilliseconds { get; set; }
    /// <summary>Detached snapshots buffered by each remote reader.</summary>
    public int BufferCapacity { get; set; } = 64;
    /// <summary>Culture used for provider messages and labels.</summary>
    public CultureInfo? MessageCulture { get; set; }
    /// <summary>Fallback provider-resource culture.</summary>
    public CultureInfo? FallbackMessageCulture { get; set; }
    /// <summary>Optional serializable predicate evaluated against projected custom fields.</summary>
    public EventPredicate? Predicate { get; set; }
    /// <summary>Optional predicate applied after custom projection.</summary>
    public Func<CustomEventRecord, bool>? ResultPredicate { get; set; }
    /// <summary>Optional per-machine and per-container exclusive record checkpoint.</summary>
    public Func<string?, string, long?>? MinimumRecordIdExclusiveResolver { get; set; }
    /// <summary>
    /// Optional per-machine and per-container bookmark checkpoint. The resolver receives the queried
    /// machine or file path and the actual container channel or file path.
    /// </summary>
    public Func<string?, string, string?>? BookmarkXmlResolver { get; set; }
    /// <summary>
    /// Record offset relative to a resolved bookmark. The default of one resumes after the bookmarked event.
    /// </summary>
    public long BookmarkOffset { get; set; } = 1;
    /// <summary>Fails the source when a resolved bookmark is no longer valid.</summary>
    public bool StrictBookmark { get; set; } = true;
    /// <summary>Observer invoked in source order after custom projection completes.</summary>
    public Action<EventObject>? CandidateObserver { get; set; }
    /// <summary>Continues healthy remote targets after an expected remote-target failure.</summary>
    public bool ContinueOnRemoteFailure { get; set; } = true;
}
