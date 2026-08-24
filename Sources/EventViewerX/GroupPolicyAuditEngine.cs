using System.Runtime.CompilerServices;

namespace EventViewerX;

/// <summary>Reads and projects source-neutral Group Policy audit events.</summary>
public static class GroupPolicyAuditEngine {
    /// <summary>Streams matching Group Policy audit events.</summary>
    public static IAsyncEnumerable<GroupPolicyAuditRecord> ReadAsync(
        GroupPolicyAuditQuery query,
        CancellationToken cancellationToken = default) {

        return ReadAsync(query, new GroupPolicyAuditQueryExecutionInfo(), cancellationToken);
    }

    /// <summary>Streams matching Group Policy audit events and reports source progress and failures.</summary>
    public static async IAsyncEnumerable<GroupPolicyAuditRecord> ReadAsync(
        GroupPolicyAuditQuery query,
        GroupPolicyAuditQueryExecutionInfo executionInfo,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {

        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        if (executionInfo == null) {
            throw new ArgumentNullException(nameof(executionInfo));
        }
        executionInfo.Reset();
        GroupPolicyAuditQuery snapshot = CreateSnapshot(query);
        IReadOnlyDictionary<string, GroupPolicyAuditCheckpoint> checkpoints =
            CreateCheckpointIndex(snapshot.Checkpoints, snapshot.Oldest);
        var definitionInfo = new EventDefinitionQueryExecutionInfo();
        var bufferedProgress = snapshot.ContextStore == null
            ? null
            : new GroupPolicyAuditQueryExecutionInfo();
        var definitionQuery = new EventDefinitionQuery(GroupPolicyAuditDefinitions.CreateDirectoryChanges()) {
            Paths = snapshot.Paths,
            MachineNames = snapshot.MachineNames,
            CollectorLogName = snapshot.CollectorLogName,
            StartTime = snapshot.StartTime,
            EndTime = snapshot.EndTime,
            TimePeriod = snapshot.TimePeriod,
            MaxEvents = snapshot.MaxEvents,
            MaxCandidates = snapshot.MaxCandidates,
            MaxConcurrency = snapshot.MaxConcurrency,
            Oldest = snapshot.Oldest,
            ReadMode = EventReadMode.StructuredDataAndMessage,
            IncludeBookmark = true,
            Credential = snapshot.Credential,
            Authentication = snapshot.Authentication,
            RemoteConnectionTimeoutMilliseconds = snapshot.RemoteConnectionTimeoutMilliseconds,
            RemoteReadTimeoutMilliseconds = snapshot.RemoteReadTimeoutMilliseconds,
            BufferCapacity = snapshot.BufferCapacity,
            MessageCulture = snapshot.MessageCulture,
            FallbackMessageCulture = snapshot.FallbackMessageCulture,
            ContinueOnRemoteFailure = snapshot.ContinueOnRemoteFailure,
            StrictBookmark = snapshot.StrictCheckpoint,
            BookmarkXmlResolver = (target, container) => ResolveCheckpoint(checkpoints, target, container),
            CandidateObserver = source => (bufferedProgress ?? executionInfo)
                .RecordCheckpoint(source, snapshot.Oldest),
            ResultPredicate = IsGroupPolicyAuditEvent
        };
        List<GroupPolicyAuditBufferedRecord>? contextRecords = snapshot.ContextStore == null
            ? null
            : new List<GroupPolicyAuditBufferedRecord>();

        try {
            await foreach (CustomEventRecord record in EventDefinitionEngine.ReadAsync(
                               definitionQuery,
                               definitionInfo,
                               cancellationToken)) {
                var projected = new GroupPolicyAuditRecord(record);
                if (contextRecords == null) {
                    yield return projected;
                } else {
                    contextRecords.Add(new GroupPolicyAuditBufferedRecord(
                        projected,
                        bufferedProgress!.Checkpoints));
                }
            }
            if (contextRecords != null) {
                await FinalizeContextAsync(
                    contextRecords.Select(static item => item.Record).ToArray(),
                    snapshot.ContextStore!,
                    snapshot.AuthorizationContext,
                    cancellationToken).ConfigureAwait(false);
                await foreach (GroupPolicyAuditRecord record in DeliverBufferedAsync(
                                   contextRecords,
                                   bufferedProgress!.Checkpoints,
                                   executionInfo,
                                   cancellationToken)) {
                    yield return record;
                }
            }
        } finally {
            executionInfo.EventsScanned = definitionInfo.EventsScanned;
            if (contextRecords == null) {
                executionInfo.EventsEmitted = definitionInfo.EventsEmitted;
            }
            executionInfo.ScanLimitReached = definitionInfo.ScanLimitReached;
            executionInfo.ResultLimitReached = definitionInfo.ResultLimitReached;
            executionInfo.TargetFailures = definitionInfo.TargetFailures;
        }
    }

    internal static async IAsyncEnumerable<GroupPolicyAuditRecord> DeliverBufferedAsync(
        IReadOnlyList<GroupPolicyAuditBufferedRecord> records,
        IReadOnlyList<GroupPolicyAuditCheckpoint> finalCheckpoints,
        GroupPolicyAuditQueryExecutionInfo executionInfo,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {

        await Task.CompletedTask.ConfigureAwait(false);
        foreach (GroupPolicyAuditBufferedRecord item in records) {
            cancellationToken.ThrowIfCancellationRequested();
            executionInfo.RecordCheckpoints(item.Checkpoints);
            executionInfo.EventsEmitted++;
            yield return item.Record;
        }
        cancellationToken.ThrowIfCancellationRequested();
        executionInfo.RecordCheckpoints(finalCheckpoints);
    }

    /// <summary>Projects an already materialized source event through the Group Policy audit contract.</summary>
    public static GroupPolicyAuditRecord CreateRecord(EventObject source) {
        if (source == null) {
            throw new ArgumentNullException(nameof(source));
        }
        if (!IsSupportedSource(source)) {
            throw new ArgumentException(
                "The event must be Security event 5136, 5137, 5139, or 5141 from Microsoft-Windows-Security-Auditing.",
                nameof(source));
        }
        CustomEventRecord record = EventDefinitionEngine.CreateRecord(
            GroupPolicyAuditDefinitions.CreateDirectoryChanges(),
            source);
        if (!IsGroupPolicyAuditEvent(record)) {
            throw new ArgumentException("The event does not affect a Group Policy object, scope, or WMI filter.", nameof(source));
        }
        return new GroupPolicyAuditRecord(record);
    }

    /// <summary>
    /// Stores the complete available Group Policy timeline before resolving each record against that timeline.
    /// </summary>
    internal static async ValueTask FinalizeContextAsync(
        IReadOnlyList<GroupPolicyAuditRecord> records,
        IEventContextStore contextStore,
        CancellationToken cancellationToken = default) {

        await FinalizeContextAsync(
            records,
            contextStore,
            authorizationContext: null,
            cancellationToken).ConfigureAwait(false);
    }

    internal static async ValueTask FinalizeContextAsync(
        IReadOnlyList<GroupPolicyAuditRecord> records,
        IEventContextStore contextStore,
        string? authorizationContext,
        CancellationToken cancellationToken = default) {

        var storedFacts = new List<EventContextFact>(records.Count);
        var queries = new List<EventContextQuery>(records.Count);
        var recordIndexes = new List<int>(records.Count);
        for (int i = 0; i < records.Count; i++) {
            EventContextFact? fact = GroupPolicyContextFactFactory.Create(records[i]);
            if (fact != null) {
                storedFacts.Add(fact);
                queries.Add(CreateContextQuery(records[i], fact, authorizationContext));
                recordIndexes.Add(i);
            }
        }
        await contextStore.StoreManyAsync(storedFacts, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<EventContextResolution> resolutions = await contextStore
            .ResolveManyAsync(queries, cancellationToken)
            .ConfigureAwait(false);
        if (resolutions.Count != queries.Count) {
            throw new InvalidDataException("The context store returned an unexpected number of batch resolutions.");
        }
        for (int i = 0; i < resolutions.Count; i++) {
            records[recordIndexes[i]].ApplyContext(resolutions[i]);
        }
    }

    /// <summary>
    /// Projects an already materialized source event, stores only context carried by that event,
    /// and resolves the resulting event-time context.
    /// </summary>
    public static async ValueTask<GroupPolicyAuditRecord> CreateRecordAsync(
        EventObject source,
        IEventContextStore contextStore,
        CancellationToken cancellationToken = default) {

        return await CreateRecordAsync(
            source,
            contextStore,
            cancellationToken,
            authorizationContext: null).ConfigureAwait(false);
    }

    /// <summary>
    /// Projects one source event and resolves context visible to the supplied caller-authorized partition.
    /// </summary>
    public static async ValueTask<GroupPolicyAuditRecord> CreateRecordAsync(
        EventObject source,
        IEventContextStore contextStore,
        CancellationToken cancellationToken,
        string? authorizationContext) {

        if (contextStore == null) {
            throw new ArgumentNullException(nameof(contextStore));
        }
        GroupPolicyAuditRecord record = CreateRecord(source);
        await ApplyContextAsync(
            record,
            contextStore,
            authorizationContext,
            cancellationToken).ConfigureAwait(false);
        return record;
    }

    internal static GroupPolicyAuditQuery CreateSnapshot(GroupPolicyAuditQuery query) {
        if (query.MaxEvents < 0 || query.MaxCandidates < 0) {
            throw new ArgumentOutOfRangeException(nameof(query), "Event limits must be non-negative.");
        }
        if (query.Checkpoints != null && query.Checkpoints.Any(static checkpoint => checkpoint == null)) {
            throw new ArgumentException("Checkpoints cannot contain null values.", nameof(query));
        }
        if (query.Checkpoints != null && query.Checkpoints.Any(checkpoint => checkpoint.Oldest != query.Oldest)) {
            throw new ArgumentException("Every checkpoint must use the same Oldest ordering as the query.", nameof(query));
        }
        return new GroupPolicyAuditQuery {
            ContextStore = query.ContextStore,
            AuthorizationContext = NormalizeAuthorizationContext(query.AuthorizationContext),
            Paths = query.Paths?.ToArray(),
            MachineNames = query.MachineNames?.ToArray(),
            CollectorLogName = string.IsNullOrWhiteSpace(query.CollectorLogName)
                ? null
                : query.CollectorLogName!.Trim(),
            StartTime = query.StartTime,
            EndTime = query.EndTime,
            TimePeriod = query.TimePeriod,
            MaxEvents = query.MaxEvents,
            MaxCandidates = query.MaxCandidates,
            MaxConcurrency = query.MaxConcurrency,
            Oldest = query.Oldest,
            Checkpoints = query.Checkpoints?.Select(CopyCheckpoint).ToArray(),
            StrictCheckpoint = query.StrictCheckpoint,
            Credential = EventLogCredentialIdentity.Copy(query.Credential),
            Authentication = query.Authentication,
            RemoteConnectionTimeoutMilliseconds = query.RemoteConnectionTimeoutMilliseconds,
            RemoteReadTimeoutMilliseconds = query.RemoteReadTimeoutMilliseconds,
            BufferCapacity = query.BufferCapacity,
            MessageCulture = query.MessageCulture,
            FallbackMessageCulture = query.FallbackMessageCulture,
            ContinueOnRemoteFailure = query.ContinueOnRemoteFailure
        };
    }

    private static IReadOnlyDictionary<string, GroupPolicyAuditCheckpoint> CreateCheckpointIndex(
        IReadOnlyList<GroupPolicyAuditCheckpoint>? checkpoints,
        bool oldest) {

        var result = new Dictionary<string, GroupPolicyAuditCheckpoint>(StringComparer.OrdinalIgnoreCase);
        foreach (GroupPolicyAuditCheckpoint checkpoint in checkpoints ?? Array.Empty<GroupPolicyAuditCheckpoint>()) {
            if (string.IsNullOrWhiteSpace(checkpoint.QueryTarget) ||
                string.IsNullOrWhiteSpace(checkpoint.ContainerLogName) ||
                string.IsNullOrWhiteSpace(checkpoint.BookmarkXml)) {
                throw new ArgumentException(
                    "Every checkpoint requires QueryTarget, ContainerLogName, and BookmarkXml.",
                    nameof(checkpoints));
            }
            if (result.ContainsKey(checkpoint.SourceKey)) {
                throw new ArgumentException($"Duplicate checkpoint source '{checkpoint.SourceKey}'.", nameof(checkpoints));
            }
            if (checkpoint.Oldest != oldest) {
                throw new ArgumentException(
                    $"Checkpoint source '{checkpoint.SourceKey}' does not match the query ordering.",
                    nameof(checkpoints));
            }
            result.Add(checkpoint.SourceKey, checkpoint);
        }
        return result;
    }

    private static string? ResolveCheckpoint(
        IReadOnlyDictionary<string, GroupPolicyAuditCheckpoint> checkpoints,
        string? target,
        string container) {

        string key = GroupPolicyAuditCheckpoint.CreateSourceKey(target, container);
        return checkpoints.TryGetValue(key, out GroupPolicyAuditCheckpoint? checkpoint)
            ? checkpoint.BookmarkXml
            : null;
    }

    private static bool IsGroupPolicyAuditEvent(CustomEventRecord record) {
        string objectClass = Value(record, "ObjectClass");
        string attributeName = Value(record, "AttributeName");
        return string.Equals(objectClass, "groupPolicyContainer", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(objectClass, "msWMI-Som", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(attributeName, "gPLink", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(attributeName, "gPOptions", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(attributeName, "gPCWQLFilter", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSupportedSource(EventObject source) {
        return source.Id is 5136 or 5137 or 5139 or 5141 &&
               string.Equals(
                   source.ProviderName,
                   "Microsoft-Windows-Security-Auditing",
                   StringComparison.OrdinalIgnoreCase) &&
               string.Equals(source.OriginalLogName, "Security", StringComparison.OrdinalIgnoreCase);
    }

    private static string Value(CustomEventRecord record, string name) {
        return record.Values.TryGetValue(name, out object? value)
            ? value?.ToString() ?? string.Empty
            : string.Empty;
    }

    private static GroupPolicyAuditCheckpoint CopyCheckpoint(GroupPolicyAuditCheckpoint source) => new() {
        QueryTarget = source.QueryTarget.Trim(),
        ContainerLogName = source.ContainerLogName.Trim(),
        BookmarkXml = source.BookmarkXml,
        RecordId = source.RecordId,
        TimeCreatedUtc = source.TimeCreatedUtc,
        Oldest = source.Oldest
    };

    private static async ValueTask ApplyContextAsync(
        GroupPolicyAuditRecord record,
        IEventContextStore contextStore,
        string? authorizationContext,
        CancellationToken cancellationToken) {

        EventContextFact? fact = GroupPolicyContextFactFactory.Create(record);
        if (fact == null) {
            return;
        }
        await contextStore.StoreAsync(fact, cancellationToken).ConfigureAwait(false);
        await ResolveContextAsync(
            record,
            fact,
            contextStore,
            authorizationContext,
            cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask ResolveContextAsync(
        GroupPolicyAuditRecord record,
        EventContextFact fact,
        IEventContextStore contextStore,
        string? authorizationContext,
        CancellationToken cancellationToken) {

        EventContextResolution resolution = await contextStore.ResolveAsync(
            CreateContextQuery(record, fact, authorizationContext),
            cancellationToken).ConfigureAwait(false);
        record.ApplyContext(resolution);
    }

    private static EventContextQuery CreateContextQuery(
        GroupPolicyAuditRecord record,
        EventContextFact fact,
        string? authorizationContext) => new() {
        ObjectKind = EventContextObjectKind.GroupPolicy,
        CanonicalId = fact.CanonicalId,
        Alias = record.ObjectDistinguishedName,
        AtUtc = record.TimeCreatedUtc,
        AuthorizationContext = NormalizeAuthorizationContext(authorizationContext)
    };

    private static string? NormalizeAuthorizationContext(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value!.Trim().ToUpperInvariant();

}

internal sealed class GroupPolicyAuditBufferedRecord {
    internal GroupPolicyAuditBufferedRecord(
        GroupPolicyAuditRecord record,
        IReadOnlyList<GroupPolicyAuditCheckpoint> checkpoints) {

        Record = record ?? throw new ArgumentNullException(nameof(record));
        Checkpoints = checkpoints?.Select(static checkpoint => checkpoint.Copy()).ToArray()
            ?? throw new ArgumentNullException(nameof(checkpoints));
    }

    internal GroupPolicyAuditRecord Record { get; }

    internal IReadOnlyList<GroupPolicyAuditCheckpoint> Checkpoints { get; }
}
