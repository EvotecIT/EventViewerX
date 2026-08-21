using System.Globalization;
using System.Runtime.CompilerServices;

namespace EventViewerX;

/// <summary>
/// Projects registered event-type rules over the shared native query and batch engines.
/// </summary>
public static partial class EventTypeEngine {
    /// <summary>Streams typed event projections with bounded memory and ordered checkpoint observation.</summary>
    public static IAsyncEnumerable<EventTypeRecord> ReadAsync(
        EventTypeQuery query,
        EventTypeQueryExecutionInfo? executionInfo = null,
        CancellationToken cancellationToken = default) {

        EventTypeQuery snapshot =
            EventTypeQuerySnapshot.Copy(query);
        Validate(snapshot);
        return ReadSnapshotAsync(
            snapshot,
            executionInfo,
            cancellationToken);
    }

    private static async IAsyncEnumerable<EventTypeRecord>
        ReadSnapshotAsync(
            EventTypeQuery query,
            EventTypeQueryExecutionInfo? executionInfo,
            [EnumeratorCancellation]
            CancellationToken cancellationToken) {

        EventTypeQueryExecutionInfo info =
            executionInfo ??
            new EventTypeQueryExecutionInfo();
        info.Reset(query.MaxCandidates);
        IReadOnlyList<EventType> resolvedTypes =
            EventTypeCatalog.Expand(query.Types);
        EventPredicate? exactPredicate = query.Predicate == null
            ? null
            : EventPredicateBuilder
                .ForTypes(resolvedTypes)
                .Normalize(query.Predicate);
        Dictionary<string, HashSet<int>> eventInfo =
            RestrictSources(
                EventTypeCatalog.GetSourceMap(
                    resolvedTypes),
                query.SourceLogName,
                query.SourceEventIds);
        if (eventInfo.Count == 0) {
            yield break;
        }

        bool managedOnlyPredicate = !string.IsNullOrWhiteSpace(query.CollectorLogName);
        EventPredicatePlan? predicatePlan = exactPredicate == null
            ? null
            : managedOnlyPredicate
                ? EventPredicatePlanner.PlanManagedOnly(
                    exactPredicate,
                    "ForwardedEvents uses the Windows Server 2025 safe '*' reader, so typed filtering is bounded and managed.")
                : EventPredicatePlanner.Plan(exactPredicate);
        info.PredicatePlan = predicatePlan;
        Func<EventTypeRecord, bool>? typedPredicate = predicatePlan?.ManagedPredicate == null
            ? null
            : EventPredicateEvaluator.Compile(predicatePlan.ManagedPredicate);

        using var enricher = query.Enrichment == null
            ? null
            : new EventEnricher(
                query.Enrichment);
        EventLogBatchQuery batch =
            CreateBatch(
                query,
                eventInfo,
                info,
                predicatePlan?.NativeFilter);
        var candidateCounter =
            new EventTypeCandidateCounter(
                query.MaxCandidates,
                info);
        long emitted = 0;

        await foreach (EventTypeProjection projection in
                       ProjectCandidatesInOrderAsync(
                           EventLogEngine.ReadBatchAsync(
                               batch,
                               cancellationToken),
                            resolvedTypes,
                            enricher,
                            candidateCounter
                                .TryRecordCandidate,
                           query.CandidateObserver,
                           cancellationToken)) {
            EventTypeRecord? target = projection.Target;
            if (target == null ||
                (typedPredicate != null &&
                 !typedPredicate(target)) ||
                (query.ResultPredicate != null &&
                 !query.ResultPredicate(target))) {
                continue;
            }

            emitted++;
            info.EventsEmitted = emitted;
            yield return target;
            if (query.MaxEvents > 0 &&
                emitted >= query.MaxEvents) {
                yield break;
            }
        }
    }

