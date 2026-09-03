using System.Reflection;
using System.Runtime.CompilerServices;
using System.Globalization;
using System.Net;

namespace EventViewerX;

/// <summary>Executes validated declarative definitions through the shared native batch engine.</summary>
public static class EventDefinitionEngine {
    private static readonly Dictionary<string, PropertyInfo> MetadataProperties = typeof(EventObject)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(static property => property.CanRead && property.GetIndexParameters().Length == 0)
        .ToDictionary(static property => property.Name, StringComparer.OrdinalIgnoreCase);

    /// <summary>Streams custom projections.</summary>
    public static IAsyncEnumerable<CustomEventRecord> ReadAsync(EventDefinitionQuery query,
        CancellationToken cancellationToken = default) {
        return ReadSnapshotAsync(CreateSnapshot(query), null, cancellationToken);
    }

    /// <summary>Streams custom projections and reports progress and isolated remote failures.</summary>
    public static IAsyncEnumerable<CustomEventRecord> ReadAsync(EventDefinitionQuery query,
        EventDefinitionQueryExecutionInfo executionInfo,
        CancellationToken cancellationToken = default) {
        if (executionInfo == null) {
            throw new ArgumentNullException(nameof(executionInfo));
        }
        return ReadSnapshotAsync(CreateSnapshot(query), executionInfo, cancellationToken);
    }

    /// <summary>Explains native prefiltering and exact post-projection evaluation for a declarative definition.</summary>
    public static EventPredicatePlan PlanPredicate(
        EventDefinition definition,
        EventPredicate predicate,
        string? collectorLogName = null) {

        if (definition == null) {
            throw new ArgumentNullException(nameof(definition));
        }
        if (predicate == null) {
            throw new ArgumentNullException(nameof(predicate));
        }
        definition.Validate();
        predicate.Validate();
        return CreatePredicatePlan(new EventDefinitionQuery(definition) {
            Predicate = predicate,
            CollectorLogName = collectorLogName
        })!;
    }

    private static async IAsyncEnumerable<CustomEventRecord> ReadSnapshotAsync(EventDefinitionQuery query,
        EventDefinitionQueryExecutionInfo? executionInfo,
        [EnumeratorCancellation] CancellationToken cancellationToken) {
        EventDefinitionQueryExecutionInfo info = executionInfo ?? new EventDefinitionQueryExecutionInfo();
        info.Reset();
        EventPredicatePlan? predicatePlan = CreatePredicatePlan(query);
        info.PredicatePlan = predicatePlan;
        Func<CustomEventRecord, bool>? typedPredicate = predicatePlan?.ManagedPredicate == null
            ? null
            : EventPredicateEvaluator.CompileCustom(predicatePlan.ManagedPredicate);
        (DateTime? start, DateTime? end) = EventTimeRange.Resolve(query.StartTime, query.EndTime, query.TimePeriod);
        EventLogBatchQuery? batch = query.Paths != null && query.Paths.Count > 0
            ? CreateFileBatch(query, predicatePlan?.NativeFilter, start, end)
            : CreateChannelBatch(query, info, predicatePlan?.NativeFilter, start, end);
        if (batch == null) {
            yield break;
        }
        batch.MaxEvents = 0;
        batch.MaxConcurrency = query.MaxConcurrency;
        batch.ContinueOnError = query.ContinueOnRemoteFailure;
        batch.FailureHandler = failure => HandleFailure(failure, info);
        long emitted = 0;
        await foreach (EventObject source in EventLogEngine.ReadBatchAsync(batch, cancellationToken)) {
            if (query.MaxCandidates > 0 && info.EventsScanned >= query.MaxCandidates) {
                info.ScanLimitReached = true;
                yield break;
            }
            info.EventsScanned++;
            var record = new CustomEventRecord(query.Definition, source, Project(query.Definition, source));
            if ((typedPredicate != null && !typedPredicate(record)) ||
                (query.ResultPredicate != null && !query.ResultPredicate(record))) {
                query.CandidateObserver?.Invoke(source);
                continue;
            }
            if (query.MaxEvents > 0 && emitted >= query.MaxEvents) {
                info.ResultLimitReached = true;
                yield break;
            }
            query.CandidateObserver?.Invoke(source);
            emitted++;
            info.EventsEmitted = emitted;
            yield return record;
        }
    }

