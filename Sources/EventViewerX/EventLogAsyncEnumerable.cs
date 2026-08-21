using System.Threading.Channels;

namespace EventViewerX;

/// <summary>
/// Streams a synchronous event source through a bounded asynchronous buffer
/// with deterministic cancellation and disposal.
/// </summary>
internal sealed class EventLogAsyncEnumerable : IAsyncEnumerable<EventObject> {
    private readonly Func<CancellationToken, IEnumerable<EventObject>> _source;
    private readonly int _bufferCapacity;
    private readonly CancellationToken _cancellationToken;

    /// <summary>
    /// Creates a bounded event stream without starting its source.
    /// </summary>
    internal EventLogAsyncEnumerable(
        Func<CancellationToken, IEnumerable<EventObject>> source,
        int bufferCapacity,
        CancellationToken cancellationToken) {

        _source = source ?? throw new ArgumentNullException(nameof(source));
        if (bufferCapacity <= 0 || bufferCapacity > 4096) {
            throw new ArgumentOutOfRangeException(
                nameof(bufferCapacity),
                "Buffer capacity must be between 1 and 4096.");
        }

        _bufferCapacity = bufferCapacity;
        _cancellationToken = cancellationToken;
    }

    /// <inheritdoc />
    public IAsyncEnumerator<EventObject> GetAsyncEnumerator(
        CancellationToken cancellationToken = default) =>
        new Enumerator(
            _source,
            _bufferCapacity,
            _cancellationToken,
            cancellationToken);

    private sealed class Enumerator : IAsyncEnumerator<EventObject> {
        private readonly Func<CancellationToken, IEnumerable<EventObject>>
            _source;
        private CancellationTokenSource? _consumerLink;
        private readonly CancellationToken _consumerCancellationToken;
        private readonly CancellationTokenSource _stop;
        private readonly Channel<EventObject> _channel;
        private readonly SemaphoreSlim _moveGate = new(1, 1);
        private Task? _producer;
        private int _disposed;

        internal Enumerator(
            Func<CancellationToken, IEnumerable<EventObject>> source,
            int bufferCapacity,
            CancellationToken streamCancellationToken,
            CancellationToken enumerationCancellationToken) {

            _source = source;
            _consumerCancellationToken = SelectConsumerCancellationToken(
                streamCancellationToken,
                enumerationCancellationToken,
                out _consumerLink);
            _stop = _consumerCancellationToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(
                    _consumerCancellationToken)
                : new CancellationTokenSource();
            _channel = Channel.CreateBounded<EventObject>(
                new BoundedChannelOptions(bufferCapacity) {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = true,
                    SingleWriter = true,
                    AllowSynchronousContinuations = false
                });
        }

        /// <inheritdoc />
        public EventObject Current { get; private set; } = null!;

        /// <inheritdoc />
        public ValueTask<bool> MoveNextAsync() =>
            new(MoveNextCoreAsync());

        /// <inheritdoc />
        public ValueTask DisposeAsync() =>
            new(DisposeCoreAsync());

        private async Task<bool> MoveNextCoreAsync() {
            await _moveGate.WaitAsync().ConfigureAwait(false);
            try {
                if (Volatile.Read(ref _disposed) != 0) {
                    return false;
                }

                _consumerCancellationToken.ThrowIfCancellationRequested();
                EnsureProducerStarted();
                try {
                    while (await _channel.Reader
                               .WaitToReadAsync(_stop.Token)
                               .ConfigureAwait(false)) {
                        if (_channel.Reader.TryRead(
                                out EventObject? eventObject)) {
                            _consumerCancellationToken
                                .ThrowIfCancellationRequested();
                            Current = eventObject;
                            return true;
                        }
                    }
                } catch (OperationCanceledException)
                    when (_consumerCancellationToken
                        .IsCancellationRequested) {
                    _consumerCancellationToken
                        .ThrowIfCancellationRequested();
                    throw;
                } catch (OperationCanceledException)
                    when (Volatile.Read(ref _disposed) != 0) {
                    Current = null!;
                    return false;
                }

                _consumerCancellationToken.ThrowIfCancellationRequested();
                Current = null!;
                return false;
            } finally {
                _moveGate.Release();
            }
        }

        private async Task DisposeCoreAsync() {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) {
                return;
            }

            _stop.Cancel();
            _consumerLink?.Dispose();
            _consumerLink = null;
            await _moveGate.WaitAsync().ConfigureAwait(false);
            _moveGate.Release();
            DisposeCancellationWhenProducerStops();
        }

        private void EnsureProducerStarted() {
            if (_producer != null) {
                return;
            }

            _producer = Task.Run(
                ProduceAsync,
                CancellationToken.None);
        }

        private async Task ProduceAsync() {
            try {
                foreach (EventObject eventObject in _source(_stop.Token)) {
                    await _channel.Writer.WriteAsync(
                        eventObject,
                        _stop.Token).ConfigureAwait(false);
                }
                _channel.Writer.TryComplete();
            } catch (OperationCanceledException)
                when (_stop.IsCancellationRequested) {
                _channel.Writer.TryComplete();
            } catch (Exception exception) {
                _channel.Writer.TryComplete(exception);
            }
        }

        private void DisposeCancellationWhenProducerStops() {
            Task? producer = _producer;
            if (producer == null) {
                DisposeCancellationSources();
                return;
            }

            if (producer.IsCompleted) {
                _ = producer.Exception;
                DisposeCancellationSources();
                return;
            }

            _ = producer.ContinueWith(
                completed => {
                    _ = completed.Exception;
                    DisposeCancellationSources();
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private void DisposeCancellationSources() {
            _stop.Dispose();
        }

        private static CancellationToken SelectConsumerCancellationToken(
            CancellationToken streamCancellationToken,
            CancellationToken enumerationCancellationToken,
            out CancellationTokenSource? consumerLink) {

            consumerLink = null;
            if (!streamCancellationToken.CanBeCanceled) {
                return enumerationCancellationToken;
            }
            if (!enumerationCancellationToken.CanBeCanceled ||
                streamCancellationToken == enumerationCancellationToken) {
                return streamCancellationToken;
            }

            consumerLink = CancellationTokenSource.CreateLinkedTokenSource(
                streamCancellationToken,
                enumerationCancellationToken);
            return consumerLink.Token;
        }
    }
}
