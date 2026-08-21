using System.Text.Json;
using DBAClientX;
using EventViewerX.Reporting;

namespace EventViewerX.Storage;

public sealed partial class EventStore {
    private const int StoredReadPageSize = 256;
    /// <summary>
    /// Explains direct SQLite selectors and conservatively treats typed predicate pushdown as managed when
    /// no store schema context is available. Use <see cref="PlanAsync"/> for an execution-accurate stored plan.
    /// </summary>
    public static EventStoreQueryPlan Plan(EventStoreQuery query) {
        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        EventStoreQuery snapshot = query.Snapshot();
        return CreatePlan(snapshot, PredicatePushdownPolicy.DisableAll, schemaContextKnown: false);
    }

    /// <summary>Explains the exact SQLite and managed stages after inspecting the selected stored schemas.</summary>
    public async Task<EventStoreQueryPlan> PlanAsync(
        EventStoreQuery query,
        CancellationToken cancellationToken = default) {

        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        EnsureInitialized();
        EventStoreQuery snapshot = query.Snapshot();
        using var sqlite = new SQLite { BusyTimeoutMs = 10000 };
        await using SQLiteAsyncSession session = await sqlite
            .OpenSessionAsync(Path, cancellationToken)
            .ConfigureAwait(false);
        StoredSchemaContext schemaContext = await ReadSchemaContextAsync(
            session,
            snapshot.ResolveDefinitionNames(),
            snapshot.DefinitionSchemas,
            cancellationToken).ConfigureAwait(false);
        snapshot.Predicate = NormalizeStoredPredicate(snapshot.Predicate, schemaContext.Schemas);
        return CreatePlan(snapshot, schemaContext.Pushdown, schemaContextKnown: true);
    }

    private static EventStoreQueryPlan CreatePlan(
        EventStoreQuery snapshot,
        PredicatePushdownPolicy pushdown,
        bool schemaContextKnown) {

        var steps = new List<EventStoreQueryPlanStep>();
        AddDirectPlanStep(steps, "Definition", snapshot.ResolveDefinitionNames(), caseInsensitive: true);
        AddDirectPlanStep(steps, "EventId", snapshot.EventIds);
        AddDirectPlanStep(steps, "RecordId", snapshot.RecordIds);
        AddDirectPlanStep(steps, "SourceComputer", snapshot.SourceComputers, caseInsensitive: true);
        AddDirectPlanStep(steps, "SourceLog", snapshot.SourceLogs, caseInsensitive: true);
        AddDirectPlanStep(steps, "Provider", snapshot.Providers, caseInsensitive: true);
        if (snapshot.StartTime.HasValue || snapshot.EndTime.HasValue) {
            steps.Add(new EventStoreQueryPlanStep(
                "TimeCreated boundary",
                EventStoreQueryPlanStage.Sql,
                "UTC time boundaries use the indexed event_time_utc column."));
        }
        if (snapshot.Predicate != null) {
            EventPredicatePlan predicatePlan = EventPredicatePlanner.Plan(snapshot.Predicate);
            bool providerPushdownSafe = CanUseSqliteNoCase(predicatePlan.NativeFilter?.ProviderNames);
            steps.AddRange(predicatePlan.Steps
                .Where(static step => !string.Equals(
                    step.Expression,
                    "Exact predicate verification",
                    StringComparison.Ordinal))
                .Select(step => CreatePredicatePlanStep(
                    step,
                    snapshot.Predicate,
                    pushdown,
                    schemaContextKnown,
                    providerPushdownSafe)));
            steps.Add(new EventStoreQueryPlanStep(
                "Exact predicate verification",
                EventStoreQueryPlanStage.Managed,
                "The complete predicate is verified against normalized typed values after SQL prefiltering."));
        }
        return new EventStoreQueryPlan(steps, snapshot.MaxCandidates);
    }

