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
        EventTypeProjectionPlan projectionPlan =
            EventTypeCatalog.CompileProjectionPlan(query.Types);
        IReadOnlyList<EventType> resolvedTypes =
            projectionPlan.ExpandedTypes;
        EventPredicate? exactPredicate = query.Predicate == null
            ? null
            : EventPredicateBuilder
                .ForTypes(resolvedTypes)
                .Normalize(query.Predicate);
        IReadOnlyList<EventSourceDefinition> eventSources =
            RestrictSources(
                EventTypeCatalog.GetSources(
                    resolvedTypes),
                query.SourceLogName,
                query.SourceEventIds);
        if (eventSources.Count == 0) {
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
        EventLogBatchQuery? batch =
            CreateBatch(
                query,
                eventSources,
                info,
                predicatePlan?.NativeFilter);
        if (batch == null) {
            yield break;
        }
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
                            projectionPlan,
                            enricher,
                            candidateCounter
                                .TryRecordCandidate,
                           cancellationToken)) {
            EventTypeProjectionDisposition disposition = ClassifyProjectionForEmission(
                projection,
                typedPredicate,
                query.ResultPredicate,
                query.MaxEvents,
                ref emitted,
                info,
                query.CandidateObserver);
            if (disposition == EventTypeProjectionDisposition.Stop) {
                yield break;
            }
            if (disposition == EventTypeProjectionDisposition.Emit) {
                yield return projection.Target!;
            }
        }
    }

    internal static EventTypeProjectionDisposition ClassifyProjectionForEmission(
        EventTypeProjection projection,
        Func<EventTypeRecord, bool>? typedPredicate,
        Func<EventTypeRecord, bool>? resultPredicate,
        long maxEvents,
        ref long emitted,
        EventTypeQueryExecutionInfo executionInfo,
        Action<EventObject>? candidateObserver) {

        EventTypeRecord? target = projection.Target;
        if (target == null ||
            typedPredicate != null && !typedPredicate(target) ||
            resultPredicate != null && !resultPredicate(target)) {
            candidateObserver?.Invoke(projection.Source);
            return EventTypeProjectionDisposition.Skip;
        }
        if (maxEvents > 0 && emitted >= maxEvents) {
            executionInfo.ResultLimitReached = true;
            return EventTypeProjectionDisposition.Stop;
        }
        candidateObserver?.Invoke(projection.Source);
        emitted++;
        executionInfo.EventsEmitted = emitted;
        return EventTypeProjectionDisposition.Emit;
    }

    internal enum EventTypeProjectionDisposition {
        Skip,
        Emit,
        Stop
    }

    internal static IReadOnlyList<EventSourceDefinition>
        RestrictSources(
            IReadOnlyList<EventSourceDefinition> sources,
            string? sourceLogName,
            IReadOnlyCollection<int>? sourceEventIds) {

        if (sources == null) {
            throw new ArgumentNullException(
                nameof(sources));
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
        var restricted = new List<EventSourceDefinition>();
        foreach (EventSourceDefinition source in sources) {
            if (normalizedLogName != null &&
                !string.Equals(
                    source.LogName,
                    normalizedLogName,
                    StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            var eventIds = new HashSet<int>(
                source.EventIds);
            if (allowedEventIds != null) {
                eventIds.IntersectWith(
                    allowedEventIds);
            }
            if (eventIds.Count > 0) {
                restricted.Add(new EventSourceDefinition(
                    source.LogName,
                    eventIds,
                    source.ProviderNames));
            }
        }
        return restricted;
    }

    internal static EventLogBatchQuery? CreateBatch(
        EventTypeQuery query,
        IReadOnlyList<EventSourceDefinition> eventSources,
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
                eventSources,
                executionInfo,
                startTime,
                endTime,
                predicateFilter);
        }
        if (!string.IsNullOrWhiteSpace(query.CollectorLogName)) {
            return CreateCollectorBatch(
                query,
                eventSources,
                executionInfo,
                startTime,
                endTime);
        }
        string?[] targets = NormalizeTargets(
            query.MachineNames);
        var channelQueries =
            new List<EventLogChannelQuery>();
        foreach (string? target in targets) {
            foreach (EventSourceDefinition source in eventSources) {
                long? checkpoint =
                    query.MinimumRecordIdExclusiveResolver?
                        .Invoke(
                            target,
                            string.IsNullOrWhiteSpace(query.CollectorLogName)
                                ? source.LogName
                                : query.CollectorLogName!);
                if (checkpoint < 0) {
                    throw new ArgumentOutOfRangeException(
                        nameof(query),
                        "Minimum event record IDs must be greater than or equal to zero.");
                }
                var baseFilter = new EventFilter {
                    EventIds = source.EventIds
                        .OrderBy(static id => id)
                        .ToArray(),
                    ProviderNames = source.ProviderNames.ToArray(),
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
                    string logName = source.LogName;
                    if (!string.IsNullOrWhiteSpace(
                            query.CollectorLogName)) {
                        xpath = EventFilterCompiler.AddOriginalChannelPredicate(
                            xpath,
                            source.LogName);
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

        if (channelQueries.Count == 0) {
            return null;
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

    internal static EventLogBatchQuery CreateCollectorBatch(
        EventTypeQuery query,
        IReadOnlyList<EventSourceDefinition> eventSources,
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
        var sourcesByLog = eventSources
            .GroupBy(
                static source => source.LogName,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static group => group.Key,
                static group => group.ToArray(),
                StringComparer.OrdinalIgnoreCase);
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
                BookmarkXml = ResolveBookmark(
                    query,
                    target,
                    collectorLogName),
                BookmarkOffset = query.BookmarkOffset,
                StrictBookmark = query.StrictBookmark,
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
                    sourcesByLog.TryGetValue(
                        eventObject.OriginalLogName,
                        out EventSourceDefinition[]? matchingSources) &&
                    matchingSources.Any(source =>
                        source.EventIds.Contains(eventObject.Id) &&
                        (source.ProviderNames.Count == 0 ||
                         source.ProviderNames.Contains(
                             eventObject.ProviderName,
                             StringComparer.OrdinalIgnoreCase)))
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

    private static EventLogBatchQuery? CreateFileBatch(
        EventTypeQuery query,
        IReadOnlyList<EventSourceDefinition> eventSources,
        EventTypeQueryExecutionInfo executionInfo,
        DateTime? startTime,
        DateTime? endTime,
        EventFilter? predicateFilter) {

        var fileQueries = new List<EventLogFileQuery>();
        foreach (string path in query.Paths!) {
            string fullPath = Path.GetFullPath(path);
            foreach (EventSourceDefinition source in eventSources) {
                long? checkpoint =
                    query.MinimumRecordIdExclusiveResolver?
                        .Invoke(fullPath, fullPath);
                if (checkpoint < 0) {
                    throw new ArgumentOutOfRangeException(
                        nameof(query),
                        "Minimum event record IDs must be greater than or equal to zero.");
                }
                var baseFilter = new EventFilter {
                    EventIds = source.EventIds
                        .OrderBy(static id => id)
                        .ToArray(),
                    ProviderNames = source.ProviderNames.ToArray(),
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
                    string xpath = EventFilterCompiler.AddOriginalChannelPredicate(
                        EventFilterCompiler.BuildXPath(partition),
                        source.LogName);
                    fileQueries.Add(new EventLogFileQuery(fullPath) {
                        XPath = xpath,
                        SavedEventReader = query.SavedEventReader,
                        SavedEventDiagnosticHandler = query.SavedEventDiagnosticHandler,
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
        if (fileQueries.Count == 0) {
            return null;
        }
        EventLogBatchQuery batch = EventLogBatchQuery.ForFiles(fileQueries);
        batch.MaxConcurrency = query.MaxConcurrency;
        batch.ContinueOnError = false;
        batch.FailureHandler = failure => HandleFailure(
            failure,
            executionInfo);
        return EventLogBatchConsolidator.Consolidate(batch);
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
