using DBAClientX;
using EventViewerX.Reporting;

namespace EventViewerX.Storage;

public sealed partial class EventStore {
    private static readonly IReadOnlyDictionary<string, StoredAggregationField> AggregationFields =
        CreateAggregationFields();

    /// <summary>Returns the deterministic execution owner for a stored aggregation.</summary>
    public static EventStoreAggregationPlan PlanAggregation(
        EventStoreQuery query,
        EventAggregationDefinition definition) {

        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        EventStoreQuery snapshot = query.Snapshot();
        EventAggregationDefinition aggregation = EventAggregationEngine
            .Aggregate(Array.Empty<EventReportRow>(), definition)
            .Definition;
        string? reason = GetManagedAggregationReason(snapshot, aggregation);
        if (reason == null && RequiresNativeTextInspection(aggregation)) {
            reason = "Native text aggregation requires store-aware Unicode inspection; use PlanAggregationAsync for a definitive execution owner.";
        }
        return CreateAggregationPlan(reason);
    }

    /// <summary>Returns the deterministic execution owner after inspecting data-dependent Unicode grouping requirements.</summary>
    public async Task<EventStoreAggregationPlan> PlanAggregationAsync(
        EventStoreQuery query,
        EventAggregationDefinition definition,
        CancellationToken cancellationToken = default) {

        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        EventStoreQuery snapshot = query.Snapshot();
        EventAggregationDefinition aggregation = EventAggregationEngine
            .Aggregate(Array.Empty<EventReportRow>(), definition)
            .Definition;
        string? reason = GetManagedAggregationReason(snapshot, aggregation);
        if (reason == null && RequiresNativeTextInspection(aggregation)) {
            EnsureInitialized();
            WhereCommand filter = BuildWhere(snapshot, includePredicateNative: false);
            using var sqlite = new SQLite { BusyTimeoutMs = 10000 };
            await using SQLiteAsyncSession session = await sqlite
                .OpenSessionAsync(Path, cancellationToken)
                .ConfigureAwait(false);
            if (await ContainsNonAsciiAggregationTextAsync(
                    session,
                    filter,
                    aggregation,
                    cancellationToken).ConfigureAwait(false)) {
                reason = "Matching native text contains non-ASCII data, so exact Unicode grouping uses the managed engine.";
            }
        }
        return CreateAggregationPlan(reason);
    }

    private static EventStoreAggregationPlan CreateAggregationPlan(string? reason) {
        return reason == null
            ? new EventStoreAggregationPlan(
                EventAggregationExecutionMode.SqlitePushdown,
                "The query and aggregation use exhaustive stored selectors, UTC calendar buckets, and native indexed columns.")
            : new EventStoreAggregationPlan(EventAggregationExecutionMode.Managed, reason);
    }

