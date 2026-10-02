using EventViewerX.Reporting;

namespace EventViewerX.Storage;

public sealed partial class EventStore {
    /// <summary>
    /// Consumes rows with backpressure and commits bounded batches using the same schema, identity,
    /// and checkpoint contracts as WriteAsync. Completed batches survive cancellation or a later source failure.
    /// </summary>
    /// <param name="rows">Single-consumer row stream. Do not mutate a submitted row until its batch commits.</param>
    /// <param name="schemas">Homogeneous source schemas, snapshotted before enumeration.</param>
    /// <param name="batchSize">Maximum rows retained for one transaction, between 1 and 4096.</param>
    /// <param name="checkpointFactory">Optional checkpoint derived only from the current batch's rows.</param>
    /// <param name="expectedCheckpoint">Durable checkpoint observed before reading; each subsequent commit uses the prior committed value.</param>
    /// <param name="cancellationToken">Stops enumeration and rolls back any active batch.</param>
    public async Task<EventStoreIngestionResult> WriteStreamAsync(
        IAsyncEnumerable<EventReportRow> rows,
        IReadOnlyList<EventReportSectionSchema> schemas,
        int batchSize = 256,
        Func<IReadOnlyList<EventReportRow>, EventStoreCheckpoint>? checkpointFactory = null,
        EventStoreCheckpoint? expectedCheckpoint = null,
        CancellationToken cancellationToken = default) {

        if (rows == null) {
            throw new ArgumentNullException(nameof(rows));
        }
        if (schemas == null) {
            throw new ArgumentNullException(nameof(schemas));
        }
        if (batchSize < 1 || batchSize > 4096) {
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        }
        if (checkpointFactory == null && expectedCheckpoint != null) {
            throw new ArgumentException("An expected checkpoint requires a checkpoint factory.", nameof(expectedCheckpoint));
        }
        if (schemas.Any(static schema => schema == null || schema.Columns == null ||
            schema.Columns.Any(static column => column == null))) {
            throw new InvalidDataException("Ingestion schemas and columns cannot be null.");
        }
        EventReportSectionSchema[] snapshot = NormalizeIncomingSchemas(schemas.ToArray())
            .Select(schema => DeserializeStoredSchema(schema.Name,
                System.Text.Json.JsonSerializer.Serialize(schema, JsonOptions))).ToArray();
        EventStoreCheckpoint? previous = SnapshotCheckpoint(expectedCheckpoint);
        var batch = new List<EventReportRow>(batchSize);
        long attempted = 0;
        long inserted = 0;
        long committedBatches = 0;
        await foreach (EventReportRow row in rows.WithCancellation(cancellationToken).ConfigureAwait(false)) {
            cancellationToken.ThrowIfCancellationRequested();
            if (row == null) {
                throw new InvalidDataException("An ingestion stream cannot contain null rows.");
            }
            batch.Add(row);
            if (batch.Count == batchSize) {
                await CommitBatchAsync().ConfigureAwait(false);
            }
        }
        if (batch.Count > 0) {
            await CommitBatchAsync().ConfigureAwait(false);
        }
        return new EventStoreIngestionResult(attempted, inserted, committedBatches, SnapshotCheckpoint(previous));

        async Task CommitBatchAsync() {
            cancellationToken.ThrowIfCancellationRequested();
            EventReportRow[] batchRows = batch.ToArray();
            EventStoreCheckpoint? next = checkpointFactory == null ? null : SnapshotCheckpoint(
                checkpointFactory(Array.AsReadOnly(batchRows)) ??
                throw new InvalidDataException("The checkpoint factory cannot return null."));
            EventReport report = EventReportEngine.CreateStored(batchRows, snapshot);
            EventStoreWriteResult result = next == null
                ? await WriteAsync(report, cancellationToken: cancellationToken).ConfigureAwait(false)
                : await WriteAsync(report, next, previous, cancellationToken).ConfigureAwait(false);
            attempted += result.Attempted;
            inserted += result.Inserted;
            committedBatches++;
            previous = SnapshotCheckpoint(result.Checkpoint);
            batch.Clear();
        }
    }
}