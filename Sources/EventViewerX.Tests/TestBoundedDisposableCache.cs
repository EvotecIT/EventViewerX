using EventViewerX.Native;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestBoundedDisposableCache {
    [Fact]
    public void EvictsAndDisposesTheOldestValueAtCapacity() {
        var first = new TestDisposable();
        var second = new TestDisposable();
        using var cache =
            new BoundedDisposableCache<string, TestDisposable>(
                1,
                StringComparer.OrdinalIgnoreCase);

        Assert.Same(
            first,
            cache.GetOrAdd(
                "Provider-A",
                () => first));
        Assert.Same(
            first,
            cache.GetOrAdd(
                "provider-a",
                () => throw new InvalidOperationException()));
        Assert.Same(
            second,
            cache.GetOrAdd(
                "Provider-B",
                () => second));

        Assert.True(
            first.IsDisposed);
        Assert.False(
            second.IsDisposed);
        Assert.Equal(
            1,
            cache.Count);
    }

    private sealed class TestDisposable : IDisposable {
        internal bool IsDisposed { get; private set; }

        public void Dispose() {
            IsDisposed = true;
        }
    }
}
