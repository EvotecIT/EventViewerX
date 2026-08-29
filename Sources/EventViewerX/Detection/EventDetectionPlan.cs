namespace EventViewerX;

/// <summary>Immutable indexed detection plan compiled from native or imported rule definitions.</summary>
public sealed class EventDetectionPlan {
    private readonly Dictionary<int, CompiledRule[]> _byEventId;
    private readonly Dictionary<string, CompiledRule[]> _byType;
    private readonly Dictionary<string, CompiledRule[]> _byChannel;
    private readonly Dictionary<string, CompiledRule[]> _byProvider;
    private readonly CompiledRule[] _withoutEventId;
    private readonly CompiledRule[] _withoutType;
    private readonly CompiledRule[] _withoutChannel;
    private readonly CompiledRule[] _withoutProvider;

    private EventDetectionPlan(
        CompiledRule[] rules,
        Dictionary<int, CompiledRule[]> byEventId,
        Dictionary<string, CompiledRule[]> byType,
        Dictionary<string, CompiledRule[]> byChannel,
        Dictionary<string, CompiledRule[]> byProvider,
        CompiledRule[] withoutEventId,
        CompiledRule[] withoutType,
        CompiledRule[] withoutChannel,
        CompiledRule[] withoutProvider) {

        Rules = Array.AsReadOnly(rules.Select(static rule => rule.Definition.Snapshot()).ToArray());
        CompiledRules = rules;
        _byEventId = byEventId;
        _byType = byType;
        _byChannel = byChannel;
        _byProvider = byProvider;
        _withoutEventId = withoutEventId;
        _withoutType = withoutType;
        _withoutChannel = withoutChannel;
        _withoutProvider = withoutProvider;
        RequiredEventTypes = Array.AsReadOnly(rules
            .SelectMany(static rule => rule.Definition.EventTypes.Concat(
                rule.Definition.Steps.SelectMany(static step => step.EventTypes)))
            .Distinct()
            .ToArray());
    }

    /// <summary>Effective detached rules represented by this plan.</summary>
    public IReadOnlyList<EventDetectionRuleDefinition> Rules { get; }

    /// <summary>Typed projections required to evaluate this plan.</summary>
    public IReadOnlyList<EventType> RequiredEventTypes { get; }

    /// <summary>Returns a detached operator-facing description of selectors and state requirements.</summary>
    public EventDetectionPlanExplanation Explain() => new(
        Rules.Select(static rule => new EventDetectionRulePlanExplanation(
            rule.RuleId,
            rule.Title,
            rule.Kind,
            rule.EventTypes,
            rule.EventIds,
            rule.Channels,
            rule.Providers,
            rule.Window,
            rule.Threshold,
            rule.GroupBy,
            rule.DistinctBy,
            rule.Steps.Select(static step => step.Name).ToArray())).ToArray(),
        RequiredEventTypes);

    internal IReadOnlyList<CompiledRule> CompiledRules { get; }

    /// <summary>Compiles validated rules, selectors, predicates, and tuning into one immutable plan.</summary>
    public static EventDetectionPlan Compile(
        IEnumerable<IEventDetectionRule> rules,
        EventDetectionTuning? tuning = null) {

        if (rules == null) {
            throw new ArgumentNullException(nameof(rules));
        }
        EventDetectionTuningSnapshot tuningSnapshot = EventDetectionTuningSnapshot.Create(tuning);
        var compiled = new List<CompiledRule>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (IEventDetectionRule rule in rules) {
            EventDetectionRuleDefinition definition = rule?.Definition?.Snapshot()
                ?? throw new ArgumentException("Detection rules cannot contain null values.", nameof(rules));
            if (!ids.Add(definition.RuleId)) {
                throw new InvalidDataException($"Duplicate detection rule ID '{definition.RuleId}'.");
            }
            if (tuningSnapshot.DisabledRuleIds.Contains(definition.RuleId)) {
                continue;
            }
            if (tuningSnapshot.SeverityOverrides.TryGetValue(definition.RuleId, out EventDetectionSeverity severity)) {
                definition.Severity = severity;
            }
            if (tuningSnapshot.ThresholdOverrides.TryGetValue(definition.RuleId, out int threshold)) {
                if (definition.Kind != EventDetectionRuleKind.Threshold || threshold < 2) {
                    throw new InvalidDataException(
                        $"Threshold override for '{definition.RuleId}' requires a threshold rule and value of at least two.");
                }
                definition.Threshold = threshold;
            }
            compiled.Add(new CompiledRule(
                compiled.Count,
                definition,
                tuningSnapshot.Suppressions.Where(item => string.Equals(
                    item.RuleId,
                    definition.RuleId,
                    StringComparison.OrdinalIgnoreCase)).ToArray()));
        }

        CompiledRule[] snapshot = compiled.ToArray();
        return new EventDetectionPlan(
            snapshot,
            BuildIndex(snapshot, static rule => rule.IndexEventIds),
            BuildTextIndex(snapshot, static rule => rule.IndexEventTypeNames),
            BuildTextIndex(snapshot, static rule => rule.IndexChannels),
            BuildTextIndex(snapshot, static rule => rule.IndexProviders),
            snapshot.Where(static rule => rule.IndexEventIds.Count == 0).ToArray(),
            snapshot.Where(static rule => rule.IndexEventTypeNames.Count == 0).ToArray(),
            snapshot.Where(static rule => rule.IndexChannels.Count == 0).ToArray(),
            snapshot.Where(static rule => rule.IndexProviders.Count == 0).ToArray());
    }