    private static EventLogBatchQuery? CreateChannelBatch(
        EventDefinitionQuery query,
        EventDefinitionQueryExecutionInfo executionInfo,
        EventFilter? predicateFilter,
        DateTime? start,
        DateTime? end) {

        string?[] targets = NormalizeTargets(query.MachineNames);
        if (!string.IsNullOrWhiteSpace(query.CollectorLogName)) {
            return CreateCollectorBatch(
                query,
                executionInfo,
                targets,
                start,
                end);
        }
        var sources = new List<EventLogChannelQuery>();
        foreach (string? target in targets) {
            foreach (EventDefinitionSource source in query.Definition.Sources) {
                foreach ((string xpath, string logName) in CreateSourceFilters(
                             query,
                             source,
                             target,
                             sourceIsFile: false,
                             predicateFilter,
                             start,
                             end,
                             useOriginalChannel: !string.IsNullOrWhiteSpace(query.CollectorLogName))) {
                    var channelQuery = new EventLogChannelQuery(logName) {
                        MachineName = target,
                        Credential = EventLogTarget.IsLocalMachine(target) ? null : query.Credential,
                        Authentication = query.Authentication,
                        XPath = xpath,
                        Oldest = query.Oldest,
                        ReadMode = query.ReadMode,
                        IncludeBookmark = query.IncludeBookmark,
                        BookmarkXml = ResolveBookmark(query, target, logName),
                        BookmarkOffset = query.BookmarkOffset,
                        StrictBookmark = query.StrictBookmark,
                        MessageCulture = query.MessageCulture,
                        FallbackMessageCulture = query.FallbackMessageCulture,
                        RemoteConnectionTimeoutMilliseconds = query.RemoteConnectionTimeoutMilliseconds,
                        RemoteReadTimeoutMilliseconds = query.RemoteReadTimeoutMilliseconds,
                        BufferCapacity = query.BufferCapacity
                    };
                    sources.Add(channelQuery);
                }
            }
        }
        return sources.Count == 0
            ? null
            : EventLogBatchConsolidator.Consolidate(EventLogBatchQuery.ForChannels(sources));
    }

    internal static EventLogBatchQuery CreateCollectorBatch(
        EventDefinitionQuery query,
        EventDefinitionQueryExecutionInfo executionInfo,
        IReadOnlyList<string?> targets,
        DateTime? start,
        DateTime? end) {

        string collectorLogName = query.CollectorLogName!.Trim();
        if (!string.Equals(
                collectorLogName,
                ForwardedEventsQuerySafety.ChannelName,
                StringComparison.OrdinalIgnoreCase)) {
            throw new ArgumentException(
                "CollectorLogName must identify ForwardedEvents.",
                nameof(query));
        }
        var definitionSources = query.Definition.Sources
            .GroupBy(
                static source => source.LogName,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static group => group.Key,
                static group => group.ToArray(),
                StringComparer.OrdinalIgnoreCase);
        var sources = new List<EventLogChannelQuery>(targets.Count);
        foreach (string? target in targets) {
            long? checkpoint = query.MinimumRecordIdExclusiveResolver?
                .Invoke(target, collectorLogName);
            var filter = new EventFilter {
                RecordIds = query.RecordIds?.ToArray(),
                MinimumRecordIdExclusive = checkpoint,
                StartTime = start,
                EndTime = end
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
                    definitionSources.TryGetValue(
                        eventObject.OriginalLogName,
                        out EventDefinitionSource[]? matchingSources) &&
                    matchingSources.Any(source =>
                        source.EventIds.Contains(eventObject.Id) &&
                        (source.ProviderNames.Count == 0 ||
                         source.ProviderNames.Contains(
                             eventObject.ProviderName,
                             StringComparer.Ordinal)))
            };
            ForwardedEventsQuerySafety.Apply(
                channelQuery,
                start,
                end);
            sources.Add(channelQuery);
        }
        return EventLogBatchQuery.ForChannels(sources);
    }

