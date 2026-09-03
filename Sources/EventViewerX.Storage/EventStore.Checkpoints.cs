using System.Globalization;
using DBAClientX;

namespace EventViewerX.Storage;

public sealed partial class EventStore {
    /// <summary>
    /// Advances one durable watcher checkpoint only when its current value still matches the caller's snapshot.
    /// </summary>
    public async Task<EventStoreCheckpoint> AdvanceCheckpointAsync(
        EventStoreCheckpoint checkpoint,
        EventStoreCheckpoint? expectedCheckpoint,
        CancellationToken cancellationToken = default) {

        EventStoreCheckpoint requested = SnapshotCheckpoint(checkpoint) ??
            throw new ArgumentNullException(nameof(checkpoint));
        EventStoreCheckpoint? expected = SnapshotCheckpoint(expectedCheckpoint);
        EnsureInitialized();
        using var sqlite = new SQLite { BusyTimeoutMs = 10000 };
        await using SQLiteAsyncSession session = await sqlite
            .OpenSessionAsync(Path, cancellationToken)
            .ConfigureAwait(false);
        return await session.RunInTransactionAsync(async (transaction, token) => {
            EventStoreCheckpoint canonical = await ResolveCheckpointIdentityAsync(
                transaction,
                requested,
                expected,
                compareExpected: true,
                token).ConfigureAwait(false);
            string updatedAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            await transaction.ExecuteNonQueryAsync(
                UpsertCheckpointSql,
                new Dictionary<string, object?> {
                    ["$consumer"] = canonical.Consumer,
                    ["$computer"] = canonical.Computer,
                    ["$container"] = canonical.Container,
                    ["$recordId"] = canonical.RecordId,
                    ["$bookmark"] = canonical.BookmarkXml,
                    ["$updated"] = updatedAt
                },
                token).ConfigureAwait(false);
            canonical.UpdatedAtUtc = DateTime.Parse(
                updatedAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
            return canonical;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Atomically migrates legacy checkpoint containers into their current identities and retires
    /// each legacy identity after every replacement that references it is durable.
    /// </summary>
    /// <param name="consumer">Stable checkpoint consumer.</param>
    /// <param name="computer">Source or collector computer.</param>
    /// <param name="legacyContainersByCurrent">
    /// Current checkpoint container identities mapped to older identities in migration priority order.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task MigrateCheckpointContainersAsync(
        string consumer,
        string computer,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> legacyContainersByCurrent,
        CancellationToken cancellationToken = default) {

        if (string.IsNullOrWhiteSpace(consumer) ||
            string.IsNullOrWhiteSpace(computer)) {
            throw new ArgumentException(
                "Consumer and computer are required.");
        }
        if (legacyContainersByCurrent == null) {
            throw new ArgumentNullException(nameof(legacyContainersByCurrent));
        }

        string normalizedConsumer = consumer.Trim();
        string normalizedComputer = computer.Trim();
        var migrations = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, IReadOnlyCollection<string>> pair in legacyContainersByCurrent) {
            string current = pair.Key?.Trim() ?? string.Empty;
            if (current.Length == 0) {
                throw new ArgumentException(
                    "Current checkpoint containers cannot contain empty values.",
                    nameof(legacyContainersByCurrent));
            }
            if (pair.Value == null) {
                throw new ArgumentException(
                    "Legacy checkpoint container collections cannot be null.",
                    nameof(legacyContainersByCurrent));
            }
            string[] legacyContainers = pair.Value
                .Select(static legacy => legacy?.Trim() ?? string.Empty)
                .ToArray();
            if (legacyContainers.Any(static legacy => legacy.Length == 0)) {
                throw new ArgumentException(
                    "Legacy checkpoint containers cannot contain empty values.",
                    nameof(legacyContainersByCurrent));
            }
            legacyContainers = legacyContainers
                .Where(legacy => !string.Equals(legacy, current, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (migrations.ContainsKey(current)) {
                throw new ArgumentException(
                    $"Current checkpoint container '{current}' is specified more than once.",
                    nameof(legacyContainersByCurrent));
            }
            migrations.Add(current, legacyContainers);
        }
        if (migrations.Count == 0) {
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
            IReadOnlyList<StoredCheckpointRow> rows = await transaction.QueryAsListAsync(
                SelectStoredCheckpointsSql,
                MapStoredCheckpoint,
                cancellationToken: token).ConfigureAwait(false);
            var readyContainers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string[]> migration in migrations) {
                bool currentExists = rows.Any(row => MatchesCheckpointIdentity(
                    row,
                    normalizedConsumer,
                    normalizedComputer,
                    migration.Key));
                if (currentExists) {
                    readyContainers.Add(migration.Key);
                    continue;
                }

                StoredCheckpointRow? legacyValue = null;
                foreach (string legacyContainer in migration.Value) {
                    legacyValue = rows
                        .Where(row => MatchesCheckpointIdentity(
                            row,
                            normalizedConsumer,
                            normalizedComputer,
                            legacyContainer))
                        .OrderByDescending(static row => row.UpdatedUtc, StringComparer.Ordinal)
                        .ThenByDescending(static row => row.RecordId ?? long.MinValue)
                        .FirstOrDefault();
                    if (legacyValue != null) {
                        break;
                    }
                }
                if (legacyValue == null) {
                    continue;
                }
                var migratedIdentity = new StoredCheckpointRow(
                    0,
                    normalizedConsumer,
                    normalizedComputer,
                    migration.Key,
                    legacyValue.RecordId,
                    legacyValue.BookmarkXml,
                    legacyValue.UpdatedUtc);
                await transaction.ExecuteNonQueryAsync(
                    InsertCheckpointSql,
                    CreateCheckpointParameters(migratedIdentity, legacyValue),
                    token).ConfigureAwait(false);
                readyContainers.Add(migration.Key);
            }

            string[] currentContainers = migrations.Keys.ToArray();
            string[] retiredLegacyContainers = migrations
                .SelectMany(static migration => migration.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(legacy => !currentContainers.Contains(legacy, StringComparer.OrdinalIgnoreCase))
                .Where(legacy => migrations
                    .Where(migration => migration.Value.Contains(legacy, StringComparer.OrdinalIgnoreCase))
                    .All(migration => readyContainers.Contains(migration.Key)))
                .ToArray();
            foreach (StoredCheckpointRow legacy in rows.Where(row =>
                         string.Equals(row.Consumer, normalizedConsumer, StringComparison.OrdinalIgnoreCase) &&
                         string.Equals(row.Computer, normalizedComputer, StringComparison.OrdinalIgnoreCase) &&
                         retiredLegacyContainers.Contains(row.Container, StringComparer.OrdinalIgnoreCase))) {
                await transaction.ExecuteNonQueryAsync(
                    "DELETE FROM evx_checkpoints WHERE rowid = $rowId;",
                    new Dictionary<string, object?> { ["$rowId"] = legacy.RowId },
                    token).ConfigureAwait(false);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Deletes one durable consumer checkpoint without removing stored events.</summary>
    public async Task<bool> DeleteCheckpointAsync(
        string consumer,
        string computer,
        string container,
        CancellationToken cancellationToken = default) {

        if (string.IsNullOrWhiteSpace(consumer) ||
            string.IsNullOrWhiteSpace(computer) ||
            string.IsNullOrWhiteSpace(container)) {
            throw new ArgumentException(
                "Consumer, computer, and container are required.");
        }
        EnsureInitialized();
        using var sqlite = new SQLite { BusyTimeoutMs = 10000 };
        await using SQLiteAsyncSession session = await sqlite
            .OpenSessionAsync(Path, cancellationToken)
            .ConfigureAwait(false);
        return await session.RunInTransactionAsync(async (transaction, token) => {
            await transaction.ExecuteNonQueryAsync(
                ReserveWriterSql,
                cancellationToken: token).ConfigureAwait(false);
            IReadOnlyList<StoredCheckpointRow> rows = await transaction.QueryAsListAsync(
                SelectStoredCheckpointsSql,
                MapStoredCheckpoint,
                cancellationToken: token).ConfigureAwait(false);
            StoredCheckpointRow[] matches = rows.Where(row => MatchesCheckpointIdentity(
                row,
                consumer.Trim(),
                computer.Trim(),
                container.Trim())).ToArray();
            foreach (StoredCheckpointRow match in matches) {
                await transaction.ExecuteNonQueryAsync(
                    "DELETE FROM evx_checkpoints WHERE rowid = $rowId;",
                    new Dictionary<string, object?> { ["$rowId"] = match.RowId },
                    token).ConfigureAwait(false);
            }
            return matches.Length > 0;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static EventStoreCheckpoint? SnapshotCheckpoint(EventStoreCheckpoint? checkpoint) {
        ValidateCheckpoint(checkpoint);
        return checkpoint == null
            ? null
            : new EventStoreCheckpoint {
                Consumer = checkpoint.Consumer.Trim(),
                Computer = checkpoint.Computer.Trim(),
                Container = checkpoint.Container.Trim(),
                RecordId = checkpoint.RecordId,
                BookmarkXml = checkpoint.BookmarkXml,
                UpdatedAtUtc = checkpoint.UpdatedAtUtc
            };
    }

    private static void EnsureCheckpointIdentitySchema(SQLiteSession session) {
        session.RunInTransaction(transaction => {
            transaction.ExecuteNonQuery(ReserveWriterSql);
            IReadOnlyList<StoredCheckpointRow> rows = transaction.QueryAsList(
                SelectStoredCheckpointsSql,
                MapStoredCheckpoint);
            foreach (IGrouping<StoredCheckpointRow, StoredCheckpointRow> group in rows
                         .GroupBy(static row => row, StoredCheckpointIdentityComparer.Instance)
                         .Where(static group => group.Skip(1).Any())) {
                StoredCheckpointRow identity = group.OrderBy(static row => row.RowId).First();
                StoredCheckpointRow value = group
                    .OrderByDescending(static row => row.UpdatedUtc, StringComparer.Ordinal)
                    .ThenByDescending(static row => row.RecordId ?? long.MinValue)
                    .First();
                foreach (StoredCheckpointRow duplicate in group) {
                    transaction.ExecuteNonQuery(
                        "DELETE FROM evx_checkpoints WHERE rowid = $rowId;",
                        new Dictionary<string, object?> { ["$rowId"] = duplicate.RowId });
                }
                transaction.ExecuteNonQuery(
                    InsertCheckpointSql,
                    CreateCheckpointParameters(identity, value));
            }
        });
    }

    private static async Task<EventStoreCheckpoint> ResolveCheckpointIdentityAsync(
        SQLiteAsyncSession session,
        EventStoreCheckpoint requested,
        EventStoreCheckpoint? expected,
        bool compareExpected,
        CancellationToken cancellationToken) {

        await session.ExecuteNonQueryAsync(
            ReserveWriterSql,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        IReadOnlyList<StoredCheckpointRow> rows = await session.QueryAsListAsync(
            SelectStoredCheckpointsSql,
            MapStoredCheckpoint,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        StoredCheckpointRow[] matches = rows.Where(row => MatchesCheckpointIdentity(
            row,
            requested.Consumer,
            requested.Computer,
            requested.Container)).OrderBy(static row => row.RowId).ToArray();
        if (compareExpected && !MatchesExpectedCheckpoint(matches, expected)) {
            throw new InvalidOperationException(
                $"Checkpoint '{requested.Consumer}' for {requested.Computer}/{requested.Container} changed after collection started; no events or checkpoint were committed.");
        }
        if (matches.Length == 0) {
            return requested;
        }
        StoredCheckpointRow identity = matches[0];
        foreach (StoredCheckpointRow duplicate in matches.Skip(1)) {
            await session.ExecuteNonQueryAsync(
                "DELETE FROM evx_checkpoints WHERE rowid = $rowId;",
                new Dictionary<string, object?> { ["$rowId"] = duplicate.RowId },
                cancellationToken).ConfigureAwait(false);
        }
        return new EventStoreCheckpoint {
            Consumer = identity.Consumer,
            Computer = identity.Computer,
            Container = identity.Container,
            RecordId = requested.RecordId,
            BookmarkXml = requested.BookmarkXml,
            UpdatedAtUtc = requested.UpdatedAtUtc
        };
    }

    private static bool MatchesExpectedCheckpoint(
        IReadOnlyList<StoredCheckpointRow> current,
        EventStoreCheckpoint? expected) {

        if (expected == null) {
            return current.Count == 0;
        }
        if (current.Count != 1) {
            return false;
        }
        StoredCheckpointRow value = current[0];
        return MatchesCheckpointIdentity(
                   value,
                   expected.Consumer,
                   expected.Computer,
                   expected.Container) &&
               value.RecordId == expected.RecordId &&
               string.Equals(
                   value.BookmarkXml,
                   expected.BookmarkXml,
                   StringComparison.Ordinal) &&
               DateTime.Parse(
                   value.UpdatedUtc,
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.RoundtripKind).ToUniversalTime() ==
               expected.UpdatedAtUtc.ToUniversalTime();
    }

    private static StoredCheckpointRow MapStoredCheckpoint(System.Data.IDataRecord record) => new(
        record.GetInt64(0),
        record.GetString(1),
        record.GetString(2),
        record.GetString(3),
        record.IsDBNull(4) ? null : record.GetInt64(4),
        record.IsDBNull(5) ? null : record.GetString(5),
        record.GetString(6));

    private static bool MatchesCheckpointIdentity(
        StoredCheckpointRow row,
        string consumer,
        string computer,
        string container) =>
        string.Equals(row.Consumer, consumer, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(row.Computer, computer, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(row.Container, container, StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, object?> CreateCheckpointParameters(
        StoredCheckpointRow identity,
        StoredCheckpointRow value) => new() {
            ["$consumer"] = identity.Consumer,
            ["$computer"] = identity.Computer,
            ["$container"] = identity.Container,
            ["$recordId"] = value.RecordId,
            ["$bookmark"] = value.BookmarkXml,
            ["$updated"] = value.UpdatedUtc
        };

    private const string ReserveWriterSql =
        "UPDATE evx_store_metadata SET schema_version = schema_version WHERE singleton_id = 1;";

    private const string SelectStoredCheckpointsSql = @"
SELECT rowid, consumer, computer, container, record_id, bookmark_xml, updated_utc
FROM evx_checkpoints;";

    private const string InsertCheckpointSql = @"
INSERT INTO evx_checkpoints
    (consumer, computer, container, record_id, bookmark_xml, updated_utc)
VALUES ($consumer, $computer, $container, $recordId, $bookmark, $updated);";

    private sealed class StoredCheckpointRow {
        internal StoredCheckpointRow(
            long rowId,
            string consumer,
            string computer,
            string container,
            long? recordId,
            string? bookmarkXml,
            string updatedUtc) {

            RowId = rowId;
            Consumer = consumer;
            Computer = computer;
            Container = container;
            RecordId = recordId;
            BookmarkXml = bookmarkXml;
            UpdatedUtc = updatedUtc;
        }

        internal long RowId { get; }
        internal string Consumer { get; }
        internal string Computer { get; }
        internal string Container { get; }
        internal long? RecordId { get; }
        internal string? BookmarkXml { get; }
        internal string UpdatedUtc { get; }
    }

    private sealed class StoredCheckpointIdentityComparer : IEqualityComparer<StoredCheckpointRow> {
        internal static readonly StoredCheckpointIdentityComparer Instance = new();

        public bool Equals(StoredCheckpointRow? left, StoredCheckpointRow? right) =>
            ReferenceEquals(left, right) ||
            left != null && right != null &&
            MatchesCheckpointIdentity(left, right.Consumer, right.Computer, right.Container);

        public int GetHashCode(StoredCheckpointRow value) {
            unchecked {
                int hash = StringComparer.OrdinalIgnoreCase.GetHashCode(value.Consumer);
                hash = (hash * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(value.Computer);
                return (hash * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(value.Container);
            }
        }
    }
}
