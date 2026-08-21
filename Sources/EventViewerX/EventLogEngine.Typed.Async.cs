namespace EventViewerX;

/// <summary>
/// Asynchronous typed convenience entry points over the partition-safe query factory.
/// </summary>
public static partial class EventLogEngine {
    /// <summary>
    /// Asynchronously streams one channel using a typed, partition-safe filter.
    /// </summary>
    public static IAsyncEnumerable<EventObject> ReadChannelAsync(
        string logName,
        EventFilter? filter = null,
        string? machineName = null,
        EventLogQueryOptions? options = null,
        CancellationToken cancellationToken = default) {

        EventLogBatchQuery batch =
            EventLogQueryFactory.ForChannels(
                new[] { logName },
                new string?[] { machineName },
                filter,
                options);
        return EventLogBatchEngine.ReadAsync(
            batch,
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously streams several channels and machines through a bounded merge.
    /// </summary>
    public static IAsyncEnumerable<EventObject> ReadChannelsAsync(
        IEnumerable<string> logNames,
        IEnumerable<string?>? machineNames = null,
        EventFilter? filter = null,
        EventLogQueryOptions? options = null,
        CancellationToken cancellationToken = default) {

        EventLogBatchQuery batch =
            EventLogQueryFactory.ForChannels(
                logNames,
                machineNames,
                filter,
                options);
        return EventLogBatchEngine.ReadAsync(
            batch,
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously streams one offline event log using a typed, partition-safe filter.
    /// </summary>
    public static IAsyncEnumerable<EventObject> ReadFileAsync(
        string path,
        EventFilter? filter = null,
        EventLogQueryOptions? options = null,
        CancellationToken cancellationToken = default) {

        EventLogBatchQuery batch =
            EventLogQueryFactory.ForFiles(
                new[] { path },
                filter,
                options);
        return EventLogBatchEngine.ReadAsync(
            batch,
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously streams several offline event logs through a bounded merge.
    /// </summary>
    public static IAsyncEnumerable<EventObject> ReadFilesAsync(
        IEnumerable<string> paths,
        EventFilter? filter = null,
        EventLogQueryOptions? options = null,
        CancellationToken cancellationToken = default) {

        EventLogBatchQuery batch =
            EventLogQueryFactory.ForFiles(
                paths,
                filter,
                options);
        return EventLogBatchEngine.ReadAsync(
            batch,
            cancellationToken);
    }
}