    private static EventLogBatchQuery? CreateFileBatch(
        EventDefinitionQuery query,
        EventFilter? predicateFilter,
        DateTime? start,
        DateTime? end) {

        var files = new List<EventLogFileQuery>();
        foreach (string path in query.Paths!) {
            string fullPath = Path.GetFullPath(path);
            foreach (EventDefinitionSource source in query.Definition.Sources) {
                foreach ((string xpath, _) in CreateSourceFilters(
                             query,
                             source,
                             fullPath,
                             sourceIsFile: true,
                             predicateFilter,
                             start,
                             end,
                             useOriginalChannel: true)) {
                    files.Add(new EventLogFileQuery(fullPath) {
                        XPath = xpath,
                        SavedEventReader = query.SavedEventReader,
                        SavedEventDiagnosticHandler = query.SavedEventDiagnosticHandler,
                        Oldest = query.Oldest,
                        ReadMode = query.ReadMode,
                        IncludeBookmark = query.IncludeBookmark,
                        BookmarkXml = ResolveBookmark(query, fullPath, fullPath),
                        BookmarkOffset = query.BookmarkOffset,
                        StrictBookmark = query.StrictBookmark,
                        MessageCulture = query.MessageCulture,
                        FallbackMessageCulture = query.FallbackMessageCulture
                    });
                }
            }
        }
        return files.Count == 0
            ? null
            : EventLogBatchConsolidator.Consolidate(EventLogBatchQuery.ForFiles(files));
    }

    private static IEnumerable<(string XPath, string LogName)> CreateSourceFilters(
        EventDefinitionQuery query,
        EventDefinitionSource source,
        string? machineName,
        bool sourceIsFile,
        EventFilter? predicateFilter,
        DateTime? start,
        DateTime? end,
        bool useOriginalChannel) {

        var baseFilter = new EventFilter {
            EventIds = source.EventIds.ToArray(),
            ProviderNames = source.ProviderNames.ToArray(),
            RecordIds = query.RecordIds?.ToArray(),
            StartTime = start,
            EndTime = end,
            MinimumRecordIdExclusive = query.MinimumRecordIdExclusiveResolver?.Invoke(
                machineName,
                sourceIsFile
                    ? machineName!
                    : string.IsNullOrWhiteSpace(query.CollectorLogName) ? source.LogName : query.CollectorLogName!)
        };
        if (!EventFilterIntersection.TryCreate(baseFilter, predicateFilter, out EventFilter filter)) {
            yield break;
        }
        foreach (EventFilter partition in EventFilterPartitioner.Partition(filter)) {
            string xpath = EventFilterCompiler.BuildXPath(partition);
            string logName = source.LogName;
            if (useOriginalChannel) {
                xpath = EventFilterCompiler.AddOriginalChannelPredicate(xpath, source.LogName);
                if (!string.IsNullOrWhiteSpace(query.CollectorLogName)) {
                    logName = query.CollectorLogName!;
                }
            }
            yield return (xpath, logName);
        }
    }

