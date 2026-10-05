namespace EventViewerX;

/// <summary>Safety bounds for one stateful detection execution.</summary>
public sealed class EventDetectionEngineOptions {
    /// <summary>Creates a bounded execution with an explicit live event-time ordering policy.</summary>
    public EventDetectionEngineOptions(EventTimeOrderingOptions eventTimeOrdering,
        long maximumObservations = 1_000_000, int maximumGroups = 25_000,
        int maximumStateObservations = 250_000, long maximumStateBytes = 256L * 1024 * 1024,
        int maximumCandidateRules = 10_000, EventDetectionCoverage? coverage = null)
        : this(maximumObservations, maximumGroups, maximumStateObservations,
            maximumStateBytes, maximumCandidateRules, coverage) {
        EventTimeOrdering = eventTimeOrdering ?? throw new ArgumentNullException(nameof(eventTimeOrdering));
    }

    /// <summary>Optional bounded live event-time ordering. Null evaluates observations immediately.</summary>
    public EventTimeOrderingOptions? EventTimeOrdering { get; }
    /// <summary>Final evaluation time and exact collection receipts used to confirm absent completions.</summary>
    public EventDetectionAbsenceWindow? AbsenceWindow { get; private set; }

    /// <summary>Returns detached options with explicit time-bounded absence coverage.</summary>
    public EventDetectionEngineOptions WithAbsenceWindow(EventDetectionAbsenceWindow? window) {
        EventDetectionEngineOptions copy = EventTimeOrdering == null
            ? new EventDetectionEngineOptions(MaximumObservations, MaximumGroups, MaximumStateObservations, MaximumStateBytes, MaximumCandidateRules, Coverage)
            : new EventDetectionEngineOptions(EventTimeOrdering, MaximumObservations, MaximumGroups, MaximumStateObservations, MaximumStateBytes, MaximumCandidateRules, Coverage);
        copy.AbsenceWindow = window;
        return copy;
    }
    /// <summary>Creates an immutable validated detection execution contract.</summary>
    public EventDetectionEngineOptions(
        long maximumObservations = 1_000_000,
        int maximumGroups = 25_000,
        int maximumStateObservations = 250_000,
        long maximumStateBytes = 256L * 1024L * 1024L,
        int maximumCandidateRules = 10_000,
        EventDetectionCoverage? coverage = null) {

        if (maximumObservations < 0) {
            throw new ArgumentOutOfRangeException(nameof(maximumObservations));
        }
        if (maximumGroups <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maximumGroups));
        }
        if (maximumStateObservations <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maximumStateObservations));
        }
        if (maximumStateBytes <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maximumStateBytes));
        }
        if (maximumCandidateRules <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maximumCandidateRules));
        }
        MaximumObservations = maximumObservations;
        MaximumGroups = maximumGroups;
        MaximumStateObservations = maximumStateObservations;
        MaximumStateBytes = maximumStateBytes;
        MaximumCandidateRules = maximumCandidateRules;
        Coverage = coverage?.Snapshot();
    }

    /// <summary>Maximum observations accepted by one evaluator. Zero is unlimited.</summary>
    public long MaximumObservations { get; }
    /// <summary>Maximum threshold groups retained at once.</summary>
    public int MaximumGroups { get; }
    /// <summary>Maximum evidence observations retained across all threshold groups.</summary>
    public int MaximumStateObservations { get; }
    /// <summary>Maximum estimated bytes retained by stateful rules.</summary>
    public long MaximumStateBytes { get; }
    /// <summary>Maximum candidate rules evaluated for one observation.</summary>
    public int MaximumCandidateRules { get; }
    /// <summary>Expected and successfully collected data sources for this execution window.</summary>
    public EventDetectionCoverage? Coverage { get; }
}

/// <summary>Fluent builder for immutable <see cref="EventDetectionEngineOptions"/>.</summary>
public sealed class EventDetectionEngineOptionsBuilder {
    private long _maximumObservations = 1_000_000;
    private int _maximumGroups = 25_000;
    private int _maximumStateObservations = 250_000;
    private long _maximumStateBytes = 256L * 1024L * 1024L;
    private int _maximumCandidateRules = 10_000;
    private EventDetectionCoverage? _coverage;
    private EventTimeOrderingOptions? _eventTimeOrdering;

    /// <summary>Sets the bounded live event-time ordering policy.</summary>
    public EventDetectionEngineOptionsBuilder WithEventTimeOrdering(EventTimeOrderingOptions? value) {
        _eventTimeOrdering = value;
        return this;
    }

    /// <summary>Sets the maximum observations accepted by one evaluator.</summary>
    public EventDetectionEngineOptionsBuilder WithMaximumObservations(long value) {
        _maximumObservations = value;
        return this;
    }

    /// <summary>Sets the maximum threshold groups retained at once.</summary>
    public EventDetectionEngineOptionsBuilder WithMaximumGroups(int value) {
        _maximumGroups = value;
        return this;
    }

    /// <summary>Sets the maximum evidence observations retained in state.</summary>
    public EventDetectionEngineOptionsBuilder WithMaximumStateObservations(int value) {
        _maximumStateObservations = value;
        return this;
    }

    /// <summary>Sets the maximum estimated state bytes.</summary>
    public EventDetectionEngineOptionsBuilder WithMaximumStateBytes(long value) {
        _maximumStateBytes = value;
        return this;
    }

    /// <summary>Sets the maximum candidate rules evaluated per observation.</summary>
    public EventDetectionEngineOptionsBuilder WithMaximumCandidateRules(int value) {
        _maximumCandidateRules = value;
        return this;
    }

    /// <summary>Sets expected-versus-observed collection coverage.</summary>
    public EventDetectionEngineOptionsBuilder WithCoverage(EventDetectionCoverage? value) {
        _coverage = value;
        return this;
    }

    /// <summary>Builds an immutable validated execution contract.</summary>
    public EventDetectionEngineOptions Build() => _eventTimeOrdering == null ? new(
        _maximumObservations,
        _maximumGroups,
        _maximumStateObservations,
        _maximumStateBytes,
        _maximumCandidateRules,
        _coverage) : new(_eventTimeOrdering, _maximumObservations, _maximumGroups,
            _maximumStateObservations, _maximumStateBytes, _maximumCandidateRules, _coverage);
}
