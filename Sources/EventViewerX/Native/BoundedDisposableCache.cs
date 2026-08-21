namespace EventViewerX.Native;

/// <summary>
/// Retains a bounded insertion-ordered set of disposable values.
/// </summary>
internal sealed class BoundedDisposableCache<TKey, TValue> : IDisposable
    where TKey : notnull
    where TValue : IDisposable {
    private readonly int _capacity;
    private readonly Dictionary<TKey, TValue> _values;
    private readonly Queue<TKey> _insertionOrder = new();

    internal BoundedDisposableCache(
        int capacity,
        IEqualityComparer<TKey>? comparer = null) {

        if (capacity <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(capacity));
        }
        _capacity = capacity;
        _values = new Dictionary<TKey, TValue>(
            capacity,
            comparer);
    }

    internal int Count => _values.Count;

    internal TValue GetOrAdd(
        TKey key,
        Func<TValue> factory) {

        if (_values.TryGetValue(
                key,
                out TValue? existing)) {
            return existing;
        }
        TValue value = factory();
        try {
            while (_values.Count >= _capacity) {
                TKey oldest = _insertionOrder.Dequeue();
                if (_values.TryGetValue(
                        oldest,
                        out TValue? evicted)) {
                    _values.Remove(
                        oldest);
                    evicted.Dispose();
                }
            }
            _values.Add(
                key,
                value);
            _insertionOrder.Enqueue(
                key);
            return value;
        } catch {
            value.Dispose();
            throw;
        }
    }

    public void Dispose() {
        foreach (TValue value in _values.Values) {
            value.Dispose();
        }
        _values.Clear();
        _insertionOrder.Clear();
    }
}