    private static EventStoreQueryPlanStep CreatePredicatePlanStep(
        EventPredicatePlanStep step,
        EventPredicate predicate,
        PredicatePushdownPolicy pushdown,
        bool schemaContextKnown,
        bool providerPushdownSafe) {

        if (step.Stage != EventPredicatePlanStage.Native) {
            return new EventStoreQueryPlanStep(
                step.Expression,
                EventStoreQueryPlanStage.Managed,
                step.Reason);
        }
        if (!TryResolveNativeAliases(step.Expression, out string[] aliases, out bool provider)) {
            return new EventStoreQueryPlanStep(
                step.Expression,
                EventStoreQueryPlanStage.Managed,
                "This native predicate dimension is conservatively verified in managed code because it is not recognized by the stored planner.");
        }
        if (provider && !providerPushdownSafe) {
            return new EventStoreQueryPlanStep(
                step.Expression,
                EventStoreQueryPlanStage.Managed,
                "Unicode-insensitive provider matching is verified in managed code because SQLite NOCASE folds ASCII only.");
        }
        if (!schemaContextKnown) {
            return new EventStoreQueryPlanStep(
                step.Expression,
                EventStoreQueryPlanStage.Managed,
                "The static plan has no stored schema context, so typed predicate pushdown is reported conservatively. Use PlanAsync for an execution-accurate plan.");
        }
        if (!pushdown.CanPush(predicate, aliases)) {
            return new EventStoreQueryPlanStep(
                step.Expression,
                EventStoreQueryPlanStage.Managed,
                "The selected stored schemas are generic or shadow this common field, so the predicate is evaluated in managed code.");
        }
        return new EventStoreQueryPlanStep(
            step.Expression,
            EventStoreQueryPlanStage.Sql,
            "The selected stored schemas preserve this common field, so the predicate uses indexed SQLite pushdown before exact verification.");
    }

