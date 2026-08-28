using System.Globalization;
using System.Net;

namespace EventViewerX;

/// <summary>
/// Defines a reusable event-type query over the native Windows Event Log engine.
/// </summary>
public sealed class EventTypeQuery {
    /// <summary>Creates a query for one or more registered event-type rules.</summary>
    public EventTypeQuery(
        IEnumerable<EventType> types) {

        if (types == null) {
            throw new ArgumentNullException(
                nameof(types));
        }
        Types = types
            .Distinct()
            .ToArray();
        if (Types.Count == 0) {
            throw new ArgumentException(
                "At least one event type is required.",
                nameof(types));
        }
        foreach (EventType type in Types) {
            if (!Enum.IsDefined(
                    typeof(EventType),
                    type)) {
                throw new ArgumentOutOfRangeException(
                    nameof(types),
                    type,
                    $"Event type value '{type}' is not registered.");
            }
        }
    }

    /// <summary>Event-type rules selected by this query.</summary>
    public IReadOnlyList<EventType> Types {
        get;
    }

    /// <summary>
    /// Optional offline event-log files. The selected <see cref="Types"/> still own
    /// source channels, event identifiers, projection, and enrichment; paths only
    /// identify the containers to read.
    /// </summary>
    public IReadOnlyList<string>? Paths { get; set; }

    /// <summary>Optional parser-neutral reader used for every saved-event path.</summary>
    public ISavedEventReader? SavedEventReader { get; set; }

    /// <summary>Optional corruption and fidelity diagnostic sink for saved-event paths.</summary>
    public Action<SavedEventReadDiagnostic>? SavedEventDiagnosticHandler { get; set; }

    /// <summary>Local or remote machines. Null or empty targets the local machine.</summary>
    public IReadOnlyList<string?>? MachineNames {
        get;
        set;
    }

    /// <summary>
    /// Optional collector channel, normally ForwardedEvents. When set, <see cref="MachineNames"/> identifies
    /// collector computers and built-in source channels are matched against each event's original Channel value.
    /// </summary>
    public string? CollectorLogName { get; set; }

    /// <summary>Optional exact source channel.</summary>
    public string? SourceLogName { get; set; }

    /// <summary>Optional source event identifiers.</summary>
    public IReadOnlyCollection<int>? SourceEventIds {
        get;
        set;
    }

    /// <summary>Optional exact event record identifiers, useful for event-triggered task handoff.</summary>
    public IReadOnlyCollection<long>? SourceRecordIds { get; set; }

    /// <summary>Earliest event time.</summary>
    public DateTime? StartTime { get; set; }

    /// <summary>Latest event time.</summary>
    public DateTime? EndTime { get; set; }

    /// <summary>Relative time window.</summary>
    public TimePeriod? TimePeriod { get; set; }

    /// <summary>Maximum matching typed events. Zero streams every match.</summary>
    public long MaxEvents { get; set; }

    /// <summary>Maximum raw candidates evaluated by rules. Zero is unlimited.</summary>
    public long MaxCandidates { get; set; }

    /// <summary>Maximum independent sources opened concurrently.</summary>
    public int MaxConcurrency { get; set; } = 8;

    /// <summary>Whether records are read oldest first.</summary>
    public bool Oldest { get; set; }

    /// <summary>
    /// Amount of source event data materialized before event-type projection.
    /// StructuredDataAndMessage preserves every built-in rule while avoiding binary attachment decoding.
    /// </summary>
    public EventReadMode ReadMode { get; set; } =
        EventReadMode.StructuredDataAndMessage;

    /// <summary>Materializes a native bookmark for every projected source event.</summary>
    public bool IncludeBookmark { get; set; }

    /// <summary>Remote credentials shared by every remote target.</summary>
    public NetworkCredential? Credential { get; set; }

    /// <summary>Remote authentication package.</summary>
    public EventLogAuthentication Authentication {
        get;
        set;
    }

    /// <summary>Maximum time used to establish each remote session.</summary>
    public int RemoteConnectionTimeoutMilliseconds {
        get;
        set;
    } = 5000;

    /// <summary>Maximum time without remote read progress. Zero is unbounded.</summary>
    public int RemoteReadTimeoutMilliseconds {
        get;
        set;
    }

    /// <summary>Detached snapshots buffered by each remote reader.</summary>
    public int BufferCapacity { get; set; } = 64;

    /// <summary>Culture used for provider messages and display names.</summary>
    public CultureInfo? MessageCulture { get; set; }

    /// <summary>Fallback provider-resource culture.</summary>
    public CultureInfo? FallbackMessageCulture {
        get;
        set;
    }

    /// <summary>Optional ordered post-projection enrichment.</summary>
    public EventEnrichmentOptions? Enrichment {
        get;
        set;
    }

    /// <summary>
    /// Optional serializable typed predicate. Native-compatible dimensions are pushed into Windows Event Log;
    /// the exact remainder runs after typed projection.
    /// </summary>
    public EventPredicate? Predicate { get; set; }

    /// <summary>Optional predicate applied after event-type projection.</summary>
    public Func<EventTypeRecord, bool>? ResultPredicate {
        get;
        set;
    }

    /// <summary>
    /// Optional per-machine and per-channel exclusive record checkpoint.
    /// </summary>
    public Func<string?, string, long?>? MinimumRecordIdExclusiveResolver {
        get;
        set;
    }

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

    /// <summary>
    /// Optional observer invoked in source order after projection and enrichment complete.
    /// </summary>
    public Action<EventObject>? CandidateObserver {
        get;
        set;
    }

    /// <summary>
    /// Continues healthy remote targets after an expected remote-target failure.
    /// Local and programming failures always terminate.
    /// </summary>
    public bool ContinueOnRemoteFailure { get; set; } = true;
}
