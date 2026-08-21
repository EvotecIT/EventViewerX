namespace EventViewerX;

/// <summary>
/// Streams a deterministic bounded merge of several native Windows Event Log queries.
/// Each source retains native record order; the next available source head is chosen by timestamp.
/// Windows records can contain non-monotonic timestamps, so this intentionally does not claim a full chronological sort.
/// </summary>
public static partial class EventLogBatchEngine {
    /// <summary>
    /// Reads a channel or file batch while keeping only one detached event per source in merge memory.
    /// </summary>
    public static IEnumerable<EventObject> Read(
        EventLogBatchQuery query,
        CancellationToken cancellationToken = default) {

        EventLogBatchExecutionPlan plan =
            CreateExecutionPlan(query);
        return ReadSynchronously(
            plan,
            cancellationToken);
    }

    private static EventLogBatchExecutionPlan CreateExecutionPlan(
        EventLogBatchQuery query) {

        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        if (query.MaxEvents < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                "Maximum events must be greater than or equal to zero.");
        }
        ValidateReadModes(query);
        ValidateConcurrency(query.MaxConcurrency);

        EventSourceSnapshot[] sources = SnapshotSources(query);
        bool oldest = sources[0].Oldest;
        if (sources.Any(source => source.Oldest != oldest)) {
            throw new ArgumentException(
                "Every source in a batch must use the same ordering direction.",
                nameof(query));
        }
        Action<EventLogQueryFailure>? failureHandler =
            CreateSerializedFailureHandler(
                query.FailureHandler);
        return new EventLogBatchExecutionPlan(
            sources,
            oldest,
            query.MaxEvents,
            query.MaxConcurrency,
            query.ContinueOnError,
            failureHandler);
    }

    private static void ValidateConcurrency(int maxConcurrency) {
        if (maxConcurrency <= 0 ||
            maxConcurrency > EventLogLimits.MaximumConcurrency) {
            throw new ArgumentOutOfRangeException(
                nameof(maxConcurrency),
                $"Maximum concurrency must be between 1 and {EventLogLimits.MaximumConcurrency}.");
        }
    }

    private static void ValidateReadModes(
        EventLogBatchQuery query) {

        foreach (EventReadMode readMode in query.ChannelQueries
                     .Select(static source => source.ReadMode)
                     .Concat(query.FileQueries.Select(
                         static source => source.ReadMode))
                     .Concat(query.StructuredQueries.Select(
                         static source => source.ReadMode))) {
            EventReadModeValidation.EnsureDefined(
                readMode,
                nameof(query));
        }
    }

    private static EventSourceSnapshot[] SnapshotSources(EventLogBatchQuery query) {
        long sourceLimit = query.MaxEvents;
        EventSourceSnapshot[] channels =
            query.ChannelQueries
                .Select(channel => {
                    EventLogChannelQuery snapshot =
                        EventLogQuerySnapshot.Copy(
                            channel,
                            sourceLimit);
                    return new EventSourceSnapshot(
                        snapshot.LogName,
                        snapshot.MachineName,
                        snapshot.Oldest,
                        cancellationToken =>
                            EventLogEngine.ReadChannel(
                                snapshot,
                                cancellationToken));
                })
                .ToArray();
        EventSourceSnapshot[] files =
            query.FileQueries
                .Select(file => {
                    EventLogFileQuery snapshot =
                        EventLogQuerySnapshot.Copy(
                            file,
                            sourceLimit);
                    return new EventSourceSnapshot(
                        snapshot.Path,
                        null,
                        snapshot.Oldest,
                        cancellationToken =>
                            EventLogEngine.ReadFile(
                                snapshot,
                                cancellationToken));
                })
                .ToArray();
        EventLogStructuredQuery[] structuredSources =
            query.StructuredQueries
                .SelectMany(static structured =>
                    ExpandStructuredSources(
                        structured))
                .ToArray();
        EventSourceSnapshot[] structured =
            structuredSources
                .Select((structured, index) => {
                    EventLogStructuredQuery snapshot =
                        EventLogQuerySnapshot.Copy(
                            structured,
                            sourceLimit);
                    return new EventSourceSnapshot(
                        $"StructuredQuery[{index}]",
                        snapshot.MachineName,
                        snapshot.Oldest,
                        cancellationToken =>
                            EventLogEngine.ReadStructured(
                                snapshot,
                                cancellationToken));
                })
                .ToArray();
        EventSourceSnapshot[] all = channels
            .Concat(files)
            .Concat(structured)
            .ToArray();
        if (all.Length > 0) {
            return all;
        }
        throw new ArgumentException(
            "The batch does not contain any query sources.",
            nameof(query));
    }

    internal static IReadOnlyList<EventLogStructuredQuery>
        ExpandStructuredSources(
            EventLogStructuredQuery source) {

        if (source == null) {
            throw new ArgumentNullException(nameof(source));
        }
        if (source.GetIndependentSourceCount() <= 1) {
            return new[] { source };
        }

        EventLogBatchQuery split =
            EventLogBatchQuery.ForStructured(
                new[] { source });
        return EventLogBatchConsolidator
            .Consolidate(split)
            .StructuredQueries
            .ToArray();
    }

    private static IEnumerable<EventObject> ReadSynchronously(
        EventLogBatchExecutionPlan plan,
        CancellationToken cancellationToken) {

        EventSourceCursor?[] primed =
            PrimeSourcesSynchronously(
                plan.Sources,
                plan.MaxConcurrency,
                plan.ContinueOnError,
                plan.FailureHandler,
                cancellationToken);
        var cursors = primed
            .Where(static cursor => cursor != null)
            .Cast<EventSourceCursor>()
            .ToList();
        var queue = new SortedSet<EventSourceCursor>(
            new EventSourceCursorComparer(plan.Oldest));
        foreach (EventSourceCursor cursor in cursors) {
            queue.Add(cursor);
        }

        long returned = 0;
        try {
            while (queue.Count > 0) {
                cancellationToken.ThrowIfCancellationRequested();
                EventSourceCursor cursor = queue.Min!;
                queue.Remove(cursor);
                yield return cursor.Current;
                returned++;
                if (plan.MaxEvents > 0 &&
                    returned >= plan.MaxEvents) {
                    yield break;
                }

                if (TryMoveNext(
                        cursor,
                        plan.ContinueOnError,
                        plan.FailureHandler,
                        cancellationToken)) {
                    queue.Add(cursor);
                }
            }
        } finally {
            foreach (EventSourceCursor cursor in cursors) {
                cursor.Dispose();
            }
        }
    }

    private static EventSourceCursor?[] PrimeSourcesSynchronously(
        EventSourceSnapshot[] sources,
        int maxConcurrency,
        bool continueOnError,
        Action<EventLogQueryFailure>? failureHandler,
        CancellationToken cancellationToken) {

        return PrimeConcurrently<EventSourceCursor>(
            sources.Length,
            maxConcurrency,
            cancellationToken,
            (index, primingToken) => {
                EventSourceCursor? cursor = TryOpenCursor(
                    index,
                    sources[index],
                    continueOnError,
                    failureHandler,
                    primingToken);
                if (cursor == null) {
                    return null;
                }
                try {
                    if (TryMoveNext(
                            cursor,
                            continueOnError,
                            failureHandler,
                            primingToken)) {
                        EventSourceCursor result =
                            cursor;
                        cursor = null;
                        return result;
                    }
                    return null;
                } finally {
                    cursor?.Dispose();
                }
            });
    }

    private static EventSourceCursor? TryOpenCursor(
        int index,
        EventSourceSnapshot source,
        bool continueOnError,
        Action<EventLogQueryFailure>? failureHandler,
        CancellationToken cancellationToken) {

        try {
            return new EventSourceCursor(
                index,
                source,
                source.Open(cancellationToken)
                    .GetEnumerator());
        } catch (Exception exception) {
            if (!continueOnError) {
                throw;
            }
            failureHandler?.Invoke(new EventLogQueryFailure(
                source.Source,
                source.MachineName,
                exception));
            return null;
        }
    }

    private static bool TryMoveNext(
        EventSourceCursor cursor,
        bool continueOnError,
        Action<EventLogQueryFailure>? failureHandler,
        CancellationToken cancellationToken) {

        try {
            cancellationToken.ThrowIfCancellationRequested();
            return cursor.MoveNext();
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception exception) {
            if (!continueOnError) {
                throw;
            }
            failureHandler?.Invoke(new EventLogQueryFailure(
                cursor.Source.Source,
                cursor.Source.MachineName,
                exception));
            cursor.Dispose();
            return false;
        }
    }

    private static Action<EventLogQueryFailure>?
        CreateSerializedFailureHandler(
            Action<EventLogQueryFailure>? failureHandler) {

        if (failureHandler == null) {
            return null;
        }
        var synchronization = new object();
        return failure => {
            lock (synchronization) {
                failureHandler(failure);
            }
        };
    }

    private static int CompareEvents(
        EventObject left,
        EventObject right,
        bool oldest) {

        int timeComparison = oldest
            ? left.TimeCreated.CompareTo(right.TimeCreated)
            : right.TimeCreated.CompareTo(left.TimeCreated);
        if (timeComparison != 0) {
            return timeComparison;
        }

        int machineComparison = string.Compare(
            left.GatheredFrom,
            right.GatheredFrom,
            StringComparison.OrdinalIgnoreCase);
        if (machineComparison != 0) {
            return oldest ? machineComparison : -machineComparison;
        }

        int logComparison = string.Compare(
            left.LogName,
            right.LogName,
            StringComparison.OrdinalIgnoreCase);
        if (logComparison != 0) {
            return oldest ? logComparison : -logComparison;
        }

        int recordComparison = oldest
            ? Nullable.Compare(left.RecordId, right.RecordId)
            : Nullable.Compare(right.RecordId, left.RecordId);
        if (recordComparison != 0) {
            return recordComparison;
        }

        int idComparison = left.Id.CompareTo(right.Id);
        return oldest ? idComparison : -idComparison;
    }

    private sealed class EventSourceSnapshot {
        internal EventSourceSnapshot(
            string source,
            string? machineName,
            bool oldest,
            Func<CancellationToken, IEnumerable<EventObject>> open) {

            Source = source;
            MachineName = machineName;
            Oldest = oldest;
            Open = open;
        }

        internal string Source { get; }
        internal string? MachineName { get; }
        internal bool Oldest { get; }
        internal Func<CancellationToken, IEnumerable<EventObject>> Open {
            get;
        }
    }

    private sealed class EventLogBatchExecutionPlan {
        internal EventLogBatchExecutionPlan(
            EventSourceSnapshot[] sources,
            bool oldest,
            long maxEvents,
            int maxConcurrency,
            bool continueOnError,
            Action<EventLogQueryFailure>? failureHandler) {

            Sources = sources;
            Oldest = oldest;
            MaxEvents = maxEvents;
            MaxConcurrency = maxConcurrency;
            ContinueOnError = continueOnError;
            FailureHandler = failureHandler;
        }

        internal EventSourceSnapshot[] Sources { get; }
        internal bool Oldest { get; }
        internal long MaxEvents { get; }
        internal int MaxConcurrency { get; }
        internal bool ContinueOnError { get; }
        internal Action<EventLogQueryFailure>? FailureHandler { get; }
    }

    private sealed class EventSourceCursor : IDisposable {
        private readonly IEnumerator<EventObject> _enumerator;
        private bool _disposed;

        internal EventSourceCursor(
            int index,
            EventSourceSnapshot source,
            IEnumerator<EventObject> enumerator) {

            Index = index;
            Source = source;
            _enumerator = enumerator;
        }

        internal int Index { get; }
        internal EventSourceSnapshot Source { get; }
        internal EventObject Current => _enumerator.Current;

        internal bool MoveNext() {
            if (_enumerator.MoveNext()) {
                return true;
            }
            Dispose();
            return false;
        }

        public void Dispose() {
            if (_disposed) {
                return;
            }
            _disposed = true;
            _enumerator.Dispose();
        }
    }

    private sealed class EventSourceCursorComparer : IComparer<EventSourceCursor> {
        private readonly bool _oldest;

        internal EventSourceCursorComparer(bool oldest) {
            _oldest = oldest;
        }

        public int Compare(
            EventSourceCursor? left,
            EventSourceCursor? right) {

            if (ReferenceEquals(left, right)) {
                return 0;
            }
            if (left == null) {
                return 1;
            }
            if (right == null) {
                return -1;
            }

            int eventComparison = CompareEvents(
                left.Current,
                right.Current,
                _oldest);
            return eventComparison != 0
                ? eventComparison
                : left.Index.CompareTo(right.Index);
        }
    }
}
