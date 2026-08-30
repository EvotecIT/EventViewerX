using System.Security.Cryptography;
using System.Text.Json;
using DBAClientX;
using EventViewerX.Reporting;

namespace EventViewerX.Storage;

public sealed partial class EventStore {
    private static readonly HashSet<string> DerivedReportTypes = new(
        new[] { "EventStoreSummary", "EventAggregation", "EventOccurrence" },
        StringComparer.OrdinalIgnoreCase);

    /// <summary>Stores one normalized report and optional checkpoint in a single transaction.</summary>
    public Task<EventStoreWriteResult> WriteAsync(
        EventReport report,
        EventStoreCheckpoint? checkpoint = null,
        CancellationToken cancellationToken = default) =>
        WriteCoreAsync(
            report,
            checkpoint,
            expectedCheckpoint: null,
            compareExpectedCheckpoint: false,
            cancellationToken);

    /// <summary>
    /// Stores one normalized report and advances a checkpoint only when its
    /// current durable value still equals the value observed before querying.
    /// </summary>
    public Task<EventStoreWriteResult> WriteAsync(
        EventReport report,
        EventStoreCheckpoint checkpoint,
        EventStoreCheckpoint? expectedCheckpoint,
        CancellationToken cancellationToken = default) =>
        WriteCoreAsync(
            report,
            checkpoint,
            expectedCheckpoint,
            compareExpectedCheckpoint: true,
            cancellationToken);

