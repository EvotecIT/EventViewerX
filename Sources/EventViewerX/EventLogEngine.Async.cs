namespace EventViewerX;

public static partial class EventLogEngine {
    /// <summary>Asynchronously streams a local or remote channel through bounded memory.</summary>
    public static IAsyncEnumerable<EventObject> ReadChannelAsync(
        EventLogChannelQuery query,
        CancellationToken cancellationToken = default) {

        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        EventLogChannelQuery snapshot =
            EventLogQuerySnapshot.Copy(query);
        return ReadAsync(
            token => ReadChannel(snapshot, token),
            snapshot.BufferCapacity,
            cancellationToken);
    }

    /// <summary>Asynchronously streams an offline event log through bounded memory.</summary>
    public static IAsyncEnumerable<EventObject> ReadFileAsync(
        EventLogFileQuery query,
        int bufferCapacity = 64,
        CancellationToken cancellationToken = default) {

        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        EventLogFileQuery snapshot =
            EventLogQuerySnapshot.Copy(query);
        return ReadAsync(
            token => ReadFile(snapshot, token),
            bufferCapacity,
            cancellationToken);
    }

    /// <summary>Asynchronously streams a structured QueryList through bounded memory.</summary>
    public static IAsyncEnumerable<EventObject> ReadStructuredAsync(
        EventLogStructuredQuery query,
        CancellationToken cancellationToken = default) {

        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        EventLogStructuredQuery snapshot =
            EventLogQuerySnapshot.Copy(query);
        return ReadAsync(
            token => ReadStructured(snapshot, token),
            snapshot.BufferCapacity,
            cancellationToken);
    }

    /// <summary>Asynchronously streams a deterministic multi-source batch through bounded memory.</summary>
    public static IAsyncEnumerable<EventObject> ReadBatchAsync(
        EventLogBatchQuery query,
        CancellationToken cancellationToken = default) {

        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        return EventLogBatchEngine.ReadAsync(
            query,
            cancellationToken);
    }

    internal static IAsyncEnumerable<EventObject> ReadAsync(
        Func<CancellationToken, IEnumerable<EventObject>> source,
        int bufferCapacity,
        CancellationToken cancellationToken) =>
        new EventLogAsyncEnumerable(
            source,
            bufferCapacity,
            cancellationToken);
}