    internal IReadOnlyList<CompiledRule> GetCandidates(EventObservation observation) {
        _byEventId.TryGetValue(observation.EventId, out CompiledRule[]? byId);
        _byType.TryGetValue(observation.TypeName, out CompiledRule[]? byType);
        _byChannel.TryGetValue(observation.SourceLog, out CompiledRule[]? byChannel);
        _byProvider.TryGetValue(observation.ProviderName, out CompiledRule[]? byProvider);

        CandidateSeed seed = new CandidateSeed(byId, _withoutEventId);
        seed = CandidateSeed.Smaller(seed, new CandidateSeed(byType, _withoutType));
        seed = CandidateSeed.Smaller(seed, new CandidateSeed(byChannel, _withoutChannel));
        seed = CandidateSeed.Smaller(seed, new CandidateSeed(byProvider, _withoutProvider));
        if (seed.Count == 0) {
            return Array.Empty<CompiledRule>();
        }

        var candidates = new HashSet<CompiledRule>();
        Add(candidates, seed.Matches);
        Add(candidates, seed.Unrestricted);
        CompiledRule[] result = candidates
            .Where(rule => rule.MayMatchSelectors(observation))
            .ToArray();
        Array.Sort(result, static (left, right) => left.Ordinal.CompareTo(right.Ordinal));
        return result;
    }

    private static Dictionary<int, CompiledRule[]> BuildIndex(
        IReadOnlyList<CompiledRule> rules,
        Func<CompiledRule, IEnumerable<int>> values) {

        return rules.SelectMany(rule => values(rule).Select(value => (value, rule)))
            .GroupBy(static item => item.value)
            .ToDictionary(
                static group => group.Key,
                static group => group.Select(static item => item.rule).Distinct().ToArray());
    }

