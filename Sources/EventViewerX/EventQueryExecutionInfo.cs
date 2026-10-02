namespace EventViewerX;

/// <summary>Progress and completion diagnostics for one batch execution. Use a separate instance for each read.</summary>
public sealed class EventQueryExecutionInfo {
    private long _nativeEventsRead;
    private long _sourceFailures;
    private int _started;
    private long _bufferedEvents;
    private long _peakBufferedEvents;
    private long _sourceReadTicks;
    /// <summary>Detached source candidates read, including heads prefetched for the merge.</summary>
    public long NativeEventsRead => Interlocked.Read(ref _nativeEventsRead);
    /// <summary>Events accepted for delivery by the batch or its PowerShell consumer.</summary>
    public long EventsEmitted { get; internal set; }
    /// <summary>Events rejected by a consumer's managed output filters.</summary>
    public long ManagedRejections { get; internal set; }
    /// <summary>Source open or read failures, including failures that terminate the query.</summary>
    public long SourceFailures => Interlocked.Read(ref _sourceFailures);
    /// <summary>Asynchronous output snapshots buffered or waiting for one producer write.</summary>
    public long BufferedEvents => Interlocked.Read(ref _bufferedEvents);
    /// <summary>Largest asynchronous output backlog, including one pending producer write.</summary>
    public long PeakBufferedEvents => Interlocked.Read(ref _peakBufferedEvents);
    /// <summary>Cumulative time in source MoveNext calls; concurrent source priming can overlap.</summary>
    public TimeSpan SourceReadDuration => TimeSpan.FromSeconds(
        (double)Interlocked.Read(ref _sourceReadTicks) / System.Diagnostics.Stopwatch.Frequency);
    /// <summary>Elapsed time spent priming source heads.</summary>
    public TimeSpan PrimingDuration { get; internal set; }
    /// <summary>Elapsed wall time, including consumer backpressure.</summary>
    public TimeSpan Duration { get; internal set; }
    /// <summary>Whether the query reached its configured end rather than being stopped by the consumer.</summary>
    public bool Completed { get; internal set; }
    /// <summary>Whether request cancellation stopped the query.</summary>
    public bool Canceled { get; internal set; }
    internal void RecordNativeEvent() => Interlocked.Increment(ref _nativeEventsRead);
    internal void RecordFailure() => Interlocked.Increment(ref _sourceFailures);
    internal void RecordSourceRead(long started) => Interlocked.Add(ref _sourceReadTicks,
        System.Diagnostics.Stopwatch.GetTimestamp() - started);
    internal void BufferEvent() {
        long count = Interlocked.Increment(ref _bufferedEvents);
        long peak = Interlocked.Read(ref _peakBufferedEvents);
        while (count > peak) {
            long previous = Interlocked.CompareExchange(ref _peakBufferedEvents, count, peak);
            if (previous == peak) {
                break;
            }
            peak = previous;
        }
    }
    internal void ReleaseBufferedEvent() => Interlocked.Decrement(ref _bufferedEvents);
    internal void Begin() {
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0) {
            throw new InvalidOperationException("Use a separate execution-info instance for each read.");
        }
    }
}