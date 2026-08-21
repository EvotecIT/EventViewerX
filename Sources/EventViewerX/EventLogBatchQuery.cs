namespace EventViewerX;

/// <summary>
/// Defines a deterministic bounded merge across several channels or offline event-log files.
/// </summary>
public sealed class EventLogBatchQuery {
    private EventLogBatchQuery(
        IReadOnlyList<EventLogChannelQuery> channels,
        IReadOnlyList<EventLogFileQuery> files,
        IReadOnlyList<EventLogStructuredQuery> structured) {

        ChannelQueries = channels;
        FileQueries = files;
        StructuredQueries = structured;
    }

    /// <summary>Creates a multi-source channel query.</summary>
    public static EventLogBatchQuery ForChannels(
        IEnumerable<EventLogChannelQuery> channels) {

        if (channels == null) {
            throw new ArgumentNullException(nameof(channels));
        }
        EventLogChannelQuery[] materialized = channels.ToArray();
        if (materialized.Length == 0) {
            throw new ArgumentException(
                "At least one channel query is required.",
                nameof(channels));
        }
        if (materialized.Any(static query => query == null)) {
            throw new ArgumentException(
                "Channel queries cannot contain null values.",
                nameof(channels));
        }
        return new EventLogBatchQuery(
            materialized,
            Array.Empty<EventLogFileQuery>(),
            Array.Empty<EventLogStructuredQuery>());
    }

    /// <summary>Creates a multi-source offline file query.</summary>
    public static EventLogBatchQuery ForFiles(
        IEnumerable<EventLogFileQuery> files) {

        if (files == null) {
            throw new ArgumentNullException(nameof(files));
        }
        EventLogFileQuery[] materialized = files.ToArray();
        if (materialized.Length == 0) {
            throw new ArgumentException(
                "At least one file query is required.",
                nameof(files));
        }
        if (materialized.Any(static query => query == null)) {
            throw new ArgumentException(
                "File queries cannot contain null values.",
                nameof(files));
        }
        return new EventLogBatchQuery(
            Array.Empty<EventLogChannelQuery>(),
            materialized,
            Array.Empty<EventLogStructuredQuery>());
    }

    /// <summary>Creates a multi-session structured XML query.</summary>
    public static EventLogBatchQuery ForStructured(
        IEnumerable<EventLogStructuredQuery> structured) {

        if (structured == null) {
            throw new ArgumentNullException(nameof(structured));
        }
        EventLogStructuredQuery[] materialized = structured.ToArray();
        if (materialized.Length == 0) {
            throw new ArgumentException(
                "At least one structured query is required.",
                nameof(structured));
        }
        if (materialized.Any(static query => query == null)) {
            throw new ArgumentException(
                "Structured queries cannot contain null values.",
                nameof(structured));
        }
        return new EventLogBatchQuery(
            Array.Empty<EventLogChannelQuery>(),
            Array.Empty<EventLogFileQuery>(),
            materialized);
    }

    /// <summary>
    /// Combines independently built channel, file, and structured batches into one bounded merge.
    /// Batch-level limits, concurrency, error handling, and failure callbacks
    /// must agree and are preserved on the combined query.
    /// </summary>
    public static EventLogBatchQuery Combine(
        IEnumerable<EventLogBatchQuery> batches) {

        if (batches == null) {
            throw new ArgumentNullException(nameof(batches));
        }
        EventLogBatchQuery[] materialized = batches
            .Where(static batch => batch != null)
            .ToArray();
        if (materialized.Length == 0) {
            throw new ArgumentException(
                "At least one batch is required.",
                nameof(batches));
        }
        EventLogBatchQuery controls = materialized[0];
        if (materialized.Skip(1).Any(batch =>
                batch.MaxEvents != controls.MaxEvents ||
                batch.MaxConcurrency != controls.MaxConcurrency ||
                batch.ContinueOnError != controls.ContinueOnError ||
                !Equals(
                    batch.FailureHandler,
                    controls.FailureHandler))) {
            throw new ArgumentException(
                "All combined batches must use the same MaxEvents, MaxConcurrency, ContinueOnError, and FailureHandler controls.",
                nameof(batches));
        }
        return new EventLogBatchQuery(
            materialized
                .SelectMany(static batch =>
                    batch.ChannelQueries)
                .ToArray(),
            materialized
                .SelectMany(static batch =>
                    batch.FileQueries)
                .ToArray(),
            materialized
                .SelectMany(static batch =>
                    batch.StructuredQueries)
                .ToArray()) {
            MaxEvents = controls.MaxEvents,
            MaxConcurrency = controls.MaxConcurrency,
            ContinueOnError = controls.ContinueOnError,
            FailureHandler = controls.FailureHandler
        };
    }

    /// <summary>Channel queries in this batch.</summary>
    public IReadOnlyList<EventLogChannelQuery> ChannelQueries { get; }

    /// <summary>Offline file queries in this batch.</summary>
    public IReadOnlyList<EventLogFileQuery> FileQueries { get; }

    /// <summary>Structured XML queries in this batch.</summary>
    public IReadOnlyList<EventLogStructuredQuery> StructuredQueries { get; }

    /// <summary>Maximum number of merged records. Zero streams every match.</summary>
    public long MaxEvents { get; set; }

    /// <summary>
    /// Maximum number of independent sources opened and primed concurrently.
    /// Once primed, each source retains one native cursor so the merge can compare
    /// one detached head record from every source deterministically.
    /// </summary>
    public int MaxConcurrency { get; set; } = 8;

    /// <summary>
    /// Continues reading other sources when one source fails and reports the failure through
    /// <see cref="FailureHandler"/>.
    /// </summary>
    public bool ContinueOnError { get; set; }

    /// <summary>
    /// Receives failures isolated to individual query sources. Calls are
    /// serialized even when several sources fail during parallel priming.
    /// </summary>
    public Action<EventLogQueryFailure>? FailureHandler { get; set; }
}
