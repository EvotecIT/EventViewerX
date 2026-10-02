namespace EventViewerX;

/// <summary>Resolved native sources and managed stages for a query, without opening event readers.</summary>
public sealed class EventQueryExplanation {
    internal EventQueryExplanation(EventLogBatchQuery? batch, IReadOnlyList<string> managedStages,
        EventPredicatePlan? predicatePlan = null, long candidateLimit = 0, long? resultLimit = null) {
        Sources = batch == null ? Array.Empty<EventQuerySourceExplanation>() :
            Array.AsReadOnly(batch.ChannelQueries.Select(static query => new EventQuerySourceExplanation(
                EventLogQuerySourceKind.Channel, query.LogName, query.MachineName, query.XPath, query.ReadMode, query.MaxEvents))
            .Concat(batch.FileQueries.Select(static query => new EventQuerySourceExplanation(
                EventLogQuerySourceKind.File, query.Path, null, query.XPath, query.ReadMode, query.MaxEvents)))
            .Concat(batch.StructuredQueries.SelectMany(static query => query.ResolveSources().Select(source =>
                new EventQuerySourceExplanation(source.Kind, source.Source, query.MachineName,
                    query.QueryXml, query.ReadMode, query.MaxEvents))))
            .ToArray());
        ManagedStages = Array.AsReadOnly(managedStages.ToArray());
        MaxEvents = batch?.MaxEvents ?? 0;
        MaxConcurrency = batch?.MaxConcurrency ?? 0;
        ContinueOnError = batch?.ContinueOnError ?? false;
        Oldest = batch?.ChannelQueries.Select(static query => query.Oldest)
            .Concat(batch.FileQueries.Select(static query => query.Oldest))
            .Concat(batch.StructuredQueries.Select(static query => query.Oldest)).FirstOrDefault() ?? false;
        PredicatePlan = predicatePlan;
        CandidateLimit = candidateLimit;
        ResultLimit = resultLimit ?? MaxEvents;
    }

    /// <summary>Resolved channels, file paths, and native XPath or QueryList partitions.</summary>
    public IReadOnlyList<EventQuerySourceExplanation> Sources { get; }
    /// <summary>Post-read filters declared by the consumer.</summary>
    public IReadOnlyList<string> ManagedStages { get; }
    /// <summary>Maximum merged native candidates. Zero is unlimited.</summary>
    public long MaxEvents { get; }
    /// <summary>Maximum concurrently primed sources.</summary>
    public int MaxConcurrency { get; }
    /// <summary>Whether healthy sources continue after isolated failures.</summary>
    public bool ContinueOnError { get; }
    /// <summary>Whether sources are read oldest first.</summary>
    public bool Oldest { get; }
    /// <summary>Exact typed predicate plan, when the query uses a predicate.</summary>
    public EventPredicatePlan? PredicatePlan { get; }
    /// <summary>Managed candidate scan limit. Zero is unlimited.</summary>
    public long CandidateLimit { get; }
    /// <summary>Post-projection or post-filter result limit. Zero is unlimited.</summary>
    public long ResultLimit { get; }
    /// <summary>Predicate dimensions pushed into native selection.</summary>
    public EventFilter? NativeFilter => PredicatePlan?.NativeFilter;
    /// <summary>Exact predicate verified after projection.</summary>
    public EventPredicate? ManagedPredicate => PredicatePlan?.ManagedPredicate;
    /// <summary>Per-predicate planning steps.</summary>
    public IReadOnlyList<EventPredicatePlanStep> Steps => PredicatePlan?.Steps ?? Array.Empty<EventPredicatePlanStep>();
    /// <summary>Whether the predicate has a native prefilter.</summary>
    public bool HasNativeFilter => PredicatePlan?.HasNativeFilter ?? false;
    /// <summary>Whether every predicate selection has a native prefilter.</summary>
    public bool IsFullyNative => PredicatePlan?.IsFullyNative ?? false;
}

/// <summary>One resolved native query partition.</summary>
public sealed class EventQuerySourceExplanation {
    internal EventQuerySourceExplanation(EventLogQuerySourceKind kind, string source, string? machineName,
        string nativeFilter, EventReadMode readMode, long maxEvents) {
        SourceKind = kind;
        Source = source;
        MachineName = machineName;
        NativeFilter = nativeFilter;
        ReadMode = readMode;
        MaxEvents = maxEvents;
    }
    /// <summary>Native channel, file, or structured query source kind.</summary>
    public EventLogQuerySourceKind SourceKind { get; }
    /// <summary>Resolved channel or file path, including paths selected by a structured query.</summary>
    public string Source { get; }
    /// <summary>Remote target, or null for local reads.</summary>
    public string? MachineName { get; }
    /// <summary>Native XPath or complete QueryList XML.</summary>
    public string NativeFilter { get; }
    /// <summary>Detached event projection used by this source.</summary>
    public EventReadMode ReadMode { get; }
    /// <summary>Source candidate limit. Zero is unlimited.</summary>
    public long MaxEvents { get; }
}