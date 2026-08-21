using System.Collections;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventLogCatalogPatterns {
    [Fact]
    public void ChannelPatternsAreMaterializedExactlyOnce() {
        if (!OperatingSystem.IsWindows()) {
            return;
        }

        var patterns = new SingleUseEnumerable<string>(
            new[] { "Application" });

        IReadOnlyList<string> channels =
            EventLogCatalog.GetChannelNames(
                channelPatterns: patterns);

        Assert.Equal(
            new[] { "Application" },
            channels,
            StringComparer.OrdinalIgnoreCase);
    }

    private sealed class SingleUseEnumerable<T> : IEnumerable<T> {
        private readonly IEnumerable<T> _values;
        private int _enumerated;

        internal SingleUseEnumerable(
            IEnumerable<T> values) {

            _values = values;
        }

        public IEnumerator<T> GetEnumerator() {
            if (Interlocked.Exchange(
                    ref _enumerated,
                    1) != 0) {
                throw new InvalidOperationException(
                    "The sequence was enumerated more than once.");
            }
            return _values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }
}