    private static Dictionary<string, CompiledRule[]> BuildTextIndex(
        IReadOnlyList<CompiledRule> rules,
        Func<CompiledRule, IEnumerable<string>> values) {

        return rules.SelectMany(rule => values(rule).Select(value => (value, rule)))
            .GroupBy(static item => item.value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static group => group.Key,
                static group => group.Select(static item => item.rule).Distinct().ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static void Add(ISet<CompiledRule> target, IEnumerable<CompiledRule> source) {
        foreach (CompiledRule rule in source) {
            target.Add(rule);
        }
    }

    internal sealed class CompiledRule {
        private readonly Func<IReadOnlyDictionary<string, object?>, bool>? _predicate;
        private readonly CompiledSuppression[] _suppressions;

        internal CompiledRule(
            int ordinal,
            EventDetectionRuleDefinition definition,
            IReadOnlyList<EventDetectionSuppression> suppressions) {

            Ordinal = ordinal;
            Definition = definition;
            EventIds = new HashSet<int>(definition.EventIds);
            EventTypeNames = new HashSet<string>(
                EventTypeCatalog.Expand(definition.EventTypes).Select(static type => type.ToString()),
                StringComparer.OrdinalIgnoreCase);
            Channels = new HashSet<string>(definition.Channels, StringComparer.OrdinalIgnoreCase);
            Providers = new HashSet<string>(definition.Providers, StringComparer.OrdinalIgnoreCase);
            _predicate = definition.Predicate == null
                ? null
                : EventPredicateEvaluator.CompileFields(definition.Predicate);
            _suppressions = suppressions.Select(static suppression => new CompiledSuppression(suppression)).ToArray();
            Steps = definition.Steps.Select(static step => new CompiledStep(step)).ToArray();
            IndexEventIds = BuildIndexValues(EventIds, Steps.Select(static step => step.EventIds));
            IndexEventTypeNames = BuildIndexValues(
                EventTypeNames,
                Steps.Select(static step => step.EventTypeNames),
                StringComparer.OrdinalIgnoreCase);
            IndexChannels = BuildIndexValues(
                Channels,
                Steps.Select(static step => step.Channels),
                StringComparer.OrdinalIgnoreCase);
            IndexProviders = BuildIndexValues(
                Providers,
                Steps.Select(static step => step.Providers),
                StringComparer.OrdinalIgnoreCase);
        }

        internal EventDetectionRuleDefinition Definition { get; }
        internal int Ordinal { get; }
        internal HashSet<int> EventIds { get; }
        internal HashSet<string> EventTypeNames { get; }
        internal HashSet<string> Channels { get; }
        internal HashSet<string> Providers { get; }
        internal CompiledStep[] Steps { get; }
        internal HashSet<int> IndexEventIds { get; }
        internal HashSet<string> IndexEventTypeNames { get; }
        internal HashSet<string> IndexChannels { get; }
        internal HashSet<string> IndexProviders { get; }
        internal bool MayMatchSelectors(EventObservation observation) =>
            (IndexEventIds.Count == 0 || IndexEventIds.Contains(observation.EventId)) &&
            (IndexEventTypeNames.Count == 0 || IndexEventTypeNames.Contains(observation.TypeName)) &&
            (IndexChannels.Count == 0 || IndexChannels.Contains(observation.SourceLog)) &&
            (IndexProviders.Count == 0 || IndexProviders.Contains(observation.ProviderName));

        internal bool Matches(EventObservation observation) {
            return (EventIds.Count == 0 || EventIds.Contains(observation.EventId)) &&
                   (EventTypeNames.Count == 0 || EventTypeNames.Contains(observation.TypeName)) &&
                   (Channels.Count == 0 || Channels.Contains(observation.SourceLog)) &&
                   (Providers.Count == 0 || Providers.Contains(observation.ProviderName)) &&
                   (_predicate == null || _predicate(observation.Fields));
        }

        internal bool IsSuppressed(EventObservation observation) =>
            _suppressions.Any(suppression => suppression.Matches(observation));

        internal int[] GetMatchingStepIndexes(EventObservation observation) {
            if (!Matches(observation)) {
                return Array.Empty<int>();
            }
            return Steps.Select(static (step, index) => (step, index))
                .Where(item => item.step.Matches(observation))
                .Select(static item => item.index)
                .ToArray();
        }

        private static HashSet<T> BuildIndexValues<T>(
            HashSet<T> ruleValues,
            IEnumerable<HashSet<T>> stepValues,
            IEqualityComparer<T>? comparer = null) {

            if (ruleValues.Count != 0) {
                return new HashSet<T>(ruleValues, comparer ?? EqualityComparer<T>.Default);
            }
            HashSet<T>[] steps = stepValues.ToArray();
            if (steps.Length == 0 || steps.Any(static values => values.Count == 0)) {
                return new HashSet<T>(comparer ?? EqualityComparer<T>.Default);
            }
            return new HashSet<T>(steps.SelectMany(static values => values), comparer ?? EqualityComparer<T>.Default);
        }
    }

    private readonly struct CandidateSeed {
        internal CandidateSeed(CompiledRule[]? matches, CompiledRule[] unrestricted) {
            Matches = matches ?? Array.Empty<CompiledRule>();
            Unrestricted = unrestricted;
        }

        internal CompiledRule[] Matches { get; }
        internal CompiledRule[] Unrestricted { get; }
        internal int Count => Matches.Length + Unrestricted.Length;

        internal static CandidateSeed Smaller(CandidateSeed left, CandidateSeed right) =>
            right.Count < left.Count ? right : left;
    }

    internal sealed class CompiledStep {
        private readonly Func<IReadOnlyDictionary<string, object?>, bool>? _predicate;

        internal CompiledStep(EventDetectionStepDefinition definition) {
            Definition = definition;
            EventIds = new HashSet<int>(definition.EventIds);
            EventTypeNames = new HashSet<string>(
                EventTypeCatalog.Expand(definition.EventTypes).Select(static type => type.ToString()),
                StringComparer.OrdinalIgnoreCase);
            Channels = new HashSet<string>(definition.Channels, StringComparer.OrdinalIgnoreCase);
            Providers = new HashSet<string>(definition.Providers, StringComparer.OrdinalIgnoreCase);
            _predicate = definition.Predicate == null
                ? null
                : EventPredicateEvaluator.CompileFields(definition.Predicate);
        }

        internal EventDetectionStepDefinition Definition { get; }
        internal HashSet<int> EventIds { get; }
        internal HashSet<string> EventTypeNames { get; }
        internal HashSet<string> Channels { get; }
        internal HashSet<string> Providers { get; }

        internal bool Matches(EventObservation observation) =>
            (EventIds.Count == 0 || EventIds.Contains(observation.EventId)) &&
            (EventTypeNames.Count == 0 || EventTypeNames.Contains(observation.TypeName)) &&
            (Channels.Count == 0 || Channels.Contains(observation.SourceLog)) &&
            (Providers.Count == 0 || Providers.Contains(observation.ProviderName)) &&
            (_predicate == null || _predicate(observation.Fields));
    }

    private sealed class CompiledSuppression {
        private readonly Func<IReadOnlyDictionary<string, object?>, bool> _predicate;

        internal CompiledSuppression(EventDetectionSuppression suppression) {
            RuleId = suppression.RuleId;
            StartTimeUtc = suppression.StartTimeUtc?.ToUniversalTime();
            EndTimeUtc = suppression.EndTimeUtc?.ToUniversalTime();
            Reason = suppression.Reason?.Trim() ?? string.Empty;
            _predicate = EventPredicateEvaluator.CompileFields(suppression.Predicate);
        }

        internal string RuleId { get; }
        internal DateTime? StartTimeUtc { get; }
        internal DateTime? EndTimeUtc { get; }
        internal string Reason { get; }

        internal bool Matches(EventObservation observation) =>
            (!StartTimeUtc.HasValue || observation.EventTimeUtc >= StartTimeUtc.Value) &&
            (!EndTimeUtc.HasValue || observation.EventTimeUtc <= EndTimeUtc.Value) &&
            _predicate(observation.Fields);
    }

    private sealed class EventDetectionTuningSnapshot {
        private EventDetectionTuningSnapshot(
            HashSet<string> disabledRuleIds,
            Dictionary<string, EventDetectionSeverity> severityOverrides,
            Dictionary<string, int> thresholdOverrides,
            EventDetectionSuppression[] suppressions) {

            DisabledRuleIds = disabledRuleIds;
            SeverityOverrides = severityOverrides;
            ThresholdOverrides = thresholdOverrides;
            Suppressions = suppressions;
        }

        internal HashSet<string> DisabledRuleIds { get; }
        internal Dictionary<string, EventDetectionSeverity> SeverityOverrides { get; }
        internal Dictionary<string, int> ThresholdOverrides { get; }
        internal EventDetectionSuppression[] Suppressions { get; }

        internal static EventDetectionTuningSnapshot Create(EventDetectionTuning? tuning) {
            tuning ??= new EventDetectionTuning();
            var disabled = new HashSet<string>(
                (tuning.DisabledRuleIds ?? Array.Empty<string>())
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .Select(static value => value.Trim()),
                StringComparer.OrdinalIgnoreCase);
            var severity = new Dictionary<string, EventDetectionSeverity>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, EventDetectionSeverity> item in tuning.SeverityOverrides ??
                     new Dictionary<string, EventDetectionSeverity>()) {
                if (string.IsNullOrWhiteSpace(item.Key) || !Enum.IsDefined(typeof(EventDetectionSeverity), item.Value)) {
                    throw new InvalidDataException("Severity overrides require a rule ID and supported severity.");
                }
                severity[item.Key.Trim()] = item.Value;
            }
            var thresholds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, int> item in tuning.ThresholdOverrides ?? new Dictionary<string, int>()) {
                if (string.IsNullOrWhiteSpace(item.Key) || item.Value < 2) {
                    throw new InvalidDataException("Threshold overrides require a rule ID and value of at least two.");
                }
                thresholds[item.Key.Trim()] = item.Value;
            }
            EventDetectionSuppression[] suppressions = (tuning.Suppressions ?? Array.Empty<EventDetectionSuppression>())
                .Select(CloneSuppression)
                .ToArray();
            return new EventDetectionTuningSnapshot(disabled, severity, thresholds, suppressions);
        }

        private static EventDetectionSuppression CloneSuppression(EventDetectionSuppression source) {
            if (source == null || string.IsNullOrWhiteSpace(source.RuleId) || source.Predicate == null) {
                throw new InvalidDataException("Suppressions require RuleId and Predicate.");
            }
            source.Predicate.Validate();
            DateTime? start = source.StartTimeUtc?.ToUniversalTime();
            DateTime? end = source.EndTimeUtc?.ToUniversalTime();
            if (start.HasValue && end.HasValue && start > end) {
                throw new InvalidDataException("Suppression start time cannot be later than end time.");
            }
            return new EventDetectionSuppression {
                RuleId = source.RuleId.Trim(),
                Predicate = source.Predicate.Clone(),
                StartTimeUtc = start,
                EndTimeUtc = end,
                Reason = source.Reason?.Trim() ?? string.Empty
            };
        }
    }
}
