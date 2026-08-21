using DBAClientX;
using EventViewerX.Reporting;

namespace EventViewerX.Storage;

public sealed partial class EventStore {
    /// <summary>
    /// Reads detached homogeneous schemas selected by the supplied stored query without reading event rows.
    /// </summary>
    public async Task<IReadOnlyList<EventReportSectionSchema>> GetSchemasAsync(
        EventStoreQuery? query = null,
        CancellationToken cancellationToken = default) {

        EnsureInitialized();
        EventStoreQuery snapshot = (query ?? new EventStoreQuery()).Snapshot();
        using var sqlite = new SQLite { BusyTimeoutMs = 10000 };
        await using SQLiteAsyncSession session = await sqlite
            .OpenSessionAsync(Path, cancellationToken)
            .ConfigureAwait(false);
        StoredSchemaContext context = await ReadSchemaContextAsync(
                session,
                snapshot.ResolveDefinitionNames(),
                snapshot.DefinitionSchemas,
                cancellationToken)
            .ConfigureAwait(false);
        return context.Schemas.ToArray();
    }
}
