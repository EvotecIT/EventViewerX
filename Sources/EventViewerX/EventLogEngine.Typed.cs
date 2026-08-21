namespace EventViewerX;

/// <summary>
/// Typed convenience entry points over the partition-safe query factory and batch engine.
/// </summary>
public static partial class EventLogEngine {
    /// <summary>
    /// Streams a channel using a typed filter without requiring callers to construct
    /// XPath or manually partition filters that exceed the native expression limit.
    /// </summary>
    public static IEnumerable<EventObject> ReadChannel(
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
        return EventLogBatchEngine.Read(
            batch,
            cancellationToken);
    }

    /// <summary>
    /// Streams several channels and machines through one deterministic, bounded merge.
    /// </summary>
    public static IEnumerable<EventObject> ReadChannels(
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
        return EventLogBatchEngine.Read(
            batch,
            cancellationToken);
    }

    /// <summary>
    /// Streams one offline event log using a typed, partition-safe filter.
    /// </summary>
    public static IEnumerable<EventObject> ReadFile(
        string path,
        EventFilter? filter = null,
        EventLogQueryOptions? options = null,
        CancellationToken cancellationToken = default) {

        EventLogBatchQuery batch =
            EventLogQueryFactory.ForFiles(
                new[] { path },
                filter,
                options);
        return EventLogBatchEngine.Read(
            batch,
            cancellationToken);
    }

    /// <summary>
    /// Streams several offline event logs through one deterministic, bounded merge.
    /// </summary>
    public static IEnumerable<EventObject> ReadFiles(
        IEnumerable<string> paths,
        EventFilter? filter = null,
        EventLogQueryOptions? options = null,
        CancellationToken cancellationToken = default) {

        EventLogBatchQuery batch =
            EventLogQueryFactory.ForFiles(
                paths,
                filter,
                options);
        return EventLogBatchEngine.Read(
            batch,
            cancellationToken);
    }
}
