using DBAClientX;
using EventViewerX.Reporting;

namespace EventViewerX.Storage;

public sealed partial class EventStore {
    /// <summary>
    /// Streams selected history in deterministic time and insertion order without retaining the result set.
    /// Schema and rows share a deferred read snapshot, so concurrent writers can continue.
    /// Keyset pages hold only a bounded number of rows on every supported target.
    /// The caller receives a completion summary only after every delivered row has been handled.
    /// </summary>
    public async Task<EventStoreRowReadResult> StreamRowsAsync(
        EventStoreQuery query,
        Func<EventReportRow, CancellationToken, Task> onRow,
        CancellationToken cancellationToken = default) {

        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        if (onRow == null) {
            throw new ArgumentNullException(nameof(onRow));
        }
        EnsureInitialized();
        EventStoreQuery snapshot = query.Snapshot();
        using var sqlite = new SQLite { BusyTimeoutMs = 10000 };
        await using SQLiteAsyncSession session = await sqlite
            .OpenSessionAsync(Path, cancellationToken)
            .ConfigureAwait(false);
        // SQLite's deferred BEGIN stays read-only and permits WAL writers while retaining
        // one schema/data snapshot. DbaClientX's regular transaction helper starts a write
        // transaction, which would block ingestion for the duration of a slow pipeline.
        await session.ExecuteNonQueryAsync(
            "BEGIN DEFERRED TRANSACTION;",
            cancellationToken: cancellationToken).ConfigureAwait(false);
        try {
            StoredSchemaContext schemaContext = await ReadSchemaContextAsync(
                session,
                snapshot.ResolveDefinitionNames(),
                snapshot.DefinitionSchemas,
                cancellationToken).ConfigureAwait(false);
            snapshot.Predicate = NormalizeStoredPredicate(snapshot.Predicate, schemaContext.Schemas);
            int candidateLimit = BuildReadCommand(snapshot, schemaContext.Pushdown).CandidateLimit;
            long scanned = 0;
            long delivered = 0;
            bool scanLimitReached = false;
            bool resultLimitReached = false;
            string? cursorTime = null;
            long? cursorRowId = null;
            bool continueReading = true;
            while (continueReading) {
                QueryCommand command = BuildReadCommand(
                    snapshot, schemaContext.Pushdown, cursorTime, cursorRowId, includeRowId: true);
                var parameters = new Dictionary<string, object?>(command.Parameters) {
                    ["$pageLimit"] = StoredReadPageSize
                };
                IReadOnlyList<(EventReportRow Row, string CursorTime, long CursorRowId)> page =
                    await session.QueryAsListAsync(
                        command.Sql + " LIMIT $pageLimit;",
                        record => (MapEventRow(record, schemaContext.ByName),
                            record.GetString(1), record.GetInt64(22)),
                        parameters,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                foreach ((EventReportRow row, string nextTime, long nextRowId) in page) {
                    cursorTime = nextTime;
                    cursorRowId = nextRowId;
                    if (!await ConsumeAsync(row).ConfigureAwait(false)) {
                        continueReading = false;
                        break;
                    }
                }
                if (page.Count < StoredReadPageSize) {
                    break;
                }
            }
            return new EventStoreRowReadResult(
                delivered,
                scanned,
                scanLimitReached || resultLimitReached,
                CreateReadCompletenessDiagnostic(snapshot.MaxEvents, scanLimitReached, resultLimitReached));

            async Task<bool> ConsumeAsync(EventReportRow row) {
                cancellationToken.ThrowIfCancellationRequested();
                if (candidateLimit > 0 && scanned >= candidateLimit) {
                    scanLimitReached = true;
                    return false;
                }
                scanned++;
                if (!MatchesDirectTextSelection(snapshot, row) ||
                    snapshot.Predicate != null &&
                    !EventPredicateEvaluator.Matches(snapshot.Predicate, row.ToPredicateDictionary())) {
                    return true;
                }
                if (snapshot.MaxEvents > 0 && delivered >= snapshot.MaxEvents) {
                    resultLimitReached = true;
                    return false;
                }
                if (!schemaContext.ByName.TryGetValue(row.Type, out EventReportSectionSchema? schema)) {
                    throw new InvalidDataException($"Stored row type '{row.Type}' has no persisted schema.");
                }
                EventReportEngine.NormalizeStoredRow(row, schema);
                await onRow(row, cancellationToken).ConfigureAwait(false);
                delivered++;
                return true;
            }
        } finally {
            await session.ExecuteNonQueryAsync("ROLLBACK;").ConfigureAwait(false);
        }
    }
}
