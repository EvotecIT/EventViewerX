using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace EventViewerX;

/// <summary>Immutable environment-specific overrides applied while compiling a detection plan.</summary>
public sealed class EventDetectionTuning {
    /// <summary>Creates a detached, immutable tuning contract.</summary>
    [JsonConstructor]
    public EventDetectionTuning(
        IReadOnlyList<string>? disabledRuleIds = null,
        IReadOnlyDictionary<string, EventDetectionSeverity>? severityOverrides = null,
        IReadOnlyDictionary<string, int>? thresholdOverrides = null,
        IReadOnlyList<EventDetectionSuppression>? suppressions = null) {

        DisabledRuleIds = Array.AsReadOnly((disabledRuleIds ?? Array.Empty<string>()).ToArray());
        SeverityOverrides = new ReadOnlyDictionary<string, EventDetectionSeverity>(
            (severityOverrides ?? new Dictionary<string, EventDetectionSeverity>())
                .ToDictionary(static item => item.Key, static item => item.Value, StringComparer.OrdinalIgnoreCase));
        ThresholdOverrides = new ReadOnlyDictionary<string, int>(
            (thresholdOverrides ?? new Dictionary<string, int>())
                .ToDictionary(static item => item.Key, static item => item.Value, StringComparer.OrdinalIgnoreCase));
        Suppressions = Array.AsReadOnly((suppressions ?? Array.Empty<EventDetectionSuppression>()).ToArray());
    }

    /// <summary>Rule identifiers omitted from the compiled plan.</summary>
    public IReadOnlyList<string> DisabledRuleIds { get; }
    /// <summary>Severity overrides keyed by stable rule identifier.</summary>
    public IReadOnlyDictionary<string, EventDetectionSeverity> SeverityOverrides { get; }
    /// <summary>Threshold overrides keyed by stable rule identifier.</summary>
    public IReadOnlyDictionary<string, int> ThresholdOverrides { get; }
    /// <summary>Predicate-based finding suppressions.</summary>
    public IReadOnlyList<EventDetectionSuppression> Suppressions { get; }
}

/// <summary>Immutable suppression of matching observations without changing signed rule content.</summary>
public sealed class EventDetectionSuppression {
    /// <summary>Creates a detached suppression contract.</summary>
    [JsonConstructor]
    public EventDetectionSuppression(
        string ruleId,
        EventPredicate predicate,
        DateTime? startTimeUtc = null,
        DateTime? endTimeUtc = null,
        string? reason = null) {

        RuleId = ruleId?.Trim() ?? string.Empty;
        Predicate = predicate?.Clone() ?? throw new ArgumentNullException(nameof(predicate));
        StartTimeUtc = startTimeUtc?.ToUniversalTime();
        EndTimeUtc = endTimeUtc?.ToUniversalTime();
        Reason = reason?.Trim() ?? string.Empty;
    }

    /// <summary>Stable rule identifier.</summary>
    public string RuleId { get; }
    /// <summary>Predicate selecting observations to suppress.</summary>
    public EventPredicate Predicate { get; }
    /// <summary>Optional UTC start of the suppression window.</summary>
    public DateTime? StartTimeUtc { get; }
    /// <summary>Optional UTC end of the suppression window.</summary>
    public DateTime? EndTimeUtc { get; }
    /// <summary>Operator-facing reason.</summary>
    public string Reason { get; }
}

/// <summary>Fluent builder for immutable <see cref="EventDetectionTuning"/> contracts.</summary>
public sealed class EventDetectionTuningBuilder {
    private readonly HashSet<string> _disabled = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, EventDetectionSeverity> _severity = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _thresholds = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<EventDetectionSuppression> _suppressions = new();

    /// <summary>Disables a stable rule identifier.</summary>
    public EventDetectionTuningBuilder Disable(string ruleId) {
        _disabled.Add(RequiredRuleId(ruleId));
        return this;
    }

    /// <summary>Overrides one rule severity.</summary>
    public EventDetectionTuningBuilder OverrideSeverity(string ruleId, EventDetectionSeverity severity) {
        _severity[RequiredRuleId(ruleId)] = severity;
        return this;
    }

    /// <summary>Overrides one threshold or distinct-value count.</summary>
    public EventDetectionTuningBuilder OverrideThreshold(string ruleId, int threshold) {
        _thresholds[RequiredRuleId(ruleId)] = threshold;
        return this;
    }

    /// <summary>Adds a predicate-based suppression.</summary>
    public EventDetectionTuningBuilder Suppress(
        string ruleId,
        EventPredicate predicate,
        string reason,
        DateTime? startTimeUtc = null,
        DateTime? endTimeUtc = null) {

        _suppressions.Add(new EventDetectionSuppression(
            RequiredRuleId(ruleId),
            predicate,
            startTimeUtc,
            endTimeUtc,
            reason));
        return this;
    }

    /// <summary>Builds an immutable detached tuning contract.</summary>
    public EventDetectionTuning Build() => new(
        _disabled.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
        _severity,
        _thresholds,
        _suppressions);

    private static string RequiredRuleId(string value) {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length == 0
            ? throw new ArgumentException("A rule ID is required.", nameof(value))
            : normalized;
    }
}
