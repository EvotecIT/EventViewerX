using DBAClientX;

namespace EventViewerX.Storage;

/// <summary>Durable DbaClientX-backed context store that can share an EventViewerX SQLite database.</summary>
public sealed class SqliteEventContextStore : IEventContextStore {
    private const int CurrentSchemaVersion = 3;
    private const int BatchQuerySize = 100;
    private readonly object _initializationLock = new();
    private bool _initialized;

    /// <summary>Creates a durable context store for one SQLite path.</summary>
    public SqliteEventContextStore(string path) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Store path cannot be empty.", nameof(path));
        }
        Path = System.IO.Path.GetFullPath(path);
    }

    /// <summary>Absolute SQLite database path.</summary>
    public string Path { get; }

    /// <summary>Creates the context schema without changing existing event-store tables.</summary>
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
            session.ExecuteNonQuery("PRAGMA journal_mode=WAL;");
            session.ExecuteNonQuery("PRAGMA synchronous=NORMAL;");
            session.ExecuteNonQuery(MetadataSchemaSql);
            session.RunInTransaction(transaction => {
                transaction.ExecuteNonQuery(ReserveWriterSql);
                int version = ReadSchemaVersion(transaction.ExecuteScalar(
                    "SELECT schema_version FROM evx_context_metadata WHERE singleton_id = 1;"));
                if (version == 1) {
                    MigrateSchemaV1ToV2(transaction);
                    version = ReadSchemaVersion(transaction.ExecuteScalar(
                        "SELECT schema_version FROM evx_context_metadata WHERE singleton_id = 1;"));
                }
                if (version == 2) {
                    MigrateSchemaV2ToV3(transaction);
                    version = ReadSchemaVersion(transaction.ExecuteScalar(
                        "SELECT schema_version FROM evx_context_metadata WHERE singleton_id = 1;"));
                }
                if (version != CurrentSchemaVersion) {
                    throw new InvalidDataException(
                        $"Context schema version '{version}' is not supported by this EventViewerX build.");
                }
                transaction.ExecuteNonQuery(SchemaSql);
            });
            _initialized = true;
        }
    }

    private static int ReadSchemaVersion(object? version) {
        if (version == null || version == DBNull.Value) {
            throw new InvalidDataException(
                $"Context schema version '{version ?? "missing"}' is not supported by this EventViewerX build.");
        }
        int parsed = Convert.ToInt32(version, CultureInfo.InvariantCulture);
        if (parsed < 1 || parsed > CurrentSchemaVersion) {
            throw new InvalidDataException(
                $"Context schema version '{parsed}' is not supported by this EventViewerX build.");
        }
        return parsed;
    }

    private static void MigrateSchemaV1ToV2(SQLiteSession transaction) {
        transaction.ExecuteNonQuery(AddDisplayNameObservationSql);
        transaction.ExecuteNonQuery(CompleteV2MigrationSql);
    }

    private static void MigrateSchemaV2ToV3(SQLiteSession transaction) {
        IReadOnlyList<StoredFactRow> rows = transaction.QueryAsList(
            SelectFactsForMigrationSql,
            MapStoredFact);
        foreach (KeyValuePair<string, EventContextFact> migrating in MaterializeFacts(rows)) {
            string currentKey = EventContextIdentity.CreateFactKey(migrating.Value);
            if (string.Equals(currentKey, migrating.Key, StringComparison.Ordinal)) {
                continue;
            }
            transaction.ExecuteNonQuery(
                InsertFactSql,
                CreateParameters(currentKey, migrating.Value));
            foreach (string alias in migrating.Value.Aliases) {
                transaction.ExecuteNonQuery(
                    InsertAliasSql,
                    new Dictionary<string, object?> {
                        ["$factKey"] = currentKey,
                        ["$alias"] = alias
                    });
            }
            transaction.ExecuteNonQuery(
                DeleteAliasesSql,
                new Dictionary<string, object?> { ["$factKey"] = migrating.Key });
            transaction.ExecuteNonQuery(
                DeleteFactSql,
                new Dictionary<string, object?> { ["$factKey"] = migrating.Key });
        }
        transaction.ExecuteNonQuery(CompleteV3MigrationSql);
    }

    /// <inheritdoc />
    public async ValueTask StoreAsync(
        EventContextFact fact,
        CancellationToken cancellationToken = default) {

        await StoreManyAsync(new[] { fact }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask StoreManyAsync(
        IReadOnlyList<EventContextFact> facts,
        CancellationToken cancellationToken = default) {

        if (facts == null) {
            throw new ArgumentNullException(nameof(facts));
        }
        cancellationToken.ThrowIfCancellationRequested();
        KeyValuePair<string, EventContextFact>[] snapshots = facts
            .Select(EventContextResolver.ValidateAndSnapshot)
            .Select(static fact => new KeyValuePair<string, EventContextFact>(
                EventContextIdentity.CreateFactKey(fact),
                fact))
            .GroupBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToArray();
        if (snapshots.Length == 0) {
            return;
        }
        EnsureInitialized();
        using var sqlite = new SQLite { BusyTimeoutMs = 10000 };
        await using SQLiteAsyncSession session = await sqlite
            .OpenSessionAsync(Path, cancellationToken)
            .ConfigureAwait(false);
        await session.RunInTransactionAsync(async (transaction, token) => {
            await transaction.ExecuteNonQueryAsync(
                ReserveWriterSql,
                cancellationToken: token).ConfigureAwait(false);
            foreach (KeyValuePair<string, EventContextFact> pair in snapshots) {
                await transaction.ExecuteNonQueryAsync(
                    InsertFactSql,
                    CreateParameters(pair.Key, pair.Value),
                    token).ConfigureAwait(false);
                foreach (string alias in pair.Value.Aliases) {
                    await transaction.ExecuteNonQueryAsync(
                        InsertAliasSql,
                        new Dictionary<string, object?> {
                            ["$factKey"] = pair.Key,
                            ["$alias"] = alias
                        },
                        token).ConfigureAwait(false);
                }
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<EventContextResolution> ResolveAsync(
        EventContextQuery query,
        CancellationToken cancellationToken = default) {

        IReadOnlyList<EventContextResolution> resolutions = await ResolveManyAsync(
            new[] { query },
            cancellationToken).ConfigureAwait(false);
        return resolutions[0];
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<EventContextResolution>> ResolveManyAsync(
        IReadOnlyList<EventContextQuery> queries,
        CancellationToken cancellationToken = default) {

        if (queries == null) {
            throw new ArgumentNullException(nameof(queries));
        }
        cancellationToken.ThrowIfCancellationRequested();
        EventContextQuery[] requests = queries
            .Select(EventContextResolver.ValidateAndSnapshot)
            .ToArray();
        if (requests.Length == 0) {
            return Array.Empty<EventContextResolution>();
        }
        EnsureInitialized();
        using var sqlite = new SQLite { BusyTimeoutMs = 10000 };
        await using SQLiteAsyncSession session = await sqlite
            .OpenSessionAsync(Path, cancellationToken)
            .ConfigureAwait(false);
        EventContextQuery[] distinctRequests = requests
            .GroupBy(static request => (
                request.ObjectKind,
                request.CanonicalId,
                request.Alias,
                request.AuthorizationContext))
            .Select(static group => group.First())
            .ToArray();
        Dictionary<string, EventContextFact> facts = await session.RunInTransactionAsync(
            async (transaction, token) => {
                var snapshot = new Dictionary<string, EventContextFact>(StringComparer.Ordinal);
                for (int offset = 0; offset < distinctRequests.Length; offset += BatchQuerySize) {
                    EventContextQuery[] batch = distinctRequests
                        .Skip(offset)
                        .Take(BatchQuerySize)
                        .ToArray();
                    var parameters = new Dictionary<string, object?>();
                    string values = string.Join(",", Enumerable.Range(0, batch.Length).Select(index => {
                        parameters["$objectKind" + index] = (int)batch[index].ObjectKind;
                        parameters["$canonicalId" + index] = batch[index].CanonicalId;
                        parameters["$alias" + index] = batch[index].Alias;
                        parameters["$authorizationContext" + index] = batch[index].AuthorizationContext;
                        return $"($objectKind{index},$canonicalId{index},$alias{index},$authorizationContext{index})";
                    }));
                    IReadOnlyList<StoredFactRow> loaded = await transaction.QueryAsListAsync(
                        SelectFactsBatchPrefix + values + SelectFactsBatchSuffix,
                        MapStoredFact,
                        parameters,
                        cancellationToken: token).ConfigureAwait(false);
                    foreach (KeyValuePair<string, EventContextFact> fact in MaterializeFacts(loaded)) {
                        snapshot[fact.Key] = fact.Value;
                    }
                }
                return snapshot;
            },
            cancellationToken).ConfigureAwait(false);
        return EventContextResolver.ResolveMany(facts.Values, requests);
    }

    private void EnsureInitialized() {
        if (!_initialized) {
            Initialize();
        }
    }

    private static Dictionary<string, object?> CreateParameters(
        string factKey,
        EventContextFact fact) => new() {
        ["$factKey"] = factKey,
        ["$objectKind"] = (int)fact.ObjectKind,
        ["$canonicalId"] = fact.CanonicalId,
        ["$displayName"] = fact.DisplayName,
        ["$displayNameObserved"] = fact.DisplayNameObserved ? 1 : 0,
        ["$domain"] = fact.Domain,
        ["$distinguishedName"] = fact.DistinguishedName,
        ["$effectiveUtc"] = fact.EffectiveAtUtc.ToString("O", CultureInfo.InvariantCulture),
        ["$observedUtc"] = fact.ObservedAtUtc.ToString("O", CultureInfo.InvariantCulture),
        ["$isDeleted"] = fact.IsDeleted ? 1 : 0,
        ["$provenance"] = (int)fact.Provenance,
        ["$sourceIdentity"] = fact.SourceIdentity,
        ["$providerName"] = fact.ProviderName,
        ["$providerSchemaVersion"] = fact.ProviderSchemaVersion,
        ["$confidenceReason"] = fact.ConfidenceReason,
        ["$authorizationContext"] = fact.AuthorizationContext,
        ["$isShareable"] = fact.IsShareable ? 1 : 0
    };

    private static StoredFactRow MapStoredFact(System.Data.IDataRecord record) => new(
        record.GetString(0),
        new EventContextFact {
            ObjectKind = (EventContextObjectKind)record.GetInt32(1),
            CanonicalId = record.GetString(2),
            DisplayName = record.IsDBNull(3) ? null : record.GetString(3),
            DisplayNameObserved = record.GetInt32(4) != 0,
            Domain = record.IsDBNull(5) ? null : record.GetString(5),
            DistinguishedName = record.IsDBNull(6) ? null : record.GetString(6),
            EffectiveAtUtc = ParseUtc(record.GetString(7)),
            ObservedAtUtc = ParseUtc(record.GetString(8)),
            IsDeleted = record.GetInt32(9) != 0,
            Provenance = (EventContextProvenance)record.GetInt32(10),
            SourceIdentity = record.GetString(11),
            ProviderName = record.GetString(12),
            ProviderSchemaVersion = record.GetInt32(13),
            ConfidenceReason = record.IsDBNull(14) ? null : record.GetString(14),
            AuthorizationContext = record.IsDBNull(15) ? null : record.GetString(15),
            IsShareable = record.GetInt32(16) != 0
        },
        record.IsDBNull(17) ? null : record.GetString(17));

    private static IReadOnlyList<KeyValuePair<string, EventContextFact>> MaterializeFacts(
        IEnumerable<StoredFactRow> rows) => rows
        .GroupBy(static row => row.FactKey, StringComparer.Ordinal)
        .Select(static group => {
            StoredFactRow first = group.First();
            first.Fact.Aliases = group
                .Select(static row => row.Alias)
                .Where(static alias => alias != null)
                .Select(static alias => alias!)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static alias => alias, StringComparer.Ordinal)
                .ToArray();
            return new KeyValuePair<string, EventContextFact>(first.FactKey, first.Fact);
        })
        .ToArray();

    private sealed class StoredFactRow {
        internal StoredFactRow(string factKey, EventContextFact fact, string? alias) {
            FactKey = factKey;
            Fact = fact;
            Alias = alias;
        }

        internal string FactKey { get; }

        internal EventContextFact Fact { get; }

        internal string? Alias { get; }
    }

    private static DateTime ParseUtc(string value) => DateTime.Parse(
        value,
        CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind).ToUniversalTime();

    private const string ReserveWriterSql =
        "UPDATE evx_context_metadata SET schema_version = schema_version WHERE singleton_id = 1;";

    private const string InsertFactSql = @"
INSERT OR IGNORE INTO evx_context_facts
    (fact_key, object_kind, canonical_id, display_name, display_name_observed, domain, distinguished_name,
     effective_utc, observed_utc, is_deleted, provenance, source_identity, provider_name,
     provider_schema_version, confidence_reason, authorization_context, is_shareable)
VALUES
    ($factKey, $objectKind, $canonicalId, $displayName, $displayNameObserved, $domain, $distinguishedName,
     $effectiveUtc, $observedUtc, $isDeleted, $provenance, $sourceIdentity, $providerName,
     $providerSchemaVersion, $confidenceReason, $authorizationContext, $isShareable);";

    private const string InsertAliasSql = @"
INSERT OR IGNORE INTO evx_context_aliases (fact_key, alias)
VALUES ($factKey, $alias);";

    private const string DeleteAliasesSql = @"
DELETE FROM evx_context_aliases
WHERE fact_key = $factKey;";

    private const string DeleteFactSql = @"
DELETE FROM evx_context_facts
WHERE fact_key = $factKey;";

    private const string SelectFactsBatchPrefix = @"
WITH requested(object_kind, canonical_id, alias, authorization_context) AS (VALUES ";

    private const string SelectFactsBatchSuffix = @")
SELECT f.fact_key,
       f.object_kind,
       f.canonical_id,
       f.display_name,
       f.display_name_observed,
       f.domain,
       f.distinguished_name,
       f.effective_utc,
       f.observed_utc,
       f.is_deleted,
       f.provenance,
       f.source_identity,
       f.provider_name,
       f.provider_schema_version,
       f.confidence_reason,
       f.authorization_context,
       f.is_shareable,
       alias.alias
FROM evx_context_facts f
LEFT JOIN evx_context_aliases alias ON alias.fact_key = f.fact_key
WHERE EXISTS (
    SELECT 1
    FROM requested request
    WHERE request.object_kind = f.object_kind
      AND (f.is_shareable = 1 OR f.authorization_context = request.authorization_context)
      AND EXISTS (
          SELECT 1
          FROM evx_context_facts identity_fact
          WHERE identity_fact.object_kind = request.object_kind
            AND identity_fact.canonical_id = f.canonical_id
            AND (identity_fact.is_shareable = 1 OR
                 identity_fact.authorization_context = request.authorization_context)
            AND (identity_fact.canonical_id = request.canonical_id OR EXISTS (
                SELECT 1
                FROM evx_context_aliases match_alias
                WHERE match_alias.fact_key = identity_fact.fact_key
                  AND match_alias.alias = request.alias))))
ORDER BY f.fact_key, alias.alias;";

    private const string AddDisplayNameObservationSql = @"
ALTER TABLE evx_context_facts
ADD COLUMN display_name_observed INTEGER NOT NULL DEFAULT 0;
UPDATE evx_context_facts
SET display_name_observed = 1
WHERE display_name IS NOT NULL;";

    private const string SelectFactsForMigrationSql = @"
SELECT f.fact_key,
       f.object_kind,
       f.canonical_id,
       f.display_name,
       f.display_name_observed,
       f.domain,
       f.distinguished_name,
       f.effective_utc,
       f.observed_utc,
       f.is_deleted,
       f.provenance,
       f.source_identity,
       f.provider_name,
       f.provider_schema_version,
       f.confidence_reason,
       f.authorization_context,
       f.is_shareable,
       alias.alias
FROM evx_context_facts f
LEFT JOIN evx_context_aliases alias ON alias.fact_key = f.fact_key
ORDER BY f.fact_key, alias.alias;";

    private const string CompleteV2MigrationSql = @"
UPDATE evx_context_metadata
SET schema_version = 2
WHERE singleton_id = 1;";

    private const string CompleteV3MigrationSql = @"
UPDATE evx_context_metadata
SET schema_version = 3
WHERE singleton_id = 1;";

    private const string MetadataSchemaSql = @"
CREATE TABLE IF NOT EXISTS evx_context_metadata (
    singleton_id INTEGER NOT NULL PRIMARY KEY CHECK (singleton_id = 1),
    schema_version INTEGER NOT NULL,
    created_utc TEXT NOT NULL
);
INSERT OR IGNORE INTO evx_context_metadata (singleton_id, schema_version, created_utc)
VALUES (1, 3, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));";

    private const string SchemaSql = @"
CREATE TABLE IF NOT EXISTS evx_context_facts (
    fact_key TEXT NOT NULL PRIMARY KEY,
    object_kind INTEGER NOT NULL,
    canonical_id TEXT NOT NULL,
    display_name TEXT NULL,
    display_name_observed INTEGER NOT NULL,
    domain TEXT NULL,
    distinguished_name TEXT NULL,
    effective_utc TEXT NOT NULL,
    observed_utc TEXT NOT NULL,
    is_deleted INTEGER NOT NULL,
    provenance INTEGER NOT NULL,
    source_identity TEXT NOT NULL,
    provider_name TEXT NOT NULL,
    provider_schema_version INTEGER NOT NULL,
    confidence_reason TEXT NULL,
    authorization_context TEXT NULL,
    is_shareable INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_evx_context_object_time
ON evx_context_facts (object_kind, canonical_id, effective_utc);

CREATE TABLE IF NOT EXISTS evx_context_aliases (
    fact_key TEXT NOT NULL,
    alias TEXT NOT NULL,
    PRIMARY KEY (fact_key, alias),
    FOREIGN KEY (fact_key) REFERENCES evx_context_facts (fact_key) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ix_evx_context_alias
ON evx_context_aliases (alias, fact_key);";
}
