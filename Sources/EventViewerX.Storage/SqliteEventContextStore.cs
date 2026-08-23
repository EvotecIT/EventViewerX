using DBAClientX;

namespace EventViewerX.Storage;

/// <summary>Durable DbaClientX-backed context store that can share an EventViewerX SQLite database.</summary>
public sealed class SqliteEventContextStore : IEventContextStore {
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
            object? metadataTable = session.ExecuteScalar(
                "SELECT 1 FROM sqlite_master " +
                "WHERE type = 'table' AND name = 'evx_context_metadata' LIMIT 1;");
            if (metadataTable != null) {
                ValidateSchemaVersion(session.ExecuteScalar(
                    "SELECT schema_version FROM evx_context_metadata WHERE singleton_id = 1;"));
            }
            session.ExecuteNonQuery("PRAGMA journal_mode=WAL;");
            session.ExecuteNonQuery("PRAGMA synchronous=NORMAL;");
            session.ExecuteNonQuery(SchemaSql);
            ValidateSchemaVersion(session.ExecuteScalar(
                "SELECT schema_version FROM evx_context_metadata WHERE singleton_id = 1;"));
            _initialized = true;
        }
    }

    private static void ValidateSchemaVersion(object? version) {
        if (version == null || version == DBNull.Value ||
            Convert.ToInt32(version, CultureInfo.InvariantCulture) != 1) {
            throw new InvalidDataException(
                $"Context schema version '{version ?? "missing"}' is not supported by this EventViewerX build.");
        }
    }

    /// <inheritdoc />
    public async ValueTask StoreAsync(
        EventContextFact fact,
        CancellationToken cancellationToken = default) {

        EventContextFact snapshot = EventContextResolver.ValidateAndSnapshot(fact);
        string factKey = EventContextIdentity.CreateFactKey(snapshot);
        EnsureInitialized();
        using var sqlite = new SQLite { BusyTimeoutMs = 10000 };
        await using SQLiteAsyncSession session = await sqlite
            .OpenSessionAsync(Path, cancellationToken)
            .ConfigureAwait(false);
        await session.RunInTransactionAsync(async (transaction, token) => {
            await transaction.ExecuteNonQueryAsync(
                ReserveWriterSql,
                cancellationToken: token).ConfigureAwait(false);
            await transaction.ExecuteNonQueryAsync(
                InsertFactSql,
                CreateParameters(factKey, snapshot),
                token).ConfigureAwait(false);
            foreach (string alias in snapshot.Aliases) {
                await transaction.ExecuteNonQueryAsync(
                    InsertAliasSql,
                    new Dictionary<string, object?> {
                        ["$factKey"] = factKey,
                        ["$alias"] = alias
                    },
                    token).ConfigureAwait(false);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<EventContextResolution> ResolveAsync(
        EventContextQuery query,
        CancellationToken cancellationToken = default) {

        EventContextQuery request = EventContextResolver.ValidateAndSnapshot(query);
        EnsureInitialized();
        using var sqlite = new SQLite { BusyTimeoutMs = 10000 };
        await using SQLiteAsyncSession session = await sqlite
            .OpenSessionAsync(Path, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<EventContextFact> facts = await session.QueryAsListAsync(
            SelectFactsSql,
            MapFact,
            new Dictionary<string, object?> {
                ["$objectKind"] = (int)request.ObjectKind,
                ["$canonicalId"] = request.CanonicalId,
                ["$alias"] = request.Alias,
                ["$authorizationContext"] = request.AuthorizationContext
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return EventContextResolver.Resolve(facts, request);
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

    private static EventContextFact MapFact(System.Data.IDataRecord record) => new() {
        ObjectKind = (EventContextObjectKind)record.GetInt32(0),
        CanonicalId = record.GetString(1),
        Aliases = record.GetString(2)
            .Split(new[] { '\u001f' }, StringSplitOptions.RemoveEmptyEntries),
        DisplayName = record.IsDBNull(3) ? null : record.GetString(3),
        Domain = record.IsDBNull(4) ? null : record.GetString(4),
        DistinguishedName = record.IsDBNull(5) ? null : record.GetString(5),
        EffectiveAtUtc = ParseUtc(record.GetString(6)),
        ObservedAtUtc = ParseUtc(record.GetString(7)),
        IsDeleted = record.GetInt32(8) != 0,
        Provenance = (EventContextProvenance)record.GetInt32(9),
        SourceIdentity = record.GetString(10),
        ProviderName = record.GetString(11),
        ProviderSchemaVersion = record.GetInt32(12),
        ConfidenceReason = record.IsDBNull(13) ? null : record.GetString(13),
        AuthorizationContext = record.IsDBNull(14) ? null : record.GetString(14),
        IsShareable = record.GetInt32(15) != 0
    };

    private static DateTime ParseUtc(string value) => DateTime.Parse(
        value,
        CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind).ToUniversalTime();

    private const string ReserveWriterSql =
        "UPDATE evx_context_metadata SET schema_version = schema_version WHERE singleton_id = 1;";

    private const string InsertFactSql = @"
INSERT OR IGNORE INTO evx_context_facts
    (fact_key, object_kind, canonical_id, display_name, domain, distinguished_name,
     effective_utc, observed_utc, is_deleted, provenance, source_identity, provider_name,
     provider_schema_version, confidence_reason, authorization_context, is_shareable)
VALUES
    ($factKey, $objectKind, $canonicalId, $displayName, $domain, $distinguishedName,
     $effectiveUtc, $observedUtc, $isDeleted, $provenance, $sourceIdentity, $providerName,
     $providerSchemaVersion, $confidenceReason, $authorizationContext, $isShareable);";

    private const string InsertAliasSql = @"
INSERT OR IGNORE INTO evx_context_aliases (fact_key, alias)
VALUES ($factKey, $alias);";

    private const string SelectFactsSql = @"
SELECT f.object_kind,
       f.canonical_id,
       COALESCE((SELECT group_concat(a.alias, char(31))
                 FROM evx_context_aliases a
                 WHERE a.fact_key = f.fact_key), ''),
       f.display_name,
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
       f.is_shareable
FROM evx_context_facts f
WHERE f.object_kind = $objectKind
  AND (f.is_shareable = 1 OR f.authorization_context = $authorizationContext)
  AND f.canonical_id IN (
      SELECT identity_fact.canonical_id
      FROM evx_context_facts identity_fact
      WHERE identity_fact.object_kind = $objectKind
        AND (identity_fact.is_shareable = 1 OR identity_fact.authorization_context = $authorizationContext)
        AND (identity_fact.canonical_id = $canonicalId OR EXISTS (
            SELECT 1 FROM evx_context_aliases match_alias
            WHERE match_alias.fact_key = identity_fact.fact_key AND match_alias.alias = $alias)))
ORDER BY f.effective_utc, f.source_identity;";

    private const string SchemaSql = @"
CREATE TABLE IF NOT EXISTS evx_context_metadata (
    singleton_id INTEGER NOT NULL PRIMARY KEY CHECK (singleton_id = 1),
    schema_version INTEGER NOT NULL,
    created_utc TEXT NOT NULL
);
INSERT OR IGNORE INTO evx_context_metadata (singleton_id, schema_version, created_utc)
VALUES (1, 1, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));

CREATE TABLE IF NOT EXISTS evx_context_facts (
    fact_key TEXT NOT NULL PRIMARY KEY,
    object_kind INTEGER NOT NULL,
    canonical_id TEXT NOT NULL,
    display_name TEXT NULL,
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
