using DBAClientX;
using EventViewerX.Reporting;

namespace EventViewerX.Storage;

public sealed partial class EventStore {
    /// <summary>
    /// Streams selected history in deterministic time and insertion order without retaining the result set.
    /// A row-id high-water mark keeps concurrent new definitions and rows outside the selected window.
    /// Modern targets use one SQLite statement snapshot; .NET Framework reads bounded pages.
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
        StoredSchemaContext schemaContext;
        long upperRowId;
        await using SQLiteAsyncSession session = await sqlite
            .OpenSessionAsync(Path, cancellationToken)
            .ConfigureAwait(false);
        // Capture this before schemas: a later writer cannot add a definition whose rows
        // enter this read, even when it commits between schema loading and the row query.
        upperRowId = Convert.ToInt64(await session.ExecuteScalarAsync(
            "SELECT COALESCE(MAX(rowid), 0) FROM evx_events;",
            cancellationToken: cancellationToken).ConfigureAwait(false));
        schemaContext = await ReadSchemaContextAsync(
            session,
            snapshot.ResolveDefinitionNames(),
            snapshot.DefinitionSchemas,
            cancellationToken).ConfigureAwait(false);
        snapshot.Predicate = NormalizeStoredPredicate(snapshot.Predicate, schemaContext.Schemas);
        QueryCommand command = BuildReadCommand(snapshot, schemaContext.Pushdown, upperRowId);
        long scanned = 0;
        long delivered = 0;
        bool scanLimitReached = false;
        bool resultLimitReached = false;
#if NETFRAMEWORK
        long offset = 0;
        bool continueReading = true;
        while (continueReading) {
            long pageLimit = StoredReadPageSize;
            var parameters = new Dictionary<string, object?>(command.Parameters) {
                ["$pageLimit"] = pageLimit,
                ["$pageOffset"] = offset
            };
            IReadOnlyList<EventReportRow> page = await session.QueryAsListAsync(
                command.Sql + " LIMIT $pageLimit OFFSET $pageOffset;",
                record => MapEventRow(record, schemaContext.ByName),
                parameters,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            offset += page.Count;
            foreach (EventReportRow row in page) {
                if (!await ConsumeAsync(row).ConfigureAwait(false)) {
                    continueReading = false;
                    break;
                }
            }
            if (page.Count < pageLimit) {
                break;
            }
        }
#else
        await foreach (EventReportRow row in sqlite.QueryStreamAsync(
                           Path,
                           command.Sql + ";",
                           record => MapEventRow(record, schemaContext.ByName),
                           command.Parameters,
                           useTransaction: false,
                           cancellationToken: cancellationToken).ConfigureAwait(false)) {
            if (!await ConsumeAsync(row).ConfigureAwait(false)) {
                break;
            }
        }
#endif
        return new EventStoreRowReadResult(
            delivered,
            scanned,
            scanLimitReached || resultLimitReached,
            CreateReadCompletenessDiagnostic(snapshot.MaxEvents, scanLimitReached, resultLimitReached));

        async Task<bool> ConsumeAsync(EventReportRow row) {
            cancellationToken.ThrowIfCancellationRequested();
            if (command.CandidateLimit > 0 && scanned >= command.CandidateLimit) {
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
    }
}
