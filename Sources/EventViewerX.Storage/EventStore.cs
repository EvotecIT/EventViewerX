using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using DBAClientX;

namespace EventViewerX.Storage;

/// <summary>Optional local SQLite history store backed by the shared DbaClientX provider layer.</summary>
public sealed partial class EventStore {
    private const int SchemaVersion = 1;
    private readonly object _initializationLock = new();
    private bool _initialized;

    /// <summary>Creates a local event store for one SQLite database path.</summary>
    public EventStore(string path) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Store path cannot be empty.", nameof(path));
        }
        Path = System.IO.Path.GetFullPath(path);
    }

    /// <summary>Absolute SQLite database path.</summary>
    public string Path { get; }

    /// <summary>Creates or validates the current storage schema.</summary>
    public void Initialize() {
        lock (_initializationLock) {
            if (_initialized) {
                return;
            }
            string? directory = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrWhiteSpace(directory)) {
                Directory.CreateDirectory(directory!);
            }
            using var sqlite = new SQLite { BusyTimeoutMs = 10000 };
            using SQLiteSession session = sqlite.OpenSession(Path);
            object? metadataTable = session.ExecuteScalar(
                "SELECT 1 FROM sqlite_master " +
                "WHERE type = 'table' AND name = 'evx_store_metadata' LIMIT 1;");
            if (metadataTable != null) {
                ValidateSchemaVersion(session.ExecuteScalar(
                    "SELECT schema_version FROM evx_store_metadata WHERE singleton_id = 1;"));
            }
            session.ExecuteNonQuery("PRAGMA journal_mode=WAL;");
            session.ExecuteNonQuery("PRAGMA synchronous=NORMAL;");
            session.ExecuteNonQuery(SchemaSql);
            ValidateSchemaVersion(session.ExecuteScalar(
                "SELECT schema_version FROM evx_store_metadata WHERE singleton_id = 1;"));
            EnsureEventIdentitySchema(session);
            EnsureCheckpointIdentitySchema(session);
            _initialized = true;
        }
    }

    private static void ValidateSchemaVersion(object? version) {
        if (version == null || version == DBNull.Value ||
            Convert.ToInt32(version, CultureInfo.InvariantCulture) != SchemaVersion) {
            throw new InvalidDataException(
                $"Event store schema version '{version ?? "missing"}' is not supported by this EventViewerX build.");
        }
    }

    private void EnsureInitialized() {
        if (!_initialized) {
            Initialize();
        }
    }

    private static JsonSerializerOptions CreateJsonOptions() {
        var options = new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new IPAddressJsonConverter());
        return options;
    }

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static void EnsureEventIdentitySchema(SQLiteSession session) {
        session.RunInTransaction(transaction => {
            // Acquire the database writer reservation before inspecting the legacy schema. Separate
            // EventStore instances and processes must not act on the same stale column snapshot.
            transaction.ExecuteNonQuery(
                "UPDATE evx_store_metadata SET schema_version = schema_version WHERE singleton_id = 1;");
            IReadOnlyList<string> columns = transaction.QueryAsList(
                "PRAGMA table_info(evx_events);",
                static record => record.GetString(1));
            bool originalKeyMissing = !columns.Contains("original_event_key", StringComparer.OrdinalIgnoreCase);
            bool transportKindMissing = !columns.Contains("transport_kind", StringComparer.OrdinalIgnoreCase);
            if (originalKeyMissing || transportKindMissing) {
                if (originalKeyMissing) {
                    transaction.ExecuteNonQuery(
                        "ALTER TABLE evx_events ADD COLUMN original_event_key TEXT NOT NULL DEFAULT ''; ");
                }
                if (transportKindMissing) {
                    transaction.ExecuteNonQuery(
                        "ALTER TABLE evx_events ADD COLUMN transport_kind INTEGER NOT NULL DEFAULT 2;");
                }
                MigrateEventIdentity(transaction);
            }
        });
        session.ExecuteNonQuery(
            "CREATE INDEX IF NOT EXISTS ix_evx_events_original_transport " +
            "ON evx_events (original_event_key, transport_kind);");
    }

    private static void MigrateEventIdentity(SQLiteSession session) {
        IReadOnlyList<LegacyIdentityRow> rows = session.QueryAsList(
            @"SELECT event_key, definition_name, event_time_utc, event_id, record_id, provider,
                     source_log, container_log, source_computer, collector_computer, values_json
              FROM evx_events;",
            static record => new LegacyIdentityRow(
                record.GetString(0),
                record.GetString(1),
                record.GetString(2),
                record.GetInt32(3),
                record.IsDBNull(4) ? null : record.GetInt64(4),
                record.GetString(5),
                record.GetString(6),
                record.GetString(7),
                record.GetString(8),
                record.GetString(9),
                JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(record.GetString(10), JsonOptions) ??
                    new Dictionary<string, JsonElement>()));
        foreach (LegacyIdentityRow row in rows) {
            var candidate = new StoredIdentityCandidate(
                row.TimeCreatedUtc,
                row.EventId,
                row.Provider,
                row.SourceLog,
                row.ContainerLog,
                row.SourceComputer,
                row.CollectorComputer,
                row.Values);
            string originalKey = CreateOriginalEventKey(candidate, row.DefinitionName);
            EventTransportKind transport = GetTransportKind(
                EventLogQuerySourceKind.Auto,
                row.SourceLog,
                row.ContainerLog);
            session.ExecuteNonQuery(
                @"UPDATE evx_events
                  SET event_key = $newKey,
                      original_event_key = $originalKey,
                      transport_kind = $transportKind
                  WHERE event_key = $oldKey;",
                new Dictionary<string, object?> {
                    ["$newKey"] = CreateEventKey(candidate, row.DefinitionName, row.RecordId),
                    ["$originalKey"] = originalKey,
                    ["$transportKind"] = (int)transport,
                    ["$oldKey"] = row.EventKey
                });
        }
    }

    private sealed class IPAddressJsonConverter : JsonConverter<IPAddress> {
        public override IPAddress Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            string? value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value) || !IPAddress.TryParse(value, out IPAddress? address)) {
                throw new JsonException($"'{value}' is not a valid IP address.");
            }
            return address;
        }

        public override void Write(Utf8JsonWriter writer, IPAddress value, JsonSerializerOptions options) {
            writer.WriteStringValue(value.ToString());
        }
    }

    private const string SchemaSql = @"
