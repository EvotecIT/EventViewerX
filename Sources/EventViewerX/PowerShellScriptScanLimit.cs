namespace EventViewerX;

internal sealed class PowerShellScriptScanLimit {
    private readonly int _maximumEvents;

    internal PowerShellScriptScanLimit(int maximumEvents) {
        if (maximumEvents < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(maximumEvents),
                "Maximum scanned events must be greater than or equal to zero.");
        }
        _maximumEvents = maximumEvents;
    }

    internal int EventsScanned { get; private set; }

    internal bool LimitReached { get; private set; }

    internal long NativeReadLimit =>
        _maximumEvents > 0
            ? (long)_maximumEvents + 1L
            : 0L;

    internal bool TryAcceptCandidate() {
        if (_maximumEvents > 0 &&
            EventsScanned >= _maximumEvents) {
            LimitReached = true;
            return false;
        }
        EventsScanned++;
        return true;
    }
}
