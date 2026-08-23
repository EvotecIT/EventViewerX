using System.Collections.Concurrent;

namespace EventViewerX;

/// <summary>
/// Reports candidate-scan progress for an event-type query.
/// </summary>
public sealed class EventTypeQueryExecutionInfo {
    private readonly ConcurrentDictionary<string, EventLogQueryTargetFailure> _targetFailures =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Number of candidate event records evaluated by event-type rules.
    /// </summary>
    public long EventsScanned { get; internal set; }

    /// <summary>
    /// Number of typed event matches emitted to the caller.
    /// </summary>
    public long EventsEmitted { get; internal set; }

    /// <summary>
    /// Effective candidate scan cap. Zero means unlimited.
    /// </summary>
    public long MaxEventsScanned { get; internal set; }

    /// <summary>
    /// Indicates that another candidate existed beyond <see cref="MaxEventsScanned"/>.
    /// </summary>
    public bool ScanLimitReached { get; internal set; }

    /// <summary>
    /// Indicates that another projected result existed after the configured result cap.
    /// </summary>
    public bool ResultLimitReached { get; internal set; }

    /// <summary>Native and managed predicate plan used by this query.</summary>
    public EventPredicatePlan? PredicatePlan { get; internal set; }

    /// <summary>
    /// Remote targets that could not be queried. Healthy targets may still have emitted results.
    /// </summary>
    public IReadOnlyList<EventLogQueryTargetFailure> TargetFailures => _targetFailures.Values
        .OrderBy(static failure => failure.MachineName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(static failure => failure.LogName, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    internal void Reset(long maxEventsScanned) {
        EventsScanned = 0;
        EventsEmitted = 0;
        MaxEventsScanned = maxEventsScanned;
        ScanLimitReached = false;
        ResultLimitReached = false;
        PredicatePlan = null;
        _targetFailures.Clear();
    }

    internal void RecordTargetFailure(EventLogQueryTargetFailure failure) {
        if (failure == null ||
            string.IsNullOrWhiteSpace(failure.MachineName) ||
            string.IsNullOrWhiteSpace(failure.LogName) ||
            failure.Kind == EventLogRemoteQueryFailureKind.None) {
            return;
        }

        _targetFailures.TryAdd(failure.MachineName + "\0" + failure.LogName, failure);
    }
}

internal sealed class EventTypeCandidateCounter {
    private readonly EventTypeQueryExecutionInfo _executionInfo;
    private readonly long _maxEventsScanned;
    private long _eventsScanned;

    internal EventTypeCandidateCounter(
        long maxEventsScanned,
        EventTypeQueryExecutionInfo executionInfo) {

        _maxEventsScanned = maxEventsScanned;
        _executionInfo = executionInfo;
    }

    internal bool TryRecordCandidate() {
        if (_maxEventsScanned > 0 &&
            _eventsScanned >= _maxEventsScanned) {
            _executionInfo.ScanLimitReached = true;
            return false;
        }

        _eventsScanned++;
        _executionInfo.EventsScanned = _eventsScanned;
        return true;
    }
}