    private static bool TryResolveNativeAliases(
        string expression,
        out string[] aliases,
        out bool provider) {

        string field = expression.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries)[0];
        provider = false;
        if (string.Equals(field, "EventId", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(field, "Id", StringComparison.OrdinalIgnoreCase)) {
            aliases = new[] { "EventId", "Id" };
            return true;
        }
        if (string.Equals(field, "RecordId", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(field, "EventRecordId", StringComparison.OrdinalIgnoreCase)) {
            aliases = new[] { "RecordId", "EventRecordId" };
            return true;
        }
        if (string.Equals(field, "ProviderName", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(field, "Provider", StringComparison.OrdinalIgnoreCase)) {
            aliases = new[] { "ProviderName", "Provider" };
            provider = true;
            return true;
        }
        if (string.Equals(field, "Level", StringComparison.OrdinalIgnoreCase)) {
            aliases = new[] { "Level" };
            return true;
        }
        if (string.Equals(field, "TimeCreated", StringComparison.OrdinalIgnoreCase)) {
            aliases = new[] { "TimeCreated", "When" };
            return true;
        }
        aliases = Array.Empty<string>();
        return false;
    }

    /// <summary>Reads normalized stored rows and recreates homogeneous report sections.</summary>
    public async Task<EventReport> ReadReportAsync(
        EventStoreQuery query,
        string? title = null,
        CancellationToken cancellationToken = default) {

        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        EnsureInitialized();
        EventStoreQuery snapshot = query.Snapshot();
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
            var rows = new List<EventReportRow>();
            long scanned = 0;
            long offset = 0;
            bool scanLimitReached = false;
            bool completed = false;
            while (!completed) {
                long remainingCandidates = command.CandidateLimit > 0
                    ? command.CandidateLimit - scanned
                    : long.MaxValue;
                long pageLimit = command.CandidateLimit > 0
                    ? Math.Min(StoredReadPageSize, remainingCandidates + 1)
                    : snapshot.MaxEvents > 0
                        ? Math.Min(StoredReadPageSize, snapshot.MaxEvents - rows.Count)
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
                    rows.Add(row);
                    if (snapshot.MaxEvents > 0 && rows.Count >= snapshot.MaxEvents) {
                        completed = true;
                        break;
                    }
                }
                if (candidates.Count < pageLimit) {
                    break;
                }
            }

            string[] definitionNames = rows.Select(static row => row.Type)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            HashSet<string> populatedDefinitions = new(definitionNames, StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<EventReportSectionSchema> schemas = rows.Count == 0
                ? schemaContext.Schemas
                : schemaContext.Schemas
                    .Where(schema => populatedDefinitions.Contains(schema.Name))
                    .ToArray();
            EventReportCoverage[] coverage = rows
                .GroupBy(static row => row.CollectorComputer + "\0" + row.SourceLog, StringComparer.OrdinalIgnoreCase)
                .Select(static group => {
                    EventReportRow first = group.First();
                    return new EventReportCoverage {
                        MachineName = first.CollectorComputer,
                        LogName = first.SourceLog,
                        Succeeded = true,
                        Status = "Stored",
                        Detail = string.Empty
                    };
                }).ToArray();
            return EventReportEngine.CreateStored(
                rows,
                schemas,
                title,
                coverage,
                eventsScanned: scanned,
                scanLimitReached: scanLimitReached);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static QueryCommand BuildReadCommand(
        EventStoreQuery query,
        PredicatePushdownPolicy pushdown) {

        WhereCommand filter = BuildWhere(query, includePredicateNative: true, pushdown: pushdown);

        bool requiresManagedFiltering = query.Predicate != null || RequiresManagedTextMatching(query);
        int candidateLimit = requiresManagedFiltering && query.MaxCandidates > 0
            ? checked((int)Math.Min(query.MaxCandidates, int.MaxValue - 1))
            : 0;
        string sql = @"SELECT definition_name, event_time_utc, event_id, record_id, provider,
source_log, container_log, source_computer, collector_computer, level, level_value, message, values_json, transport_kind
FROM evx_events";
        if (filter.Clauses.Count > 0) {
            sql += " WHERE " + string.Join(" AND ", filter.Clauses);
        }
        sql += query.Oldest ? " ORDER BY event_time_utc ASC, rowid ASC" : " ORDER BY event_time_utc DESC, rowid DESC";
        return new QueryCommand(sql, filter.Parameters, candidateLimit);
    }

    private static WhereCommand BuildWhere(
        EventStoreQuery query,
        bool includePredicateNative,
        PredicatePushdownPolicy? pushdown = null) {

        var where = new List<string>();
        var parameters = new Dictionary<string, object?>();
        AddSqliteNoCaseIn(where, parameters, "definition_name", "definition", query.ResolveDefinitionNames());
        AddBoundary(where, parameters, "event_time_utc", "$start", ">=", ToUtcText(query.StartTime));
        AddBoundary(where, parameters, "event_time_utc", "$end", "<=", ToUtcText(query.EndTime));
        AddIn(where, parameters, "event_id", "eventId", query.EventIds);
        AddIn(where, parameters, "record_id", "recordId", query.RecordIds);
        AddSqliteNoCaseIn(where, parameters, "source_computer", "sourceComputer", query.SourceComputers);
        AddSqliteNoCaseIn(where, parameters, "source_log", "sourceLog", query.SourceLogs);
        AddSqliteNoCaseIn(where, parameters, "provider", "provider", query.Providers);
        if (includePredicateNative && query.Predicate != null) {
            EventFilter? native = EventPredicatePlanner.Plan(query.Predicate).NativeFilter;
            if (native != null) {
                pushdown ??= PredicatePushdownPolicy.AllowAll;
                if (pushdown.CanPush(query.Predicate, "EventId", "Id")) {
                    AddIn(where, parameters, "event_id", "predicateEventId", native.EventIds);
                }
                if (pushdown.CanPush(query.Predicate, "RecordId", "EventRecordId")) {
                    AddIn(where, parameters, "record_id", "predicateRecordId", native.RecordIds);
                }
                if (pushdown.CanPush(query.Predicate, "ProviderName", "Provider")) {
                    AddSqliteNoCaseIn(where, parameters, "provider", "predicateProvider", native.ProviderNames);
                }
                if (pushdown.CanPush(query.Predicate, "Level")) {
                    AddIn(where, parameters, "level_value", "predicateLevel", native.Levels);
                }
                if (pushdown.CanPush(query.Predicate, "TimeCreated", "When")) {
                    AddBoundary(where, parameters, "event_time_utc", "$predicateStart", ">=", ToUtcText(native.StartTime));
                    AddBoundary(where, parameters, "event_time_utc", "$predicateEnd", "<=", ToUtcText(native.EndTime));
                }
            }
        }
        return new WhereCommand(where, parameters);
    }

    private static async Task<StoredSchemaContext> ReadSchemaContextAsync(
        SQLiteAsyncSession session,
        IReadOnlyList<string> definitionNames,
        IReadOnlyList<EventReportSectionSchema>? suppliedSchemas,
        CancellationToken cancellationToken) {

        var where = new List<string>();
        var parameters = new Dictionary<string, object?>();
        AddSqliteNoCaseIn(where, parameters, "definition_name", "pushdownSchema", definitionNames);
        string sql = "SELECT definition_name, schema_json FROM evx_definitions";
        if (where.Count > 0) {
            sql += " WHERE " + where[0];
        }
        sql += ";";
        IReadOnlyList<EventReportSectionSchema> loadedSchemas = await session.QueryAsListAsync(
            sql,
            static record => DeserializeStoredSchema(record.GetString(0), record.GetString(1)),
            parameters,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var schemas = loadedSchemas
            .Where(schema => MatchesText(definitionNames, schema.Name))
            .ToList();
        EventReportSectionSchema[] normalizedSupplied = NormalizeIncomingSchemas(
            suppliedSchemas ?? Array.Empty<EventReportSectionSchema>());
        foreach (EventReportSectionSchema supplied in normalizedSupplied) {
            if (definitionNames.Count > 0 && !MatchesText(definitionNames, supplied.Name)) {
                throw new ArgumentException(
                    $"Supplied schema '{supplied.Name}' is not part of the selected stored definitions.");
            }
            EventReportSectionSchema? stored = schemas.FirstOrDefault(schema => string.Equals(
                schema.Name,
                supplied.Name,
                StringComparison.OrdinalIgnoreCase));
            if (stored == null) {
                schemas.Add(supplied);
                continue;
            }
            if (stored.Kind != supplied.Kind ||
                !string.Equals(CreateSchemaHash(stored), CreateSchemaHash(supplied), StringComparison.Ordinal)) {
                throw new InvalidDataException(
                    $"Supplied schema '{supplied.Name}' does not match the schema already persisted in this store.");
            }
        }
        if (definitionNames.Count > 0) {
            var known = new HashSet<string>(
                schemas.Select(static schema => schema.Name),
                StringComparer.OrdinalIgnoreCase);
            foreach (string definitionName in definitionNames) {
                if (known.Contains(definitionName) ||
                    !BuiltInDefinitions.TryGetValue(definitionName, out EventTypeDefinition? definition) ||
                    definition.IsComposite) {
                    continue;
                }
                schemas.Add(EventReportSectionSchema.FromType(definition.Type));
                known.Add(definition.Name);
            }
        }
        PredicatePushdownPolicy pushdown = schemas.Any(static schema => schema.Kind == EventReportSectionKind.Generic)
            ? PredicatePushdownPolicy.DisableAll
            : new PredicatePushdownPolicy(schemas
                .SelectMany(static schema => schema.Columns)
                .Select(static column => column.Name));
        return new StoredSchemaContext(schemas, pushdown);
    }

    private static EventReportRow MapEventRow(
        IDataRecord record,
        IReadOnlyDictionary<string, EventReportSectionSchema> schemas) {

        string definitionName = record.GetString(0);
        schemas.TryGetValue(definitionName, out EventReportSectionSchema? schema);
        return new EventReportRow {
            Type = definitionName,
            TimeCreated = ParseUtc(record.GetString(1)),
            EventId = record.GetInt32(2),
            RecordId = record.IsDBNull(3) ? null : record.GetInt64(3),
            Provider = record.GetString(4),
            SourceLog = record.GetString(5),
            ContainerLog = record.GetString(6),
            SourceComputer = record.GetString(7),
            CollectorComputer = record.GetString(8),
            Level = record.GetString(9),
            LevelValue = record.IsDBNull(10) ? null : record.GetByte(10),
            Message = record.GetString(11),
            Values = DeserializeValues(record.GetString(12), schema),
            SourceKind = record.GetInt32(13) == 2
                ? EventLogQuerySourceKind.File
                : EventLogQuerySourceKind.Channel
        };
    }

    private static IReadOnlyDictionary<string, object?> DeserializeValues(
        string json,
        EventReportSectionSchema? schema) {

        Dictionary<string, JsonElement>? values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions);
        if (values == null) {
            return new Dictionary<string, object?>();
        }
        if (schema?.Kind == EventReportSectionKind.Generic) {
            return values.ToDictionary(
                static item => item.Key,
                static item => ConvertGenericJson(item.Value),
                StringComparer.OrdinalIgnoreCase);
        }
        IReadOnlyDictionary<string, Type> declaredTypes = CreateDeclaredTypes(schema);
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, JsonElement> item in values) {
            result[item.Key] = declaredTypes.TryGetValue(item.Key, out Type? declaredType) && declaredType != typeof(object)
                ? ConvertDeclaredJson(item.Value, declaredType, schema!.Name, item.Key)
                : ConvertJson(item.Value);
        }
        return result;
    }

    private static IReadOnlyDictionary<string, Type> CreateDeclaredTypes(EventReportSectionSchema? schema) {
        if (schema == null) {
            return new Dictionary<string, Type>();
        }
        var result = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        foreach (EventReportColumnSchema column in schema.Columns) {
            if (result.ContainsKey(column.Name)) {
                throw new InvalidDataException(
                    $"Stored definition '{schema.Name}' contains duplicate case-insensitive column '{column.Name}'.");
            }
            result.Add(
                column.Name,
                EventReportColumnSchema.ResolveValueTypeName(column.ValueTypeName));
        }
        return result;
    }

    private static EventPredicate? NormalizeStoredPredicate(
        EventPredicate? predicate,
        IReadOnlyList<EventReportSectionSchema> schemas) {

        if (predicate == null || schemas.Count != 1 ||
            schemas[0].Kind == EventReportSectionKind.Generic) {
            return predicate;
        }
        EventReportSectionSchema schema = schemas[0];
        EventPredicateBuilder builder = EventPredicateBuilder.ForFields(
            schema.Name,
            schema.Columns.Select(static column => new KeyValuePair<string, Type>(
                column.Name,
                EventReportColumnSchema.ResolveValueTypeName(column.ValueTypeName))),
            schema.DisplayName,
            schema.Columns.ToDictionary(
                static column => column.Name,
                static column => column.Aliases ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase));
        return builder.Normalize(predicate);
    }

    private static object? ConvertDeclaredJson(
        JsonElement value,
        Type declaredType,
        string definitionName,
        string fieldName) {

        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) {
            return null;
        }
        try {
            return JsonSerializer.Deserialize(value.GetRawText(), declaredType, JsonOptions);
        } catch (JsonException exception) {
            throw new InvalidDataException(
                $"Stored field '{definitionName}.{fieldName}' cannot be restored as '{declaredType.FullName}'.",
                exception);
        } catch (NotSupportedException exception) {
            throw new InvalidDataException(
                $"Stored field '{definitionName}.{fieldName}' uses unsupported type '{declaredType.FullName}'.",
                exception);
        }
    }

    private static object? ConvertJson(JsonElement value) => value.ValueKind switch {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.String when value.TryGetDateTime(out DateTime date) => date,
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number when value.TryGetInt64(out long integer) => integer,
        JsonValueKind.Number => value.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Array => value.EnumerateArray().Select(ConvertJson).ToArray(),
        JsonValueKind.Object => value.EnumerateObject().ToDictionary(
            static property => property.Name,
            static property => ConvertJson(property.Value),
            StringComparer.OrdinalIgnoreCase),
        _ => value.GetRawText()
    };

    private static object? ConvertGenericJson(JsonElement value) => value.ValueKind switch {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number when value.TryGetInt64(out long integer) => integer,
        JsonValueKind.Number => value.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Array => value.EnumerateArray().Select(ConvertGenericJson).ToArray(),
        JsonValueKind.Object => value.EnumerateObject().ToDictionary(
            static property => property.Name,
            static property => ConvertGenericJson(property.Value),
            StringComparer.OrdinalIgnoreCase),
        _ => value.GetRawText()
    };

    private static DateTime ParseUtc(string value) => DateTime.Parse(
        value,
        CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind).ToUniversalTime();

    private static string? ToUtcText(DateTime? value) => value?.ToUniversalTime()
        .ToString("O", CultureInfo.InvariantCulture);

    private static void AddBoundary(
        ICollection<string> where,
        IDictionary<string, object?> parameters,
        string column,
        string parameter,
        string operation,
        string? value) {

        if (value == null) {
            return;
        }
        where.Add($"{column} {operation} {parameter}");
        parameters[parameter] = value;
    }

    private static void AddIn<T>(
        ICollection<string> where,
        IDictionary<string, object?> parameters,
        string column,
        string prefix,
        IReadOnlyList<T>? values,
        bool caseInsensitive = false) {

        if (values == null || values.Count == 0) {
            return;
        }
        string parameter = $"${prefix}";
        if (parameters.ContainsKey(parameter)) {
            throw new InvalidOperationException($"Stored selector parameter '{parameter}' is already defined.");
        }
        parameters[parameter] = JsonSerializer.Serialize(values, JsonOptions);
        string expression = caseInsensitive ? $"{column} COLLATE NOCASE" : column;
        where.Add($"{expression} IN (SELECT value FROM json_each({parameter}))");
    }

    private static void AddSqliteNoCaseIn(
        ICollection<string> where,
        IDictionary<string, object?> parameters,
        string column,
        string prefix,
        IReadOnlyList<string>? values) {

        if (!CanUseSqliteNoCase(values)) {
            return;
        }
        AddIn(where, parameters, column, prefix, values, caseInsensitive: true);
    }

    private static bool CanUseSqliteNoCase(IReadOnlyList<string>? values) =>
        values == null || values.All(static value => value.All(static character => character <= 0x7F));

    private static bool RequiresManagedTextMatching(EventStoreQuery query) =>
        !CanUseSqliteNoCase(query.ResolveDefinitionNames()) ||
        !CanUseSqliteNoCase(query.SourceComputers) ||
        !CanUseSqliteNoCase(query.SourceLogs) ||
        !CanUseSqliteNoCase(query.Providers);

    private static bool MatchesDirectTextSelection(EventStoreQuery query, EventReportRow row) =>
        MatchesText(query.ResolveDefinitionNames(), row.Type) &&
        MatchesText(query.SourceComputers, row.SourceComputer) &&
        MatchesText(query.SourceLogs, row.SourceLog) &&
        MatchesText(query.Providers, row.Provider);

    private static bool MatchesText(IReadOnlyList<string>? expected, string actual) =>
        expected == null || expected.Count == 0 ||
        expected.Contains(actual, StringComparer.OrdinalIgnoreCase);

    private static void AddDirectPlanStep<T>(
        ICollection<EventStoreQueryPlanStep> steps,
        string name,
        IReadOnlyList<T>? values,
        bool caseInsensitive = false) {

        if (values == null || values.Count == 0) {
            return;
        }
        bool sql = !caseInsensitive || values is not IReadOnlyList<string> text || CanUseSqliteNoCase(text);
        steps.Add(new EventStoreQueryPlanStep(
            $"{name} ({values.Count})",
            sql ? EventStoreQueryPlanStage.Sql : EventStoreQueryPlanStage.Managed,
            sql
                ? $"{name} selection uses an indexed normalized SQLite column."
                : $"{name} uses managed ordinal-ignore-case verification because SQLite NOCASE folds ASCII only."));
    }

    private sealed class QueryCommand {
        internal QueryCommand(string sql, Dictionary<string, object?> parameters, int candidateLimit) {
            Sql = sql;
            Parameters = parameters;
            CandidateLimit = candidateLimit;
        }

        internal string Sql { get; }
        internal Dictionary<string, object?> Parameters { get; }
        internal int CandidateLimit { get; }
    }

    private sealed class WhereCommand {
        internal WhereCommand(List<string> clauses, Dictionary<string, object?> parameters) {
            Clauses = clauses;
            Parameters = parameters;
        }

        internal List<string> Clauses { get; }
        internal Dictionary<string, object?> Parameters { get; }
    }

    private sealed class PredicatePushdownPolicy {
        internal static readonly PredicatePushdownPolicy AllowAll = new(Array.Empty<string>());
        internal static readonly PredicatePushdownPolicy DisableAll = new(Array.Empty<string>(), disableAll: true);
        private readonly HashSet<string> _shadowedFields;
        private readonly bool _disableAll;

        internal PredicatePushdownPolicy(IEnumerable<string> shadowedFields, bool disableAll = false) {
            _shadowedFields = new HashSet<string>(shadowedFields, StringComparer.OrdinalIgnoreCase);
            _disableAll = disableAll;
        }

        internal bool CanPush(EventPredicate predicate, params string[] nativeAliases) =>
            !_disableAll && !UsesShadowedField(predicate, nativeAliases);

        private bool UsesShadowedField(EventPredicate predicate, IReadOnlyList<string> nativeAliases) {
            if (predicate.Kind == EventPredicateKind.Comparison) {
                return nativeAliases.Any(alias => string.Equals(alias, predicate.Field, StringComparison.OrdinalIgnoreCase)) &&
                       _shadowedFields.Contains(predicate.Field!);
            }
            return predicate.Children.Any(child => UsesShadowedField(child, nativeAliases));
        }
    }

    private sealed class StoredSchemaContext {
        internal StoredSchemaContext(
            IReadOnlyList<EventReportSectionSchema> schemas,
            PredicatePushdownPolicy pushdown) {

            Schemas = schemas;
            Pushdown = pushdown;
            ByName = schemas.ToDictionary(
                static schema => schema.Name,
                StringComparer.OrdinalIgnoreCase);
        }

        internal IReadOnlyList<EventReportSectionSchema> Schemas { get; }
        internal IReadOnlyDictionary<string, EventReportSectionSchema> ByName { get; }
        internal PredicatePushdownPolicy Pushdown { get; }
    }
}