    private async Task<EventStoreWriteResult> WriteCoreAsync(
        EventReport report,
        EventStoreCheckpoint? checkpoint,
        EventStoreCheckpoint? expectedCheckpoint,
        bool compareExpectedCheckpoint,
        CancellationToken cancellationToken) {

        if (report == null) {
            throw new ArgumentNullException(nameof(report));
        }
        EventStoreCheckpoint? checkpointSnapshot = SnapshotCheckpoint(checkpoint);
        EventStoreCheckpoint? expectedCheckpointSnapshot =
            SnapshotCheckpoint(expectedCheckpoint);
        EventReportRow[] rows = report.Rows.ToArray();
        EventReportSectionSchema[] incomingSchemas = report.Sections
            .Select(EventReportSectionSchema.FromSection)
            .ToArray();
        string? derivedReportType = rows.Select(static row => row.Type)
            .Concat(incomingSchemas.Select(static schema => schema.Name))
            .FirstOrDefault(DerivedReportTypes.Contains);
        if (derivedReportType != null) {
            throw new InvalidDataException(
                $"Derived {derivedReportType} reports cannot be written back into EventStore history. " +
                "Store source events and regenerate summaries from them.");
        }
        EventReportSectionSchema[] schemas = NormalizeIncomingSchemas(incomingSchemas);
        var schemaNames = new HashSet<string>(schemas.Select(static schema => schema.Name),
            StringComparer.OrdinalIgnoreCase);
        if (rows.Any(row => !schemaNames.Contains(row.Type) &&
                            !string.Equals(row.Type, "Generic", StringComparison.OrdinalIgnoreCase))) {
            throw new InvalidDataException("Every stored typed row must have a matching homogeneous report schema.");
        }
        rows = EventReportEngine.CreateStored(rows, schemas).Rows.ToArray();
        EnsureInitialized();

        using var sqlite = new SQLite { BusyTimeoutMs = 10000 };
        await using SQLiteAsyncSession session = await sqlite
            .OpenSessionAsync(Path, cancellationToken)
            .ConfigureAwait(false);
        return await session.RunInTransactionAsync(async (transaction, token) => {
            string updatedAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            IReadOnlyList<StoredDefinitionSchema> storedDefinitions = await transaction.QueryAsListAsync(
                "SELECT definition_name, schema_hash, schema_json FROM evx_definitions;",
                static record => new StoredDefinitionSchema(
                    record.GetString(0),
                    record.GetString(1),
                    record.GetString(2)),
                cancellationToken: token).ConfigureAwait(false);
            string[] ambiguousDefinitions = storedDefinitions
                .GroupBy(static definition => definition.Name, StringComparer.OrdinalIgnoreCase)
                .Where(static group => group.Count() > 1)
                .Select(static group => group.Key)
                .ToArray();
            if (ambiguousDefinitions.Length > 0) {
                throw new InvalidDataException(
                    "Stored definitions contain Unicode case-equivalent names that cannot be selected unambiguously: " +
                    string.Join(", ", ambiguousDefinitions) + ".");
            }
            var canonicalNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (EventReportSectionSchema incomingSchema in schemas) {
                string requestedName = incomingSchema.Name;
                StoredDefinitionSchema? existingDefinition = storedDefinitions.FirstOrDefault(definition =>
                    string.Equals(definition.Name, requestedName, StringComparison.OrdinalIgnoreCase));
                EventReportSectionSchema schema = incomingSchema;
                if (existingDefinition != null) {
                    EventReportSectionSchema storedSchema = DeserializeStoredSchema(existingDefinition);
                    if (storedSchema.Kind != schema.Kind) {
                        throw new InvalidDataException(
                            $"Stored definition '{existingDefinition.Name}' cannot change from " +
                            $"{storedSchema.Kind} to {schema.Kind}.");
                    }
                    schema = schema.Kind == EventReportSectionKind.Generic
                        ? MergeGenericSchemas(storedSchema, schema)
                        : schema;
                    schema.Name = existingDefinition.Name;
                }
                canonicalNames[requestedName] = schema.Name;
                string schemaHash = CreateSchemaHash(schema);
                if (existingDefinition != null &&
                    !string.Equals(existingDefinition.Hash, schemaHash, StringComparison.Ordinal) &&
                    !HasEquivalentSchema(existingDefinition.Json, schemaHash)) {
                    object? existingRows = await transaction.ExecuteScalarAsync(
                        "SELECT COUNT(*) FROM evx_events WHERE definition_name = $name;",
                        new Dictionary<string, object?> { ["$name"] = schema.Name },
                        token).ConfigureAwait(false);
                    if (Convert.ToInt64(existingRows, CultureInfo.InvariantCulture) > 0) {
                        throw new InvalidDataException(
                            $"Stored definition '{schema.Name}' has a different column schema. " +
                            "Prune its existing rows or use a new definition name before writing the revised schema.");
                    }
                }
                await transaction.ExecuteNonQueryAsync(
                    UpsertDefinitionSql,
                    new Dictionary<string, object?> {
                        ["$name"] = schema.Name,
                        ["$display"] = schema.DisplayName,
                        ["$description"] = schema.Description,
                        ["$kind"] = (int)schema.Kind,
                        ["$schemaHash"] = schemaHash,
                        ["$schema"] = JsonSerializer.Serialize(schema, JsonOptions),
                        ["$updated"] = updatedAt
                    },
                    token).ConfigureAwait(false);
            }
            int inserted = 0;
            foreach (EventReportRow row in rows) {
                token.ThrowIfCancellationRequested();
                string definitionName = canonicalNames.TryGetValue(row.Type, out string? canonicalName)
                    ? canonicalName
                    : row.Type;
                inserted += await transaction.ExecuteNonQueryAsync(
                    InsertEventSql,
                    CreateEventParameters(row, definitionName, updatedAt),
                    token).ConfigureAwait(false);
            }
            if (checkpointSnapshot != null) {
                EventStoreCheckpoint storedCheckpoint = await ResolveCheckpointIdentityAsync(
                    transaction,
                    checkpointSnapshot,
                    expectedCheckpointSnapshot,
                    compareExpectedCheckpoint,
                    token).ConfigureAwait(false);
                await transaction.ExecuteNonQueryAsync(
                    UpsertCheckpointSql,
                    new Dictionary<string, object?> {
                        ["$consumer"] = storedCheckpoint.Consumer,
                        ["$computer"] = storedCheckpoint.Computer,
                        ["$container"] = storedCheckpoint.Container,
                        ["$recordId"] = storedCheckpoint.RecordId,
                        ["$bookmark"] = storedCheckpoint.BookmarkXml,
                        ["$updated"] = updatedAt
                    },
                    token).ConfigureAwait(false);
            }
            return new EventStoreWriteResult(rows.Length, inserted, checkpointSnapshot != null);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Gets a detached durable checkpoint for one consumer and source.</summary>
    public async Task<EventStoreCheckpoint?> GetCheckpointAsync(
        string consumer,
        string computer,
        string container,
        CancellationToken cancellationToken = default) {

        if (string.IsNullOrWhiteSpace(consumer) || string.IsNullOrWhiteSpace(computer) ||
            string.IsNullOrWhiteSpace(container)) {
            throw new ArgumentException("Consumer, computer, and container are required.");
        }
        EnsureInitialized();
        using var sqlite = new SQLite { BusyTimeoutMs = 10000 };
        await using SQLiteAsyncSession session = await sqlite
            .OpenSessionAsync(Path, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<EventStoreCheckpoint> rows = await session.QueryAsListAsync(
            @"SELECT consumer, computer, container, record_id, bookmark_xml, updated_utc
              FROM evx_checkpoints;",
            static record => new EventStoreCheckpoint {
                Consumer = record.GetString(0),
                Computer = record.GetString(1),
                Container = record.GetString(2),
                RecordId = record.IsDBNull(3) ? null : record.GetInt64(3),
                BookmarkXml = record.IsDBNull(4) ? null : record.GetString(4),
                UpdatedAtUtc = DateTime.Parse(
                    record.GetString(5),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind)
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return rows
            .Where(row =>
                string.Equals(row.Consumer, consumer.Trim(), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(row.Computer, computer.Trim(), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(row.Container, container.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(static row => row.UpdatedAtUtc)
            .FirstOrDefault();
    }

    private static Dictionary<string, object?> CreateEventParameters(
        EventReportRow row,
        string definitionName,
        string insertedAt) {

        EventTransportKind transport = GetTransportKind(
            row.SourceKind,
            row.SourceLog,
            row.ContainerLog);
        return new Dictionary<string, object?> {
            ["$key"] = CreateEventKey(row, definitionName),
            ["$originalKey"] = CreateOriginalEventKey(row, definitionName),
            ["$transportKind"] = (int)transport,
            ["$observationIdentity"] = row.ObservationIdentity ?? string.Empty,
            ["$definition"] = definitionName,
            ["$time"] = row.TimeCreated.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            ["$eventId"] = row.EventId,
            ["$recordId"] = row.RecordId,
            ["$provider"] = row.Provider ?? string.Empty,
            ["$sourceLog"] = row.SourceLog ?? string.Empty,
            ["$containerLog"] = row.ContainerLog ?? string.Empty,
            ["$sourceComputer"] = row.SourceComputer ?? string.Empty,
            ["$collectorComputer"] = row.CollectorComputer ?? string.Empty,
            ["$level"] = row.Level ?? string.Empty,
            ["$levelValue"] = row.LevelValue,
            ["$activityId"] = row.ActivityId?.ToString("D"),
            ["$relatedActivityId"] = row.RelatedActivityId?.ToString("D"),
            ["$processId"] = row.ProcessId,
            ["$threadId"] = row.ThreadId,
            ["$message"] = row.Message ?? string.Empty,
            ["$values"] = JsonSerializer.Serialize(row.Values, JsonOptions),
            ["$received"] = row.ReceivedTimeUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            ["$processed"] = row.ProcessedTimeUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            ["$inserted"] = insertedAt
        };
    }

    private static string CreateEventKey(EventReportRow row, string definitionName) {
        string identity = string.Join("\0", new[] {
            CreateOriginalEventKey(row, definitionName),
            row.RecordId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            NormalizeSqliteNoCaseIdentity(row.ContainerLog),
            NormalizeSqliteNoCaseIdentity(row.CollectorComputer)
        });
        return CreateSha256(identity);
    }

    private static string CreateEventKey(
        StoredIdentityCandidate candidate,
        string definitionName,
        long? recordId) {

        string identity = string.Join("\0", new[] {
            CreateOriginalEventKey(candidate, definitionName, recordId),
            recordId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            NormalizeSqliteNoCaseIdentity(candidate.ContainerLog),
            NormalizeSqliteNoCaseIdentity(candidate.CollectorComputer)
        });
        return CreateSha256(identity);
    }

    private static string CreateOriginalEventKey(EventReportRow row, string definitionName) {
        string identity = CreateOriginalEventIdentity(
            row.SourceComputer,
            row.SourceLog,
            row.EventId,
            row.Provider,
            row.TimeCreated.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            definitionName,
            row.RecordId,
            row.ActivityId?.ToString("D"),
            row.RelatedActivityId?.ToString("D"),
            CreateSemanticIdentity(row.Values));
        return CreateSha256(identity);
    }

    private static string CreateOriginalEventKey(
        StoredIdentityCandidate candidate,
        string definitionName,
        long? recordId) {

        string identity = CreateOriginalEventIdentity(
            candidate.SourceComputer,
            candidate.SourceLog,
            candidate.EventId,
            candidate.Provider,
            candidate.TimeCreatedUtc,
            definitionName,
            recordId,
            candidate.ActivityId,
            candidate.RelatedActivityId,
            CreateSemanticIdentity(candidate.Values));
        return CreateSha256(identity);
    }

    private static string CreateOriginalEventIdentity(
        string? sourceComputer,
        string? sourceLog,
        int eventId,
        string? provider,
        string timeCreatedUtc,
        string definitionName,
        long? recordId,
        string? activityId,
        string? relatedActivityId,
        string semanticIdentity) {

        var components = new List<string> {
            NormalizeSqliteNoCaseIdentity(sourceComputer),
            NormalizeSqliteNoCaseIdentity(sourceLog),
            eventId.ToString(CultureInfo.InvariantCulture),
            NormalizeSqliteNoCaseIdentity(provider),
            timeCreatedUtc,
            NormalizeSqliteNoCaseIdentity(definitionName)
        };
        if (!recordId.HasValue) {
            components.Add(activityId ?? string.Empty);
            components.Add(relatedActivityId ?? string.Empty);
        }
        components.Add(semanticIdentity);
        return string.Join("\0", components);
    }

    private static string CreateSha256(string identity) {
        using SHA256 sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(identity));
        return string.Concat(hash.Select(static value => value.ToString("x2", CultureInfo.InvariantCulture)));
    }

    private static string CreateSemanticIdentity(IReadOnlyDictionary<string, object?> values) {
        return string.Join(
            "\u001e",
            values
                .OrderBy(static item => item.Key, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static item => item.Key, StringComparer.Ordinal)
                .Select(static item =>
                    NormalizeSqliteNoCaseIdentity(item.Key) +
                    "\u001f" +
                    JsonSerializer.Serialize(item.Value, JsonOptions)));
    }

    private static string CreateSemanticIdentity(IReadOnlyDictionary<string, JsonElement> values) {
        return string.Join(
            "\u001e",
            values
                .OrderBy(static item => item.Key, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static item => item.Key, StringComparer.Ordinal)
                .Select(static item =>
                    NormalizeSqliteNoCaseIdentity(item.Key) +
                    "\u001f" +
                    item.Value.GetRawText()));
    }

    private static EventTransportKind GetTransportKind(
        EventLogQuerySourceKind sourceKind,
        string? sourceLog,
        string? containerLog) {

        if (sourceKind == EventLogQuerySourceKind.File ||
            sourceKind == EventLogQuerySourceKind.Auto &&
            !string.IsNullOrWhiteSpace(containerLog) &&
            containerLog!.EndsWith(".evtx", StringComparison.OrdinalIgnoreCase)) {
            return EventTransportKind.File;
        }
        bool differentContainer = !string.Equals(
            sourceLog,
            containerLog,
            StringComparison.OrdinalIgnoreCase);
        return differentContainer
            ? EventTransportKind.Collector
            : EventTransportKind.Direct;
    }

    private static string NormalizeSqliteNoCaseIdentity(string? value) {
        if (string.IsNullOrEmpty(value)) {
            return string.Empty;
        }
        char[] characters = value!.ToCharArray();
        for (int index = 0; index < characters.Length; index++) {
            if (characters[index] is >= 'a' and <= 'z') {
                characters[index] = (char)(characters[index] - ('a' - 'A'));
            }
        }
        return new string(characters);
    }

    private static void ValidateCheckpoint(EventStoreCheckpoint? checkpoint) {
        if (checkpoint == null) {
            return;
        }
        if (string.IsNullOrWhiteSpace(checkpoint.Consumer) ||
            string.IsNullOrWhiteSpace(checkpoint.Computer) ||
            string.IsNullOrWhiteSpace(checkpoint.Container)) {
            throw new ArgumentException("Checkpoint consumer, computer, and container are required.", nameof(checkpoint));
        }
        if (checkpoint.RecordId < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(checkpoint),
                "Checkpoint record ID must be greater than or equal to zero.");
        }
    }

    private const string UpsertDefinitionSql = @"
INSERT INTO evx_definitions
    (definition_name, display_name, description, kind, schema_hash, schema_json, updated_utc)
VALUES ($name, $display, $description, $kind, $schemaHash, $schema, $updated)
ON CONFLICT(definition_name) DO UPDATE SET
    display_name = excluded.display_name,
    description = excluded.description,
    kind = excluded.kind,
    schema_hash = excluded.schema_hash,
    schema_json = excluded.schema_json,
    updated_utc = excluded.updated_utc;";

    private const string InsertEventSql = @"
INSERT OR IGNORE INTO evx_events
    (event_key, original_event_key, transport_kind, observation_identity,
     definition_name, event_time_utc, event_id, record_id, provider,
     source_log, container_log, source_computer, collector_computer, level,
     level_value, activity_id, related_activity_id, process_id, thread_id, message, values_json,
     received_time_utc, processed_time_utc, inserted_utc)
SELECT
    $key, $originalKey, $transportKind, $observationIdentity,
     $definition, $time, $eventId, $recordId, $provider,
     $sourceLog, $containerLog, $sourceComputer, $collectorComputer, $level,
     $levelValue, $activityId, $relatedActivityId, $processId, $threadId, $message, $values,
     COALESCE($received, $inserted), COALESCE($processed, $inserted), $inserted
WHERE $transportKind = 2 OR NOT EXISTS (
    SELECT 1
    FROM evx_events
    WHERE original_event_key = $originalKey
      AND transport_kind IN (0, 1)
      AND (
          transport_kind <> $transportKind
          OR ($transportKind = 1 AND
              collector_computer <> $collectorComputer COLLATE NOCASE)
      )
);";

    private const string UpsertCheckpointSql = @"
INSERT INTO evx_checkpoints
    (consumer, computer, container, record_id, bookmark_xml, updated_utc)
VALUES ($consumer, $computer, $container, $recordId, $bookmark, $updated)
ON CONFLICT(consumer, computer, container) DO UPDATE SET
    record_id = excluded.record_id,
    bookmark_xml = excluded.bookmark_xml,
    updated_utc = excluded.updated_utc;";

    private sealed class StoredIdentityCandidate {
        internal StoredIdentityCandidate(
            string timeCreatedUtc,
            int eventId,
            string provider,
            string sourceLog,
            string containerLog,
            string sourceComputer,
            string collectorComputer,
            string? activityId,
            string? relatedActivityId,
            IReadOnlyDictionary<string, JsonElement> values) {

            TimeCreatedUtc = timeCreatedUtc;
            EventId = eventId;
            Provider = provider;
            SourceLog = sourceLog;
            ContainerLog = containerLog;
            SourceComputer = sourceComputer;
            CollectorComputer = collectorComputer;
            ActivityId = activityId;
            RelatedActivityId = relatedActivityId;
            Values = values;
        }

        internal string TimeCreatedUtc { get; }
        internal int EventId { get; }
        internal string Provider { get; }
        internal string SourceLog { get; }
        internal string ContainerLog { get; }
        internal string SourceComputer { get; }
        internal string CollectorComputer { get; }
        internal string? ActivityId { get; }
        internal string? RelatedActivityId { get; }
        internal IReadOnlyDictionary<string, JsonElement> Values { get; }
    }

    private enum EventTransportKind {
        Direct,
        Collector,
        File
    }

}
