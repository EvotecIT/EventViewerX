namespace EventViewerX.Native;

/// <summary>
/// Defers disposal of an owned resource until every in-flight native operation releases its lease.
/// </summary>
internal sealed class RetainedDisposable<T> : IDisposable
    where T : IDisposable {
    private readonly object _sync = new();
    private T? _value;
    private int _references = 1;
    private int _ownerReleased;

    internal RetainedDisposable(T value) {
        _value = value ??
            throw new ArgumentNullException(nameof(value));
    }

    internal T Value {
        get {
            lock (_sync) {
                return _value ??
                    throw new ObjectDisposedException(
                        typeof(T).Name);
            }
        }
    }

    internal IDisposable Retain() {
        lock (_sync) {
            if (_value == null || _references == 0) {
                throw new ObjectDisposedException(
                    typeof(T).Name);
            }
            _references++;
            return new Lease(this);
        }
    }

    public void Dispose() {
        if (Interlocked.Exchange(
                ref _ownerReleased,
                1) == 0) {
            Release();
        }
    }

    private void Release() {
        T? dispose = default;
        lock (_sync) {
            if (_references == 0) {
                return;
            }
            _references--;
            if (_references == 0) {
                dispose = _value;
                _value = default;
            }
        }
        dispose?.Dispose();
    }

    private sealed class Lease : IDisposable {
        private RetainedDisposable<T>? _owner;

        internal Lease(
            RetainedDisposable<T> owner) {

            _owner = owner;
        }

        public void Dispose() {
            Interlocked.Exchange(
                ref _owner,
                null)?.Release();
        }
    }
}