    internal static Dictionary<string, HashSet<int>>
        RestrictSources(
            IReadOnlyDictionary<string, HashSet<int>> eventInfo,
            string? sourceLogName,
            IReadOnlyCollection<int>? sourceEventIds) {

        if (eventInfo == null) {
            throw new ArgumentNullException(
                nameof(eventInfo));
        }
        if (sourceEventIds != null &&
            sourceEventIds.Any(static eventId =>
                eventId <= 0)) {
            throw new ArgumentException(
                "Source event IDs must be positive.",
                nameof(sourceEventIds));
        }

        string? normalizedLogName =
            string.IsNullOrWhiteSpace(sourceLogName)
                ? null
                : sourceLogName!.Trim();
        HashSet<int>? allowedEventIds =
            sourceEventIds == null
                ? null
                : new HashSet<int>(
                    sourceEventIds);
        var restricted =
            new Dictionary<string, HashSet<int>>(
                StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, HashSet<int>> source in
                 eventInfo) {
            if (normalizedLogName != null &&
                !string.Equals(
                    source.Key,
                    normalizedLogName,
                    StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            var eventIds = new HashSet<int>(
                source.Value);
            if (allowedEventIds != null) {
                eventIds.IntersectWith(
                    allowedEventIds);
            }
            if (eventIds.Count > 0) {
                restricted[source.Key] =
                    eventIds;
            }
        }
        return restricted;
    }

    private static EventLogBatchQuery CreateBatch(
        EventTypeQuery query,
        IReadOnlyDictionary<string, HashSet<int>> eventInfo,
        EventTypeQueryExecutionInfo executionInfo,
        EventFilter? predicateFilter) {

        (DateTime? startTime, DateTime? endTime) =
            EventTimeRange.Resolve(
                query.StartTime,
                query.EndTime,
                query.TimePeriod);
        if (query.Paths != null && query.Paths.Count > 0) {
            return CreateFileBatch(
                query,
                eventInfo,
                executionInfo,
                startTime,
                endTime,
                predicateFilter);
        }
        if (!string.IsNullOrWhiteSpace(query.CollectorLogName)) {
            return CreateCollectorBatch(
                query,
                eventInfo,
                executionInfo,
                startTime,
                endTime);
        }
        string?[] targets = NormalizeTargets(
            query.MachineNames);
        var channelQueries =
            new List<EventLogChannelQuery>();
        foreach (string? target in targets) {
            foreach (KeyValuePair<string, HashSet<int>> source in
                     eventInfo) {
                long? checkpoint =
                    query.MinimumRecordIdExclusiveResolver?
                        .Invoke(
                            target,
                            string.IsNullOrWhiteSpace(query.CollectorLogName)
                                ? source.Key
                                : query.CollectorLogName!);
                if (checkpoint < 0) {
                    throw new ArgumentOutOfRangeException(
                        nameof(query),
                        "Minimum event record IDs must be greater than or equal to zero.");
                }
                var baseFilter = new EventFilter {
                    EventIds = source.Value
                        .OrderBy(static id => id)
                        .ToArray(),
                    RecordIds = query.SourceRecordIds?.ToArray(),
                    StartTime = startTime,
                    EndTime = endTime,
                    MinimumRecordIdExclusive =
                        checkpoint
                };
                if (!EventFilterIntersection.TryCreate(
                        baseFilter,
                        predicateFilter,
                        out EventFilter filter)) {
                    continue;
                }
                foreach (EventFilter partition in
                         EventFilterPartitioner.Partition(
                             filter)) {
                    string xpath = EventFilterCompiler.BuildXPath(
                        partition);
                    string logName = source.Key;
                    if (!string.IsNullOrWhiteSpace(
                            query.CollectorLogName)) {
                        xpath = AddOriginalChannelPredicate(
                            xpath,
                            source.Key);
                        logName = query.CollectorLogName!;
                    }
                    var channelQuery =
                        new EventLogChannelQuery(
                            logName) {
                            MachineName = target,
                            Credential =
                                EventLogTarget.IsLocalMachine(
                                    target)
                                    ? null
                                    : query.Credential,
                            Authentication =
                                query.Authentication,
                            XPath = xpath,
                            Oldest = query.Oldest,
                            ReadMode =
                                query.ReadMode,
                            IncludeBookmark =
                                query.IncludeBookmark,
                            BookmarkXml = ResolveBookmark(
                                query,
                                target,
                                logName),
                            BookmarkOffset =
                                query.BookmarkOffset,
                            StrictBookmark =
                                query.StrictBookmark,
                            MessageCulture =
                                query.MessageCulture,
                            FallbackMessageCulture =
                                query.FallbackMessageCulture,
                            RemoteConnectionTimeoutMilliseconds =
                                query.RemoteConnectionTimeoutMilliseconds,
                            RemoteReadTimeoutMilliseconds =
                                query.RemoteReadTimeoutMilliseconds,
                            BufferCapacity =
                                query.BufferCapacity
                        };
                    channelQueries.Add(channelQuery);
                }
            }
        }

        EventLogBatchQuery batch =
            EventLogBatchQuery.ForChannels(
                channelQueries);
        batch.MaxConcurrency =
            query.MaxConcurrency;
        batch.ContinueOnError =
            query.ContinueOnRemoteFailure;
        batch.FailureHandler =
            failure => HandleFailure(
                failure,
                executionInfo);
        return EventLogBatchConsolidator.Consolidate(
            batch);
    }

    private static EventLogBatchQuery CreateCollectorBatch(
        EventTypeQuery query,
        IReadOnlyDictionary<string, HashSet<int>> eventInfo,
        EventTypeQueryExecutionInfo executionInfo,
        DateTime? startTime,
        DateTime? endTime) {

        string collectorLogName = query.CollectorLogName!.Trim();
        if (!string.Equals(
                collectorLogName,
                ForwardedEventsQuerySafety.ChannelName,
                StringComparison.OrdinalIgnoreCase)) {
            throw new ArgumentException(
                "CollectorLogName must identify ForwardedEvents.",
                nameof(query));
        }
        var channelQueries = new List<EventLogChannelQuery>();
        foreach (string? target in NormalizeTargets(query.MachineNames)) {
            long? checkpoint = query.MinimumRecordIdExclusiveResolver?
                .Invoke(target, collectorLogName);
            var filter = new EventFilter {
                RecordIds = query.SourceRecordIds?.ToArray(),
                MinimumRecordIdExclusive = checkpoint,
                StartTime = startTime,
                EndTime = endTime
            };
            Func<EventObject, bool>? basePredicate =
                ManagedEventFilter.CreatePredicate(filter);
            var channelQuery = new EventLogChannelQuery(collectorLogName) {
                MachineName = target,
                Credential = EventLogTarget.IsLocalMachine(target)
                    ? null
                    : query.Credential,
                Authentication = query.Authentication,
                XPath = "*",
                Oldest = query.Oldest,
                ReadMode = query.ReadMode,
                IncludeBookmark = query.IncludeBookmark,
                MessageCulture = query.MessageCulture,
                FallbackMessageCulture = query.FallbackMessageCulture,
                RemoteConnectionTimeoutMilliseconds =
                    query.RemoteConnectionTimeoutMilliseconds,
                RemoteReadTimeoutMilliseconds =
                    query.RemoteReadTimeoutMilliseconds,
                BufferCapacity = query.BufferCapacity,
                ManagedMaxEventsScanned = query.MaxCandidates,
                ManagedScanLimitReached = () =>
                    executionInfo.ScanLimitReached = true,
                ManagedPredicate = eventObject =>
                    (basePredicate == null || basePredicate(eventObject)) &&
                    eventInfo.TryGetValue(
                        eventObject.OriginalLogName,
                        out HashSet<int>? eventIds) &&
                    eventIds.Contains(eventObject.Id)
            };
            ForwardedEventsQuerySafety.Apply(
                channelQuery,
                startTime,
                endTime);
            channelQueries.Add(channelQuery);
        }
        EventLogBatchQuery batch = EventLogBatchQuery.ForChannels(
            channelQueries);
        batch.MaxConcurrency = query.MaxConcurrency;
        batch.ContinueOnError = query.ContinueOnRemoteFailure;
        batch.FailureHandler = failure => HandleFailure(
            failure,
            executionInfo);
        return batch;
    }

    private static EventLogBatchQuery CreateFileBatch(
        EventTypeQuery query,
        IReadOnlyDictionary<string, HashSet<int>> eventInfo,
        EventTypeQueryExecutionInfo executionInfo,
        DateTime? startTime,
        DateTime? endTime,
        EventFilter? predicateFilter) {

        var fileQueries = new List<EventLogFileQuery>();
        foreach (string path in query.Paths!) {
            string fullPath = Path.GetFullPath(path);
            foreach (KeyValuePair<string, HashSet<int>> source in eventInfo) {
                long? checkpoint =
                    query.MinimumRecordIdExclusiveResolver?
                        .Invoke(fullPath, fullPath);
                if (checkpoint < 0) {
                    throw new ArgumentOutOfRangeException(
                        nameof(query),
                        "Minimum event record IDs must be greater than or equal to zero.");
                }
                var baseFilter = new EventFilter {
                    EventIds = source.Value
                        .OrderBy(static id => id)
                        .ToArray(),
                    RecordIds = query.SourceRecordIds?.ToArray(),
                    StartTime = startTime,
                    EndTime = endTime,
                    MinimumRecordIdExclusive = checkpoint
                };
                if (!EventFilterIntersection.TryCreate(
                        baseFilter,
                        predicateFilter,
                        out EventFilter filter)) {
                    continue;
                }
                foreach (EventFilter partition in
                         EventFilterPartitioner.Partition(filter)) {
                    string xpath = AddOriginalChannelPredicate(
                        EventFilterCompiler.BuildXPath(partition),
                        source.Key);
                    fileQueries.Add(new EventLogFileQuery(fullPath) {
                        XPath = xpath,
                        Oldest = query.Oldest,
                        ReadMode = query.ReadMode,
                        IncludeBookmark = query.IncludeBookmark,
                        BookmarkXml = ResolveBookmark(
                            query,
                            fullPath,
                            fullPath),
                        BookmarkOffset = query.BookmarkOffset,
                        StrictBookmark = query.StrictBookmark,
                        MessageCulture = query.MessageCulture,
                        FallbackMessageCulture = query.FallbackMessageCulture
                    });
                }
            }
        }
        EventLogBatchQuery batch = EventLogBatchQuery.ForFiles(fileQueries);
        batch.MaxConcurrency = query.MaxConcurrency;
        batch.ContinueOnError = false;
        batch.FailureHandler = failure => HandleFailure(
            failure,
            executionInfo);
        return EventLogBatchConsolidator.Consolidate(batch);
    }

    internal static string AddOriginalChannelPredicate(
        string xpath,
        string originalChannel) {

        if (string.IsNullOrWhiteSpace(originalChannel)) {
            throw new ArgumentException(
                "Original channel cannot be empty.",
                nameof(originalChannel));
        }
        string channelLiteral =
            WindowsEventFilterBuilder.FormatXPathStringLiteral(
                originalChannel.Trim(),
                nameof(originalChannel));
        if (xpath == "*") {
            return $"*[System[Channel={channelLiteral}]]";
        }
        if (string.IsNullOrWhiteSpace(xpath) || xpath.IndexOf("<QueryList", StringComparison.OrdinalIgnoreCase) >= 0) {
            throw new ArgumentException("The typed filter must be a native XPath expression.", nameof(xpath));
        }
        return $"(*[System[Channel={channelLiteral}]]) and ({xpath})";
    }

    internal static void HandleFailure(
        EventLogQueryFailure failure,
        EventTypeQueryExecutionInfo executionInfo) {

        if (EventLogRemoteQueryFailureClassifier.TryClassify(
                failure.MachineName,
                failure.Exception,
                out EventLogRemoteQueryFailureKind kind)) {
            executionInfo.RecordTargetFailure(
                new EventLogQueryTargetFailure(
                    failure.MachineName!,
                    failure.Source,
                    kind,
                    failure.Exception.Message));
            return;
        }
        throw failure.Exception;
    }

    private static string?[] NormalizeTargets(
        IReadOnlyList<string?>? machineNames) {

        IEnumerable<string?> candidates =
            machineNames == null ||
            machineNames.Count == 0
                ? new string?[] { null }
                : machineNames;
        return candidates
            .Select(static machine =>
                EventLogTarget.IsLocalMachine(machine)
                    ? null
                    : machine?.Trim())
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void Validate(
        EventTypeQuery query) {

        if (query == null) {
            throw new ArgumentNullException(
                nameof(query));
        }
        EventReadModeValidation.EnsureDefined(
            query.ReadMode,
            nameof(query));
        if (!Enum.IsDefined(
                typeof(EventLogAuthentication),
                query.Authentication)) {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "The remote authentication value is not supported.");
        }
        if (query.RemoteConnectionTimeoutMilliseconds <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "Remote connection timeout must be greater than zero.");
        }
        if (query.RemoteReadTimeoutMilliseconds < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "Remote read timeout must be greater than or equal to zero.");
        }
        if (query.MaxEvents < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "Maximum events must be greater than or equal to zero.");
        }
        if (query.MaxCandidates < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "Maximum candidates must be greater than or equal to zero.");
        }
        if (query.MaxConcurrency <= 0 ||
            query.MaxConcurrency >
            EventLogLimits.MaximumConcurrency) {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                $"Maximum concurrency must be between 1 and {EventLogLimits.MaximumConcurrency}.");
        }
        if (query.BufferCapacity <= 0 ||
            query.BufferCapacity > 4096) {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "Buffer capacity must be between 1 and 4096.");
        }
        if (query.BookmarkOffset == 0) {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "Bookmark offset cannot be zero.");
        }
        if (query.Credential != null &&
            NormalizeTargets(query.MachineNames)
                .Any(EventLogTarget.IsLocalMachine)) {
            throw new ArgumentException(
                "Credential can only be used when every event-type target is a remote computer.",
                nameof(query));
        }
        bool hasPaths = query.Paths != null && query.Paths.Count > 0;
        if (hasPaths && query.Paths!.Any(static path =>
                string.IsNullOrWhiteSpace(path))) {
            throw new ArgumentException(
                "Offline paths cannot contain empty values.",
                nameof(query));
        }
        if (hasPaths &&
            (query.MachineNames != null && query.MachineNames.Count > 0 ||
             !string.IsNullOrWhiteSpace(query.CollectorLogName) ||
             query.Credential != null)) {
            throw new ArgumentException(
                "Offline paths cannot be combined with remote targets, collectors, or credentials.",
                nameof(query));
        }
        query.Enrichment?.Validate();
    }

    private static string? ResolveBookmark(
        EventTypeQuery query,
        string? machineName,
        string container) {

        string? bookmark = query.BookmarkXmlResolver?
            .Invoke(machineName, container);
        return string.IsNullOrWhiteSpace(bookmark)
            ? null
            : bookmark;
    }
}