    private static EventPredicatePlan? CreatePredicatePlan(EventDefinitionQuery query) {
        if (query.Predicate == null) {
            return null;
        }
        EventPredicate exactPredicate = EventPredicateBuilder
            .ForDefinition(query.Definition)
            .Normalize(query.Predicate);
        const string projectionReason =
            "Declarative fields are evaluated after their configured projection and conversion.";
        if (!string.IsNullOrWhiteSpace(query.CollectorLogName)) {
            return EventPredicatePlanner.PlanManaged(
                exactPredicate,
                "ForwardedEvents uses the Windows Server 2025 safe '*' reader; declarative predicates remain managed and bounded.");
        }
        EventPredicate? nativeCandidate = ExtractNativeCandidate(query.Definition, exactPredicate);
        if (nativeCandidate == null) {
            return EventPredicatePlanner.PlanManaged(exactPredicate, projectionReason);
        }
        EventPredicatePlan nativePlan = EventPredicatePlanner.Plan(nativeCandidate);
        var steps = new List<EventPredicatePlanStep>(nativePlan.Steps.Where(static step =>
            !string.Equals(step.Expression, "Exact predicate verification", StringComparison.Ordinal))) {
            new EventPredicatePlanStep(
                "Full declarative predicate",
                EventPredicatePlanStage.Managed,
                "The complete predicate is verified after projection so custom conversion and fallback semantics remain exact.")
        };
        return new EventPredicatePlan(
            nativePlan.NativeFilter,
            exactPredicate,
            steps);
    }

    private static EventPredicate? ExtractNativeCandidate(
        EventDefinition definition,
        EventPredicate predicate) {

        if (predicate.Kind == EventPredicateKind.All) {
            EventPredicate[] children = predicate.Children
                .Select(child => ExtractNativeCandidate(definition, child))
                .Where(static child => child != null)
                .Cast<EventPredicate>()
                .ToArray();
            return children.Length switch {
                0 => null,
                1 => children[0],
                _ => EventPredicate.AllOf(children)
            };
        }
        if (predicate.Kind != EventPredicateKind.Comparison ||
            !TryResolveNativeField(definition, predicate.Field!, out string? nativeField)) {
            return null;
        }
        EventPredicate mapped = predicate.Clone();
        mapped.Field = nativeField;
        EventPredicatePlan plan = EventPredicatePlanner.Plan(mapped);
        return plan.IsFullyNative && plan.HasNativeFilter ? mapped : null;
    }