CREATE TABLE IF NOT EXISTS evx_store_metadata (
    singleton_id INTEGER NOT NULL PRIMARY KEY CHECK (singleton_id = 1),
    schema_version INTEGER NOT NULL,
    created_utc TEXT NOT NULL
);
INSERT OR IGNORE INTO evx_store_metadata (singleton_id, schema_version, created_utc)
VALUES (1, 1, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));

CREATE TABLE IF NOT EXISTS evx_definitions (
    definition_name TEXT NOT NULL PRIMARY KEY COLLATE NOCASE,
    display_name TEXT NOT NULL,
    description TEXT NOT NULL,
    kind INTEGER NOT NULL,
    schema_hash TEXT NOT NULL,
    schema_json TEXT NOT NULL,
    updated_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS evx_events (
    event_key TEXT NOT NULL PRIMARY KEY,
    original_event_key TEXT NOT NULL,
    transport_kind INTEGER NOT NULL,
    definition_name TEXT NOT NULL COLLATE NOCASE,
    event_time_utc TEXT NOT NULL,
    event_id INTEGER NOT NULL,
    record_id INTEGER NULL,
    provider TEXT NOT NULL,
    source_log TEXT NOT NULL,
    container_log TEXT NOT NULL,
    source_computer TEXT NOT NULL,
    collector_computer TEXT NOT NULL,
    level TEXT NOT NULL,
    level_value INTEGER NULL,
    message TEXT NOT NULL,
    values_json TEXT NOT NULL,
    inserted_utc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_evx_events_time ON evx_events (event_time_utc);
CREATE INDEX IF NOT EXISTS ix_evx_events_definition_time ON evx_events (definition_name, event_time_utc);
CREATE INDEX IF NOT EXISTS ix_evx_events_source_time ON evx_events (source_computer, source_log, event_time_utc);
CREATE INDEX IF NOT EXISTS ix_evx_events_source_nocase_time ON evx_events (
    source_computer COLLATE NOCASE,
    source_log COLLATE NOCASE,
    event_time_utc
);
CREATE INDEX IF NOT EXISTS ix_evx_events_provider_nocase_time ON evx_events (
    provider COLLATE NOCASE,
    event_time_utc
);
CREATE INDEX IF NOT EXISTS ix_evx_events_event_id_time ON evx_events (event_id, event_time_utc);

CREATE TABLE IF NOT EXISTS evx_checkpoints (
    consumer TEXT NOT NULL COLLATE NOCASE,
    computer TEXT NOT NULL COLLATE NOCASE,
    container TEXT NOT NULL COLLATE NOCASE,
    record_id INTEGER NULL,
    bookmark_xml TEXT NULL,
    updated_utc TEXT NOT NULL,
    PRIMARY KEY (consumer, computer, container)
);";

    private sealed class LegacyIdentityRow {
        internal LegacyIdentityRow(
            string eventKey,
            string definitionName,
            string timeCreatedUtc,
            int eventId,
            long? recordId,
            string provider,
            string sourceLog,
            string containerLog,
            string sourceComputer,
            string collectorComputer,
            IReadOnlyDictionary<string, JsonElement> values) {

            EventKey = eventKey;
            DefinitionName = definitionName;
            TimeCreatedUtc = timeCreatedUtc;
            EventId = eventId;
            RecordId = recordId;
            Provider = provider;
            SourceLog = sourceLog;
            ContainerLog = containerLog;
            SourceComputer = sourceComputer;
            CollectorComputer = collectorComputer;
            Values = values;
        }

        internal string EventKey { get; }
        internal string DefinitionName { get; }
        internal string TimeCreatedUtc { get; }
        internal int EventId { get; }
        internal long? RecordId { get; }
        internal string Provider { get; }
        internal string SourceLog { get; }
        internal string ContainerLog { get; }
        internal string SourceComputer { get; }
        internal string CollectorComputer { get; }
        internal IReadOnlyDictionary<string, JsonElement> Values { get; }
    }
}
