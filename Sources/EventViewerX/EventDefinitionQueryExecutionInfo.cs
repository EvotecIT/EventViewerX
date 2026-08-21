using System.Collections.Concurrent;

namespace EventViewerX;

/// <summary>Reports source progress and expected remote failures for a declarative event query.</summary>
public sealed class EventDefinitionQueryExecutionInfo {
    private readonly ConcurrentDictionary<string, EventLogQueryTargetFailure> _targetFailures =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Number of matching source events read before projection.</summary>
    public long EventsScanned { get; internal set; }

    /// <summary>Number of custom records emitted.</summary>
    public long EventsEmitted { get; internal set; }

    /// <summary>Whether another candidate existed after the configured scan cap.</summary>
    public bool ScanLimitReached { get; internal set; }

    /// <summary>Whether another projected result existed after the configured result cap.</summary>
    public bool ResultLimitReached { get; internal set; }

    /// <summary>Native and managed predicate plan used by this query.</summary>
    public EventPredicatePlan? PredicatePlan { get; internal set; }

    /// <summary>Expected remote-target failures isolated while healthy targets continued.</summary>
    public IReadOnlyList<EventLogQueryTargetFailure> TargetFailures => _targetFailures.Values
        .OrderBy(static failure => failure.MachineName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(static failure => failure.LogName, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    internal void Reset() {
        EventsScanned = 0;
        EventsEmitted = 0;
        ScanLimitReached = false;
        ResultLimitReached = false;
        PredicatePlan = null;
        _targetFailures.Clear();
    }

    internal void RecordTargetFailure(EventLogQueryTargetFailure failure) {
        if (failure == null) {
            return;
        }
        _targetFailures.TryAdd(failure.MachineName + "\0" + failure.LogName, failure);
    }
}