    /// <summary>
    /// Aggregates stored rows with safe SQLite pushdown and automatically falls back to the shared managed engine
    /// when exact normalization, Unicode grouping, predicates, or ranking semantics require it.
    /// </summary>
    public async Task<EventAggregationResult> AggregateAsync(
        EventStoreQuery query,
        EventAggregationDefinition definition,
        CancellationToken cancellationToken = default) {

        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        EventStoreQuery snapshot = query.Snapshot();
        EventAggregationDefinition aggregation = EventAggregationEngine
            .Aggregate(Array.Empty<EventReportRow>(), definition)
            .Definition;
        string? managedReason = GetManagedAggregationReason(snapshot, aggregation);
        if (managedReason != null) {
            return await AggregateManagedStreamingAsync(
                    snapshot,
                    aggregation,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        EnsureInitialized();
        WhereCommand filter = BuildWhere(snapshot, includePredicateNative: false);
        using var sqlite = new SQLite { BusyTimeoutMs = 10000 };
        await using SQLiteAsyncSession session = await sqlite
            .OpenSessionAsync(Path, cancellationToken)
            .ConfigureAwait(false);
        EventAggregationResult? pushed = await session
            .RunInTransactionAsync<EventAggregationResult?>(async (transaction, token) => {
                if (await ContainsNonAsciiAggregationTextAsync(
                        transaction,
                        filter,
                        aggregation,
                        token).ConfigureAwait(false)) {
                    return null;
                }

                object? inputValue = await transaction.ExecuteScalarAsync(
                    BuildCountSql(filter),
                    filter.Parameters,
                    token).ConfigureAwait(false);
                long inputRows = Convert.ToInt64(inputValue, CultureInfo.InvariantCulture);
                if (inputRows == 0) {
                    return new EventAggregationResult(
                        aggregation,
                        Array.Empty<EventAggregationRow>(),
                        EventAggregationInputCompleteness.Complete,
                        aggregationComplete: true,
                        diagnostic: null,
                        EventAggregationExecutionMode.SqlitePushdown,
                        inputRows: 0);
                }

                _ = TryGetBoundedGroupingIndex(aggregation, out string? groupingIndex);
                SqlAggregationCommand command = BuildAggregationCommand(
                    filter,
                    aggregation,
                    groupingIndex);
                IReadOnlyList<EventAggregationRow> pushedRows = await transaction.QueryAsListAsync(
                    command.Sql,
                    record => MapAggregationRow(record, aggregation, command),
                    command.Parameters,
                    cancellationToken: token).ConfigureAwait(false);
                if (pushedRows.Count > aggregation.MaximumGroups) {
                    return new EventAggregationResult(
                        aggregation,
                        Array.Empty<EventAggregationRow>(),
                        EventAggregationInputCompleteness.Complete,
                        aggregationComplete: false,
                        $"Aggregation state exceeded MaximumGroups {aggregation.MaximumGroups:N0}.",
                        EventAggregationExecutionMode.SqlitePushdown,
                        inputRows);
                }
                if (pushedRows.Any(row => aggregation.Measures.Any(measure =>
                        measure.Operation == EventAggregationOperation.DistinctCount &&
                        Convert.ToInt64(row.Measures[measure.OutputName!], CultureInfo.InvariantCulture) >
                        aggregation.MaximumDistinctValues))) {
                    return new EventAggregationResult(
                        aggregation,
                        Array.Empty<EventAggregationRow>(),
                        EventAggregationInputCompleteness.Complete,
                        aggregationComplete: false,
                        $"A distinct measure exceeded MaximumDistinctValues {aggregation.MaximumDistinctValues:N0}.",
                        EventAggregationExecutionMode.SqlitePushdown,
                        inputRows);
                }
                long stateBytes = EstimateAggregationStateBytes(pushedRows, aggregation);
                if (stateBytes > aggregation.MaximumStateBytes) {
                    return new EventAggregationResult(
                        aggregation,
                        Array.Empty<EventAggregationRow>(),
                        EventAggregationInputCompleteness.Complete,
                        aggregationComplete: false,
                        $"Aggregation state exceeded MaximumStateBytes {aggregation.MaximumStateBytes:N0}.",
                        EventAggregationExecutionMode.SqlitePushdown,
                        inputRows);
                }
                EventAggregationRow[] selected = ApplyStoredTop(pushedRows, aggregation);
                return new EventAggregationResult(
                    aggregation,
                    selected,
                    EventAggregationInputCompleteness.Complete,
                    aggregationComplete: true,
                    diagnostic: null,
                    EventAggregationExecutionMode.SqlitePushdown,
                    inputRows);
            }, cancellationToken).ConfigureAwait(false);
        if (pushed == null) {
            return await AggregateManagedStreamingAsync(
                    snapshot,
                    aggregation,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        return pushed;
    }

    private static string? GetManagedAggregationReason(
        EventStoreQuery query,
        EventAggregationDefinition definition) {

        if (query.MaxEvents != 0) {
            return "MaxEvents makes the stored input partial; exhaustive aggregation requires MaxEvents zero.";
        }
        if (query.Predicate != null || RequiresManagedTextMatching(query)) {
            return "The stored selection requires exact managed predicate or Unicode text verification.";
        }
        if (!string.Equals(definition.TimeZoneId, "UTC", StringComparison.OrdinalIgnoreCase)) {
            return "Non-UTC calendar buckets use the shared timezone and DST-aware managed engine.";
        }
        if (definition.Measures.Any(static measure =>
                measure.Operation == EventAggregationOperation.DistinctCount)) {
            return "DistinctCount uses the managed engine so MaximumDistinctValues and MaximumStateBytes are enforced before unbounded distinct state is accumulated.";
        }
        IEnumerable<string> fields = definition.GroupBy.Concat(
            definition.Measures.Where(static measure => measure.Field != null)
                .Select(static measure => measure.Field!));
        string? unsupported = fields.FirstOrDefault(field => !AggregationFields.ContainsKey(field));
        if (unsupported != null) {
            return $"Field '{unsupported}' requires normalized managed evaluation because it is not a native stored column.";
        }
        EventAggregationMeasure? parsedDateMeasure = definition.Measures.FirstOrDefault(measure =>
            measure.Operation is EventAggregationOperation.FirstSeen or EventAggregationOperation.LastSeen &&
            AggregationFields[measure.Field!].ValueKind != StoredAggregationValueKind.DateTime);
        if (parsedDateMeasure != null) {
            return $"{parsedDateMeasure.Operation} over field '{parsedDateMeasure.Field}' requires managed date parsing because the stored column is not a native UTC timestamp.";
        }
        if (definition.Top > 0 && definition.Bucket != EventAggregationBucket.None &&
            definition.TopScope == EventAggregationTopScope.GlobalGroup) {
            return "Global top-N across time buckets uses the managed engine so ranking measures retain exact semantics.";
        }
        if (!TryGetBoundedGroupingIndex(definition, out _)) {
            return "The selected grouping has no ordered SQLite index, so the shared streaming managed engine enforces MaximumGroups before unbounded database grouping state can accumulate.";
        }
        return null;
    }

    private async Task<EventAggregationResult> AggregateManagedStreamingAsync(
        EventStoreQuery snapshot,
        EventAggregationDefinition aggregation,
        CancellationToken cancellationToken) {

        EnsureInitialized();
        using var sqlite = new SQLite { BusyTimeoutMs = 10000 };
        await using SQLiteAsyncSession session = await sqlite
            .OpenSessionAsync(Path, cancellationToken)
            .ConfigureAwait(false);
        return await session.RunInTransactionAsync(async (transaction, token) => {
            StoredSchemaContext schemaContext = await ReadSchemaContextAsync(
                transaction,
                snapshot.ResolveDefinitionNames(),
                snapshot.DefinitionSchemas,
                token).ConfigureAwait(false);
            snapshot.Predicate = NormalizeStoredPredicate(snapshot.Predicate, schemaContext.Schemas);
            QueryCommand command = BuildReadCommand(snapshot, schemaContext.Pushdown);
            EventAggregationAccumulator accumulator = EventAggregationEngine.CreateAccumulator(
                aggregation,
                EventAggregationInputCompleteness.Unknown);
            long scanned = 0;
            long selected = 0;
            long offset = 0;
            bool scanLimitReached = false;
            bool resultLimitReached = false;
            bool aggregationBoundReached = false;
            bool completed = false;
            while (!completed) {
                long remainingCandidates = command.CandidateLimit > 0
                    ? command.CandidateLimit - scanned
                    : long.MaxValue;
                long pageLimit = command.CandidateLimit > 0
                    ? Math.Min(StoredReadPageSize, remainingCandidates + 1)
                    : snapshot.MaxEvents > 0
                        ? Math.Min(StoredReadPageSize, GetResultProbeLimit(snapshot.MaxEvents, selected))
                        : StoredReadPageSize;
                if (pageLimit <= 0) {
                    break;
                }
                var pageParameters = new Dictionary<string, object?>(command.Parameters) {
                    ["$pageLimit"] = pageLimit,
                    ["$pageOffset"] = offset
                };
                IReadOnlyList<EventReportRow> candidates = await transaction.QueryAsListAsync(
                    command.Sql + " LIMIT $pageLimit OFFSET $pageOffset;",
                    record => MapEventRow(record, schemaContext.ByName),
                    pageParameters,
                    cancellationToken: token).ConfigureAwait(false);
                if (candidates.Count == 0) {
                    break;
                }
                offset += candidates.Count;
                foreach (EventReportRow row in candidates) {
                    token.ThrowIfCancellationRequested();
                    if (command.CandidateLimit > 0 && scanned >= command.CandidateLimit) {
                        scanLimitReached = true;
                        completed = true;
                        break;
                    }
                    scanned++;
                    if (!MatchesDirectTextSelection(snapshot, row)) {
                        continue;
                    }
                    if (snapshot.Predicate != null &&
                        !EventPredicateEvaluator.Matches(snapshot.Predicate, row.ToPredicateDictionary())) {
                        continue;
                    }
                    if (snapshot.MaxEvents > 0 && selected >= snapshot.MaxEvents) {
                        resultLimitReached = true;
                        completed = true;
                        break;
                    }
                    selected++;
                    if (!accumulator.Add(row)) {
                        aggregationBoundReached = true;
                        completed = true;
                        break;
                    }
                }
                if (candidates.Count < pageLimit) {
                    break;
                }
            }
            EventAggregationInputCompleteness completeness =
                scanLimitReached || resultLimitReached || aggregationBoundReached
                    ? EventAggregationInputCompleteness.Incomplete
                    : EventAggregationInputCompleteness.Complete;
            string? diagnostic = EventCompletenessDiagnostic.Compose(
                CreateReadCompletenessDiagnostic(
                    snapshot.MaxEvents,
                    scanLimitReached,
                    resultLimitReached),
                aggregationBoundReached
                    ? "The stored scan stopped when an aggregation bound was reached"
                    : null);
            return accumulator.Complete(completeness, diagnostic);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static bool TryGetBoundedGroupingIndex(
        EventAggregationDefinition definition,
        out string? indexName) {

        indexName = null;
        if (definition.Bucket != EventAggregationBucket.None) {
            return false;
        }
        string[] columns = definition.GroupBy
            .Select(field => AggregationFields[field].Sql)
            .ToArray();
        if (columns.Length == 0) {
            return true;
        }
        if (columns.SequenceEqual(new[] { "source_computer" }, StringComparer.Ordinal) ||
            columns.SequenceEqual(new[] { "source_computer", "source_log" }, StringComparer.Ordinal)) {
            indexName = "ix_evx_events_source_nocase_time";
            return true;
        }
        if (columns.SequenceEqual(new[] { "provider" }, StringComparer.Ordinal)) {
            indexName = "ix_evx_events_provider_nocase_time";
            return true;
        }
        if (columns.SequenceEqual(new[] { "event_id" }, StringComparer.Ordinal)) {
            indexName = "ix_evx_events_event_id_time";
            return true;
        }
        if (columns.SequenceEqual(new[] { "event_time_utc" }, StringComparer.Ordinal)) {
            indexName = "ix_evx_events_time";
            return true;
        }
        return false;
    }

    private static bool RequiresNativeTextInspection(EventAggregationDefinition definition) =>
        definition.GroupBy
            .Concat(definition.Measures.Where(static measure => measure.Field != null)
                .Select(static measure => measure.Field!))
            .Select(field => AggregationFields[field])
            .Any(static field => field.IsText);

    private static async Task<bool> ContainsNonAsciiAggregationTextAsync(
        SQLiteAsyncSession session,
        WhereCommand filter,
        EventAggregationDefinition definition,
        CancellationToken cancellationToken) {

        string[] expressions = definition.GroupBy
            .Concat(definition.Measures.Where(static measure => measure.Field != null)
                .Select(static measure => measure.Field!))
            .Select(field => AggregationFields[field])
            .Where(static field => field.IsText)
            .Select(static field => field.Sql)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (expressions.Length == 0) {
            return false;
        }
        string sql = "SELECT 1 FROM evx_events";
        var clauses = new List<string>(filter.Clauses) {
            "(" + string.Join(" OR ", expressions.Select(static expression =>
                $"({expression} IS NOT NULL AND {expression} GLOB '*[^ -~]*')")) + ")"
        };
        sql += " WHERE " + string.Join(" AND ", clauses) + " LIMIT 1;";
        object? value = await session.ExecuteScalarAsync(
            sql,
            filter.Parameters,
            cancellationToken).ConfigureAwait(false);
        return value != null && value != DBNull.Value;
    }

    private static SqlAggregationCommand BuildAggregationCommand(
        WhereCommand filter,
        EventAggregationDefinition definition,
        string? groupingIndex) {

        var select = new List<string>();
        var groupBy = new List<string>();
        foreach (string fieldName in definition.GroupBy) {
            StoredAggregationField field = AggregationFields[fieldName];
            string grouping = field.IsText ? $"{field.Sql} COLLATE NOCASE" : field.Sql;
            select.Add(field.IsText ? $"MIN({field.Sql})" : field.Sql);
            groupBy.Add(grouping);
        }
        string? bucket = GetSqlBucket(definition.Bucket);
        if (bucket != null) {
            select.Add(bucket);
            groupBy.Add(bucket);
        }
        foreach (EventAggregationMeasure measure in definition.Measures) {
            select.Add(GetSqlMeasure(measure));
        }
        string sql = "SELECT " + string.Join(", ", select) + " FROM evx_events" +
                     (groupingIndex == null ? string.Empty : $" INDEXED BY {groupingIndex}");
        var aggregationClauses = new List<string>(filter.Clauses);
        if (definition.GroupNulls == EventAggregationNullPolicy.Exclude) {
            aggregationClauses.AddRange(definition.GroupBy.Select(fieldName =>
                AggregationFields[fieldName].Sql + " IS NOT NULL"));
        }
        if (aggregationClauses.Count > 0) {
            sql += " WHERE " + string.Join(" AND ", aggregationClauses);
        }
        if (groupBy.Count > 0) {
            sql += " GROUP BY " + string.Join(", ", groupBy);
        }
        long limit = (long)definition.MaximumGroups + 1L;
        sql += " ORDER BY " + (groupBy.Count > 0 ? string.Join(", ", groupBy) : "1") +
               $" LIMIT {limit};";
        return new SqlAggregationCommand(sql, filter.Parameters, bucket != null);
    }

    private static string BuildCountSql(WhereCommand filter) {
        string sql = "SELECT COUNT(*) FROM evx_events";
        if (filter.Clauses.Count > 0) {
            sql += " WHERE " + string.Join(" AND ", filter.Clauses);
        }
        return sql + ";";
    }

    private static string GetSqlMeasure(EventAggregationMeasure measure) {
        if (measure.Operation is EventAggregationOperation.Count or EventAggregationOperation.Rate) {
            return "COUNT(*)";
        }
        StoredAggregationField field = AggregationFields[measure.Field!];
        string distinctField = field.IsText ? $"UPPER({field.Sql})" : field.Sql;
        return measure.Operation switch {
            EventAggregationOperation.DistinctCount when measure.Nulls == EventAggregationNullPolicy.Include =>
                $"COUNT(DISTINCT {distinctField}) + CASE WHEN SUM(CASE WHEN {field.Sql} IS NULL THEN 1 ELSE 0 END) > 0 THEN 1 ELSE 0 END",
            EventAggregationOperation.DistinctCount => $"COUNT(DISTINCT {distinctField})",
            EventAggregationOperation.FirstSeen => $"MIN({field.Sql})",
            EventAggregationOperation.LastSeen => $"MAX({field.Sql})",
            _ => throw new ArgumentOutOfRangeException(nameof(measure))
        };
    }

    private static string? GetSqlBucket(EventAggregationBucket bucket) => bucket switch {
        EventAggregationBucket.None => null,
        EventAggregationBucket.Hour => "strftime('%Y-%m-%dT%H:00:00.0000000Z', event_time_utc)",
        EventAggregationBucket.Day => "strftime('%Y-%m-%dT00:00:00.0000000Z', event_time_utc)",
        EventAggregationBucket.Week =>
            "strftime('%Y-%m-%dT00:00:00.0000000Z', event_time_utc, '-' || ((CAST(strftime('%w', event_time_utc) AS INTEGER) + 6) % 7) || ' days')",
        EventAggregationBucket.Month => "strftime('%Y-%m-01T00:00:00.0000000Z', event_time_utc)",
        _ => throw new ArgumentOutOfRangeException(nameof(bucket))
    };

    private static EventAggregationRow MapAggregationRow(
        IDataRecord record,
        EventAggregationDefinition definition,
        SqlAggregationCommand command) {

        int index = 0;
        var group = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (string field in definition.GroupBy) {
            group[field] = ReadRecordValue(record, index++, AggregationFields[field]);
        }
        DateTime? start = null;
        DateTime? end = null;
        string? label = null;
        if (command.HasBucket) {
            start = ParseUtc(record.GetString(index++));
            end = definition.Bucket switch {
                EventAggregationBucket.Hour => start.Value.AddHours(1),
                EventAggregationBucket.Day => start.Value.AddDays(1),
                EventAggregationBucket.Week => start.Value.AddDays(7),
                EventAggregationBucket.Month => start.Value.AddMonths(1),
                _ => throw new ArgumentOutOfRangeException(nameof(definition.Bucket))
            };
            label = start.Value.ToString(
                definition.Bucket == EventAggregationBucket.Hour ? "yyyy-MM-dd HH:00 +00:00" : "yyyy-MM-dd +00:00",
                CultureInfo.InvariantCulture);
        }
        var measures = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (EventAggregationMeasure measure in definition.Measures) {
            object? value = record.IsDBNull(index) ? null : record.GetValue(index);
            index++;
            measures[measure.OutputName!] = measure.Operation switch {
                EventAggregationOperation.Count or EventAggregationOperation.DistinctCount =>
                    Convert.ToInt64(value, CultureInfo.InvariantCulture),
                EventAggregationOperation.FirstSeen or EventAggregationOperation.LastSeen =>
                    value == null ? null : ParseUtc(Convert.ToString(value, CultureInfo.InvariantCulture)!),
                EventAggregationOperation.Rate => GetStoredRate(
                    Convert.ToInt64(value, CultureInfo.InvariantCulture),
                    measure.RateUnit!.Value,
                    start ?? definition.WindowStart!.Value,
                    end ?? definition.WindowEnd!.Value),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        return new EventAggregationRow {
            Group = group,
            BucketStartUtc = start,
            BucketEndUtc = end,
            BucketLabel = label,
            Measures = measures
        };
    }

    private static object? ReadRecordValue(IDataRecord record, int index, StoredAggregationField field) {
        if (record.IsDBNull(index)) {
            return null;
        }
        object value = record.GetValue(index);
        return field.ValueKind switch {
            StoredAggregationValueKind.Int32 => Convert.ToInt32(value, CultureInfo.InvariantCulture),
            StoredAggregationValueKind.Int64 => Convert.ToInt64(value, CultureInfo.InvariantCulture),
            StoredAggregationValueKind.Byte => Convert.ToByte(value, CultureInfo.InvariantCulture),
            StoredAggregationValueKind.DateTime => ParseUtc(Convert.ToString(value, CultureInfo.InvariantCulture)!),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };
    }

    private static double GetStoredRate(long count, TimeSpan unit, DateTime start, DateTime end) =>
        count / ((end - start).Ticks / (double)unit.Ticks);

    private static EventAggregationRow[] ApplyStoredTop(
        IReadOnlyList<EventAggregationRow> rows,
        EventAggregationDefinition definition) {

        IEnumerable<EventAggregationRow> selected = rows;
        if (definition.Top > 0) {
            selected = definition.TopScope == EventAggregationTopScope.PerBucket
                ? rows.GroupBy(static row => row.BucketStartUtc)
                    .SelectMany(group => RankStored(group, definition).Take(definition.Top))
                : RankStored(rows, definition).Take(definition.Top);
        }
        return selected.OrderBy(static row => row.BucketStartUtc)
            .ThenBy(row => string.Join("\u001f", definition.GroupBy.Select(field =>
                EventAggregationEngine.Canonicalize(row.Group[field]))), StringComparer.Ordinal)
            .ToArray();
    }

    private static long EstimateAggregationStateBytes(
        IEnumerable<EventAggregationRow> rows,
        EventAggregationDefinition definition) {

        long bytes = 0;
        foreach (EventAggregationRow row in rows) {
            string groupIdentity = string.Concat(definition.GroupBy.Select(field =>
                EventAggregationEngine.Canonicalize(row.Group[field])));
            DateTime? start = row.BucketStartUtc ?? definition.WindowStart;
            DateTime? end = row.BucketEndUtc ?? definition.WindowEnd;
            string bucketIdentity = start.HasValue && end.HasValue
                ? start.Value.ToUniversalTime().Ticks.ToString("D19", CultureInfo.InvariantCulture) + "/" +
                  end.Value.ToUniversalTime().Ticks.ToString("D19", CultureInfo.InvariantCulture)
                : string.Empty;
            bytes = checked(bytes + AggregationState.EstimateBytes(
                groupIdentity,
                bucketIdentity,
                definition.Measures));
        }
        return bytes;
    }

    private static IOrderedEnumerable<EventAggregationRow> RankStored(
        IEnumerable<EventAggregationRow> rows,
        EventAggregationDefinition definition) => rows
        .OrderByDescending(
            row => row.Measures[definition.RankingMeasure!],
            EventAggregationEngine.ValueComparer)
        .ThenBy(row => string.Join("\u001f", definition.GroupBy.Select(field =>
            EventAggregationEngine.Canonicalize(row.Group[field]))), StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, StoredAggregationField> CreateAggregationFields() {
        var fields = new Dictionary<string, StoredAggregationField>(StringComparer.OrdinalIgnoreCase);
        Add("definition_name", StoredAggregationValueKind.Text, "Type", "TypeName");
        Add("event_time_utc", StoredAggregationValueKind.DateTime, "TimeCreated", "When");
        Add("event_id", StoredAggregationValueKind.Int32, "EventId", "Id");
        Add("record_id", StoredAggregationValueKind.Int64, "RecordId", "EventRecordId");
        Add("provider", StoredAggregationValueKind.Text, "Provider", "ProviderName");
        Add("source_log", StoredAggregationValueKind.Text, "SourceLog", "SourceLogName", "LogName");
        Add("container_log", StoredAggregationValueKind.Text, "ContainerLog", "ContainerLogName");
        Add("source_computer", StoredAggregationValueKind.Text, "SourceComputer", "MachineName", "Computer");
        Add("collector_computer", StoredAggregationValueKind.Text, "CollectorComputer");
        Add("level", StoredAggregationValueKind.Text, "Level", "LevelDisplayName");
        Add("level_value", StoredAggregationValueKind.Byte, "LevelValue");
        Add("message", StoredAggregationValueKind.Text, "Message");
        return fields;

        void Add(string sql, StoredAggregationValueKind kind, params string[] aliases) {
            var field = new StoredAggregationField(sql, kind);
            foreach (string alias in aliases) {
                fields.Add(alias, field);
            }
        }
    }

    private sealed class StoredAggregationField {
        internal StoredAggregationField(string sql, StoredAggregationValueKind valueKind) {
            Sql = sql;
            ValueKind = valueKind;
        }
        internal string Sql { get; }
        internal StoredAggregationValueKind ValueKind { get; }
        internal bool IsText => ValueKind == StoredAggregationValueKind.Text;
    }

    private enum StoredAggregationValueKind {
        Text,
        Int32,
        Int64,
        Byte,
        DateTime
    }

    private sealed class SqlAggregationCommand {
        internal SqlAggregationCommand(
            string sql,
            Dictionary<string, object?> parameters,
            bool hasBucket) {
            Sql = sql;
            Parameters = parameters;
            HasBucket = hasBucket;
        }
        internal string Sql { get; }
        internal Dictionary<string, object?> Parameters { get; }
        internal bool HasBucket { get; }
    }
}
