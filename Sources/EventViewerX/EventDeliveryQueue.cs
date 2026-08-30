using System.Threading.Channels;

namespace EventViewerX;

/// <summary>
/// Bounded, single-consumer delivery queue for moving native watcher callbacks onto an asynchronous processor.
/// Synchronous producers fail explicitly when capacity is exhausted; asynchronous producers can wait for space.
/// </summary>
/// <typeparam name="T">Detached item type delivered to the processor.</typeparam>
public sealed class EventDeliveryQueue<T> : IAsyncDisposable {
    private readonly Channel<T> _channel;
    private readonly Func<T, CancellationToken, ValueTask> _processor;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Queue<DateTime> _pendingAcceptedUtc = new();
    private readonly object _pendingLock = new();
    private readonly Task _worker;
    private readonly int _capacity;
    private long _accepted;
    private long _completed;
    private int _depth;
    private int _highWatermark;
    private Exception? _failure;
    private int _completionRequested;

    /// <summary>Creates and starts one bounded delivery worker.</summary>
    public EventDeliveryQueue(int capacity, Func<T, CancellationToken, ValueTask> processor) {
        if (capacity <= 0) {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
        _capacity = capacity;
        _channel = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity) {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait
        });
        _worker = ProcessAsync();
    }

    /// <summary>Completes when all accepted items finish or the processor fails.</summary>
    public Task Completion => _worker;

    /// <summary>
    /// Attempts to enqueue immediately. False means capacity is exhausted or completion has started;
    /// the item was not accepted and must be retried or treated as a terminal delivery failure.
    /// </summary>
    public bool TryWrite(T item) {
        lock (_pendingLock) {
            if (!_channel.Writer.TryWrite(item)) {
                return false;
            }
            _pendingAcceptedUtc.Enqueue(DateTime.UtcNow);
            MarkAccepted();
        }
        return true;
    }

    /// <summary>Waits for bounded capacity and enqueues one item.</summary>
    public async ValueTask WriteAsync(T item, CancellationToken cancellationToken = default) {
        while (await _channel.Writer.WaitToWriteAsync(cancellationToken).ConfigureAwait(false)) {
            lock (_pendingLock) {
                if (!_channel.Writer.TryWrite(item)) {
                    continue;
                }
                _pendingAcceptedUtc.Enqueue(DateTime.UtcNow);
                MarkAccepted();
                return;
            }
        }
        throw new ChannelClosedException();
    }

    /// <summary>Stops accepting new items and drains every accepted item.</summary>
    public void Complete() {
        if (Interlocked.Exchange(ref _completionRequested, 1) == 0) {
            _channel.Writer.TryComplete();
        }
    }

    /// <summary>Returns queue health without blocking the producer or consumer.</summary>
    public EventDeliveryQueueSnapshot GetSnapshot() {
        DateTime? oldest;
        lock (_pendingLock) {
            oldest = _pendingAcceptedUtc.Count == 0 ? null : _pendingAcceptedUtc.Peek();
        }
        return new EventDeliveryQueueSnapshot(
            _capacity,
            Interlocked.Read(ref _accepted),
            Interlocked.Read(ref _completed),
            Volatile.Read(ref _depth),
            Volatile.Read(ref _highWatermark),
            oldest,
            Volatile.Read(ref _failure));
    }

    /// <summary>Cancels processing and releases queue resources.</summary>
    public async ValueTask DisposeAsync() {
        Complete();
        _cancellation.Cancel();
        try {
            await _worker.ConfigureAwait(false);
        } finally {
            _cancellation.Dispose();
        }
    }

    private void MarkAccepted() {
        Interlocked.Increment(ref _accepted);
        int depth = Interlocked.Increment(ref _depth);
        while (true) {
            int high = Volatile.Read(ref _highWatermark);
            if (depth <= high || Interlocked.CompareExchange(ref _highWatermark, depth, high) == high) {
                break;
            }
        }
    }

    private async Task ProcessAsync() {
        try {
            await foreach (T item in _channel.Reader.ReadAllAsync(_cancellation.Token).ConfigureAwait(false)) {
                await _processor(item, _cancellation.Token).ConfigureAwait(false);
                Interlocked.Increment(ref _completed);
                Interlocked.Decrement(ref _depth);
                lock (_pendingLock) {
                    _pendingAcceptedUtc.Dequeue();
                }
            }
        } catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) {
            _channel.Writer.TryComplete();
        } catch (Exception exception) {
            Volatile.Write(ref _failure, exception);
            Interlocked.Exchange(ref _completionRequested, 1);
            _channel.Writer.TryComplete(exception);
            throw;
        }
    }
}