    private static bool TryResolveNativeField(
        EventDefinition definition,
        string predicateField,
        out string? nativeField) {

        nativeField = null;
        EventDefinitionField? field = definition.Fields.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, predicateField, StringComparison.OrdinalIgnoreCase) ||
            candidate.Aliases.Contains(predicateField, StringComparer.OrdinalIgnoreCase));
        if (field != null) {
            if (!CanUseNativeMetadataField(field)) {
                return false;
            }
            nativeField = field.SourceName;
            return true;
        }
        if (!EventFieldDefinition.IsNativeField(predicateField)) {
            return false;
        }
        nativeField = predicateField;
        return true;
    }

    internal static bool CanUseNativeMetadataField(EventDefinitionField field) {
        if (field.Source != EventFieldSource.Metadata || field.DefaultValue != null ||
            !EventFieldDefinition.IsNativeField(field.SourceName) ||
            !TryResolveMetadataProperty(field.SourceName, out PropertyInfo? property)) {
            return false;
        }
        Type sourceType = Nullable.GetUnderlyingType(property!.PropertyType) ?? property.PropertyType;
        Type projectedType = Nullable.GetUnderlyingType(field.ValueType) ?? field.ValueType;
        if (sourceType == projectedType) {
            return true;
        }
        return string.Equals(field.SourceName, nameof(EventObject.Level), StringComparison.OrdinalIgnoreCase) &&
               sourceType == typeof(byte) &&
               projectedType == typeof(int);
    }

    private static bool TryResolveMetadataProperty(string name, out PropertyInfo? property) {
        string canonicalName = name switch {
            var value when value.Equals("EventId", StringComparison.OrdinalIgnoreCase) => nameof(EventObject.Id),
            var value when value.Equals("EventRecordId", StringComparison.OrdinalIgnoreCase) => nameof(EventObject.RecordId),
            var value when value.Equals("Provider", StringComparison.OrdinalIgnoreCase) => nameof(EventObject.ProviderName),
            _ => name
        };
        return MetadataProperties.TryGetValue(canonicalName, out property);
    }

    /// <summary>Projects one previously read event through a custom definition.</summary>
    public static CustomEventRecord CreateRecord(EventDefinition definition, EventObject source) {
        if (definition == null) {
            throw new ArgumentNullException(nameof(definition));
        }
        if (source == null) {
            throw new ArgumentNullException(nameof(source));
        }
        definition.Validate();
        return new CustomEventRecord(definition, source, Project(definition, source));
    }

    internal static IReadOnlyDictionary<string, object?> Project(EventDefinition definition, EventObject source) {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (EventDefinitionField field in definition.Fields) {
            object? value = field.Source switch {
                EventFieldSource.Data => source.Data.TryGetValue(field.SourceName, out string? data) ? data : field.DefaultValue,
                EventFieldSource.MessageData => source.MessageData.TryGetValue(field.SourceName, out string? messageData) ? messageData : field.DefaultValue,
                EventFieldSource.Metadata => TryResolveMetadataProperty(field.SourceName, out PropertyInfo? property) ? property!.GetValue(source) : field.DefaultValue,
                EventFieldSource.Message => source.Message,
                EventFieldSource.Constant => field.SourceName,
                _ => field.DefaultValue
            };
            result[field.Name] = ConvertFieldValue(field, value);
        }
        return result;
    }

    internal static EventDefinitionQuery CreateSnapshot(EventDefinitionQuery query) {
        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        query.Definition.Validate();
        if (query.MaxEvents < 0) {
            throw new ArgumentOutOfRangeException(nameof(query), "Maximum events must be non-negative.");
        }
        if (query.MaxCandidates < 0) {
            throw new ArgumentOutOfRangeException(nameof(query), "Maximum candidates must be non-negative.");
        }
        if (query.MaxConcurrency < 1 || query.MaxConcurrency > EventLogLimits.MaximumConcurrency) {
            throw new ArgumentOutOfRangeException(nameof(query), $"Maximum concurrency must be between 1 and {EventLogLimits.MaximumConcurrency}.");
        }
        if (query.RemoteConnectionTimeoutMilliseconds <= 0) {
            throw new ArgumentOutOfRangeException(nameof(query), "Remote connection timeout must be positive.");
        }
        if (query.RemoteReadTimeoutMilliseconds < 0) {
            throw new ArgumentOutOfRangeException(nameof(query), "Remote read timeout must be non-negative.");
        }
        if (query.BufferCapacity is < 1 or > 4096) {
            throw new ArgumentOutOfRangeException(nameof(query), "Buffer capacity must be between 1 and 4096.");
        }
        if (query.BookmarkOffset == 0) {
            throw new ArgumentOutOfRangeException(nameof(query), "Bookmark offset cannot be zero.");
        }
        if (!Enum.IsDefined(typeof(EventLogAuthentication), query.Authentication)) {
            throw new ArgumentOutOfRangeException(nameof(query), "The remote authentication value is not supported.");
        }
        string?[] targets = NormalizeTargets(query.MachineNames);
        bool hasPaths = query.Paths != null && query.Paths.Count > 0;
        if (hasPaths && query.Paths!.Any(static path => string.IsNullOrWhiteSpace(path))) {
            throw new ArgumentException("Offline paths cannot contain empty values.", nameof(query));
        }
        if (hasPaths &&
            (query.MachineNames != null && query.MachineNames.Count > 0 ||
             !string.IsNullOrWhiteSpace(query.CollectorLogName) ||
             query.Credential != null)) {
            throw new ArgumentException(
                "Offline paths cannot be combined with remote targets, collectors, or credentials.",
                nameof(query));
        }
        if (query.Credential != null && targets.Any(EventLogTarget.IsLocalMachine)) {
            throw new ArgumentException("Credential can only be used when every definition target is remote.", nameof(query));
        }
        EventDefinition definition = CopyDefinition(query.Definition);
        return new EventDefinitionQuery(definition) {
            Paths = query.Paths?.ToArray(),
            SavedEventReader = query.SavedEventReader,
            SavedEventDiagnosticHandler = query.SavedEventDiagnosticHandler,
            MachineNames = targets,
            CollectorLogName = string.IsNullOrWhiteSpace(query.CollectorLogName) ? null : query.CollectorLogName!.Trim(),
            StartTime = query.StartTime,
            EndTime = query.EndTime,
            TimePeriod = query.TimePeriod,
            RecordIds = query.RecordIds?.ToArray(),
            MaxEvents = query.MaxEvents,
            MaxCandidates = query.MaxCandidates,
            MaxConcurrency = query.MaxConcurrency,
            Oldest = query.Oldest,
            ReadMode = query.ReadMode,
            IncludeBookmark = query.IncludeBookmark,
            Credential = query.Credential,
            Authentication = query.Authentication,
            RemoteConnectionTimeoutMilliseconds = query.RemoteConnectionTimeoutMilliseconds,
            RemoteReadTimeoutMilliseconds = query.RemoteReadTimeoutMilliseconds,
            BufferCapacity = query.BufferCapacity,
            MessageCulture = query.MessageCulture,
            FallbackMessageCulture = query.FallbackMessageCulture,
            Predicate = query.Predicate?.Clone(),
            ResultPredicate = query.ResultPredicate,
            MinimumRecordIdExclusiveResolver = query.MinimumRecordIdExclusiveResolver,
            BookmarkXmlResolver = query.BookmarkXmlResolver,
            BookmarkOffset = query.BookmarkOffset,
            StrictBookmark = query.StrictBookmark,
            CandidateObserver = query.CandidateObserver,
            ContinueOnRemoteFailure = query.ContinueOnRemoteFailure
        };
    }

    private static EventDefinition CopyDefinition(EventDefinition definition) => new() {
        Name = definition.Name.Trim(),
        DisplayName = definition.DisplayName?.Trim() ?? string.Empty,
        Description = definition.Description?.Trim() ?? string.Empty,
        Category = definition.Category?.Trim() ?? string.Empty,
        Sources = definition.Sources.Select(static source => new EventDefinitionSource {
            LogName = source.LogName.Trim(),
            EventIds = source.EventIds.Distinct().OrderBy(static id => id).ToArray(),
            ProviderNames = source.ProviderNames.Select(static provider => provider.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static provider => provider, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        }).ToArray(),
        Fields = definition.Fields.Select(static field => new EventDefinitionField {
            Name = field.Name.Trim(),
            DisplayName = field.DisplayName?.Trim() ?? string.Empty,
            Description = field.Description?.Trim() ?? string.Empty,
            Aliases = field.Aliases.Select(static alias => alias.Trim()).ToArray(),
            ValueKind = field.ValueKind,
            Source = field.Source,
            SourceName = field.SourceName?.Trim() ?? string.Empty,
            DefaultValue = field.DefaultValue
        }).ToArray()
    };

    private static object? ConvertFieldValue(EventDefinitionField field, object? value) {
        return field.ConvertValue(value);
    }

    private static string?[] NormalizeTargets(IReadOnlyList<string?>? machineNames) {
        IEnumerable<string?> candidates = machineNames == null || machineNames.Count == 0
            ? new string?[] { null }
            : machineNames;
        return candidates.Select(static machine => EventLogTarget.IsLocalMachine(machine) ? null : machine?.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? ResolveBookmark(
        EventDefinitionQuery query,
        string? machineName,
        string container) {

        string? bookmark = query.BookmarkXmlResolver?.Invoke(machineName, container);
        return string.IsNullOrWhiteSpace(bookmark) ? null : bookmark;
    }

    private static void HandleFailure(EventLogQueryFailure failure, EventDefinitionQueryExecutionInfo executionInfo) {
        if (EventLogRemoteQueryFailureClassifier.TryClassify(failure.MachineName, failure.Exception,
                out EventLogRemoteQueryFailureKind kind)) {
            executionInfo.RecordTargetFailure(new EventLogQueryTargetFailure(
                failure.MachineName!, failure.Source, kind, failure.Exception.Message));
            return;
        }
        throw failure.Exception;
    }
}
