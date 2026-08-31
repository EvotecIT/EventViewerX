using System.Globalization;
using System.Runtime.CompilerServices;

namespace EventViewerX;

/// <summary>Executes immutable detection plans against live or offline observation streams.</summary>
public static partial class EventDetectionEngine {
    /// <summary>Explains every rule decision for one observation without changing stateful evaluator state.</summary>
    public static IReadOnlyList<EventDetectionRuleTrace> Explain(
        EventObservation observation,
        EventDetectionPlan plan,
        EventDetectionCoverage? coverage = null) {

        if (observation == null) {
            throw new ArgumentNullException(nameof(observation));
        }
        if (plan == null) {
            throw new ArgumentNullException(nameof(plan));
        }
        EventDetectionCoverage effectiveCoverage = coverage?.Snapshot() ?? EventDetectionCoverage.Unknown();
        return plan.CompiledRules.Select(rule => CreateTrace(rule, observation, effectiveCoverage)).ToArray();
    }

    private static EventDetectionRuleTrace CreateTrace(
        EventDetectionPlan.CompiledRule rule,
        EventObservation observation,
        EventDetectionCoverage coverage) {

        bool eventId = rule.EventIds.Count == 0 || rule.EventIds.Contains(observation.EventId);
        bool type = rule.EventTypeNames.Count == 0 || rule.EventTypeNames.Contains(observation.TypeName);
        bool channel = rule.Channels.Count == 0 || rule.Channels.Contains(observation.SourceLog);
        bool provider = rule.Providers.Count == 0 || rule.Providers.Contains(observation.ProviderName);
        bool predicate = eventId && type && channel && provider && rule.MatchesPredicate(observation);
        bool accepted = eventId && type && channel && provider && predicate;
        bool suppressed = accepted && rule.IsSuppressed(observation);
        string[] matchingSteps = accepted
            ? rule.GetMatchingStepIndexes(observation)
                .Select(index => rule.Steps[index].Definition.Name)
                .ToArray()
            : Array.Empty<string>();
        var conditions = new[] {
            Condition("EventId", eventId, rule.EventIds.Count == 0
                ? "No event-ID restriction."
                : $"Observed {observation.EventId}; required {string.Join(",", rule.EventIds.OrderBy(static value => value))}."),
            Condition("EventType", type, rule.EventTypeNames.Count == 0
                ? "No typed-projection restriction."
                : $"Observed {observation.TypeName}; required {string.Join(",", rule.EventTypeNames.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase))}."),
            Condition("Channel", channel, rule.Channels.Count == 0
                ? "No source-channel restriction."
                : $"Observed {observation.SourceLog}; required {string.Join(",", rule.Channels.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase))}."),
            Condition("Provider", provider, rule.Providers.Count == 0
                ? "No provider restriction."
                : $"Observed {observation.ProviderName}; required {string.Join(",", rule.Providers.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase))}."),
            Condition("Predicate", predicate, predicate
                ? "The semantic field predicate matched or the rule has no predicate."
                : "The semantic field predicate did not match, or a preceding selector rejected the observation."),
            Condition("Suppression", !suppressed, suppressed
                ? "Environment tuning suppressed this otherwise matching observation."
                : "No tuning suppression matched."),
            Condition("Coverage", coverage.IsComplete, coverage.IsComplete
                ? "Declared collection coverage is complete."
                : string.Join(" ", coverage.Failures.Concat(new[] { "Required source coverage is incomplete or undeclared." })))
        };
        string outcome;
        if (!coverage.IsComplete) {
            outcome = "Evidence unavailable or collection coverage incomplete.";
        } else if (!accepted) {
            EventDetectionConditionResult failed = conditions.First(static condition => !condition.Satisfied);
            outcome = $"Rejected by {failed.Condition}: {failed.Detail}";
        } else if (suppressed) {
            outcome = "Matched selectors and predicate but was suppressed by tuning.";
        } else if (rule.Definition.Kind == EventDetectionRuleKind.Stateless) {
            outcome = "Matched all selectors and the semantic predicate.";
        } else {
            outcome = "Accepted as a stateful candidate; the complete rule depends on bounded correlation state.";
        }
        return new EventDetectionRuleTrace(
            rule.Definition.RuleId,
            rule.Definition.Title,
            observation.Identity,
            rule.Definition.Kind,
            accepted,
            suppressed,
            outcome,
            matchingSteps,
            conditions);
    }

    private static EventDetectionConditionResult Condition(string name, bool satisfied, string detail) =>
        new(name, satisfied, detail);
    /// <summary>Projects raw events once and streams findings without requiring storage.</summary>
    public static IEnumerable<EventDetectionFinding> Stream(
        IEnumerable<EventObject> events,
        EventDetectionPlan plan,
        EventDetectionEngineOptions? options = null) {

        if (events == null) {
            throw new ArgumentNullException(nameof(events));
        }
        if (plan == null) {
            throw new ArgumentNullException(nameof(plan));
        }
        EventTypeProjectionPlan? projectionPlan = plan.RequiredEventTypes.Count == 0
            ? null
            : EventTypeCatalog.CompileProjectionPlan(plan.RequiredEventTypes);
        IEnumerable<EventObservation> observations = events.Select(source => {
            if (source == null) {
                throw new ArgumentException("Events cannot contain null values.", nameof(events));
            }
            EventTypeRecord? typed = projectionPlan == null
                ? null
                : EventTypeCatalog.CreateEventRule(source, projectionPlan);
            return EventObservation.Create(source, typed);
        });
        return Stream(observations, plan, options);
    }

    /// <summary>Streams findings while evaluating an ordered observation sequence.</summary>
    public static IEnumerable<EventDetectionFinding> Stream(
        IEnumerable<EventObservation> observations,
        EventDetectionPlan plan,
        EventDetectionEngineOptions? options = null) {

        if (observations == null) {
            throw new ArgumentNullException(nameof(observations));
        }
        var evaluator = new Evaluator(plan, options);
        foreach (EventObservation observation in observations) {
            if (observation == null) {
                throw new ArgumentException("Observations cannot contain null values.", nameof(observations));
            }
            foreach (EventDetectionFinding finding in evaluator.Process(observation)) {
                yield return finding;
            }
        }
    }

    /// <summary>Streams findings from an asynchronous live or offline observation source.</summary>
    public static async IAsyncEnumerable<EventDetectionFinding> StreamAsync(
        IAsyncEnumerable<EventObservation> observations,
        EventDetectionPlan plan,
        EventDetectionEngineOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {

        if (observations == null) {
            throw new ArgumentNullException(nameof(observations));
        }
        var evaluator = new Evaluator(plan, options);
        await foreach (EventObservation observation in observations
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false)) {
            if (observation == null) {
                throw new ArgumentException("Observations cannot contain null values.", nameof(observations));
            }
            foreach (EventDetectionFinding finding in evaluator.Process(observation)) {
                yield return finding;
            }
        }
    }

    /// <summary>Projects an asynchronous raw event stream once and emits findings as they occur.</summary>
    public static async IAsyncEnumerable<EventDetectionFinding> StreamAsync(
        IAsyncEnumerable<EventObject> events,
        EventDetectionPlan plan,
        EventDetectionEngineOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {

        if (events == null) {
            throw new ArgumentNullException(nameof(events));
        }
        if (plan == null) {
            throw new ArgumentNullException(nameof(plan));
        }
        EventTypeProjectionPlan? projectionPlan = plan.RequiredEventTypes.Count == 0
            ? null
            : EventTypeCatalog.CompileProjectionPlan(plan.RequiredEventTypes);
        var evaluator = new Evaluator(plan, options);
        await foreach (EventObject source in events.WithCancellation(cancellationToken).ConfigureAwait(false)) {
            if (source == null) {
                throw new ArgumentException("Events cannot contain null values.", nameof(events));
            }
            EventTypeRecord? typed = projectionPlan == null
                ? null
                : EventTypeCatalog.CreateEventRule(source, projectionPlan);
            EventObservation observation = EventObservation.Create(source, typed);
            foreach (EventDetectionFinding finding in evaluator.Process(observation)) {
                yield return finding;
            }
        }
    }

    /// <summary>Materializes a bounded observation dry run for diagnostics and tests.</summary>
    public static EventDetectionExecutionResult Evaluate(
        IEnumerable<EventObservation> observations,
        EventDetectionPlan plan,
        EventDetectionEngineOptions? options = null) {

        if (observations == null) {
            throw new ArgumentNullException(nameof(observations));
        }
        EventObservation[] snapshot = SnapshotBounded(observations, options?.MaximumObservations ?? 1_000_000);
        if (snapshot.Any(static observation => observation == null)) {
            throw new ArgumentException("Observations cannot contain null values.", nameof(observations));
        }
        Array.Sort(snapshot, CompareObservations);
        EventDetectionCoverage coverage = options?.Coverage?.Snapshot() ?? EventDetectionCoverage.Unknown();
        EventDetectionFinding[] findings = Stream(snapshot, plan, options).ToArray();
        return new EventDetectionExecutionResult(
            GetEvaluatedItems(snapshot, options?.MaximumObservations ?? 1_000_000),
            findings,
            coverage);
    }

    /// <summary>Projects raw events and materializes a bounded dry run.</summary>
    public static EventDetectionExecutionResult Evaluate(
        IEnumerable<EventObject> events,
        EventDetectionPlan plan,
        EventDetectionEngineOptions? options = null) {

        if (events == null) {
            throw new ArgumentNullException(nameof(events));
        }
        EventObject[] snapshot = SnapshotBounded(events, options?.MaximumObservations ?? 1_000_000);
        if (snapshot.Any(static source => source == null)) {
            throw new ArgumentException("Events cannot contain null values.", nameof(events));
        }
        Array.Sort(snapshot, CompareEvents);
        EventTypeProjectionPlan? projectionPlan = plan.RequiredEventTypes.Count == 0
            ? null
            : EventTypeCatalog.CompileProjectionPlan(plan.RequiredEventTypes);
        EventObservation[] observations = snapshot.Select(source => {
            EventTypeRecord? typed = projectionPlan == null
                ? null
                : EventTypeCatalog.CreateEventRule(source, projectionPlan);
            return EventObservation.Create(source, typed);
        }).ToArray();
        EventDetectionFinding[] findings = Stream(observations, plan, options).ToArray();
        return new EventDetectionExecutionResult(
            GetEvaluatedItems(observations, options?.MaximumObservations ?? 1_000_000),
            findings,
            options?.Coverage?.Snapshot() ?? EventDetectionCoverage.Unknown());
    }

    private static T[] GetEvaluatedItems<T>(T[] snapshot, long maximumObservations) {
        if (maximumObservations == 0 || snapshot.LongLength <= maximumObservations) {
            return snapshot;
        }
        return snapshot.Take(checked((int)maximumObservations)).ToArray();
    }

    private static T[] SnapshotBounded<T>(IEnumerable<T> source, long maximumObservations) {
        if (maximumObservations < 0) {
            throw new ArgumentOutOfRangeException(nameof(maximumObservations));
        }
        var snapshot = new List<T>();
        foreach (T item in source) {
            snapshot.Add(item);
            if (maximumObservations > 0 && snapshot.Count > maximumObservations) {
                break;
            }
        }
        return snapshot.ToArray();
    }

    private static int CompareObservations(EventObservation? left, EventObservation? right) {
        if (ReferenceEquals(left, right)) {
            return 0;
        }
        if (left == null) {
            return -1;
        }
        if (right == null) {
            return 1;
        }
        int result = left.EventTimeUtc.CompareTo(right.EventTimeUtc);
        if (result != 0) {
            return result;
        }
        result = Nullable.Compare(left.RecordId, right.RecordId);
        return result != 0
            ? result
            : string.Compare(left.Identity, right.Identity, StringComparison.Ordinal);
    }

    private static int CompareEvents(EventObject? left, EventObject? right) {
        if (ReferenceEquals(left, right)) {
            return 0;
        }
        if (left == null) {
            return -1;
        }
        if (right == null) {
            return 1;
        }
        int result = left.TimeCreated.CompareTo(right.TimeCreated);
        if (result != 0) {
            return result;
        }
        result = Nullable.Compare(left.RecordId, right.RecordId);
        if (result != 0) {
            return result;
        }
        result = string.Compare(left.SourceComputer, right.SourceComputer, StringComparison.OrdinalIgnoreCase);
        if (result != 0) {
            return result;
        }
        result = string.Compare(left.OriginalLogName, right.OriginalLogName, StringComparison.OrdinalIgnoreCase);
        return result != 0 ? result : left.Id.CompareTo(right.Id);
    }

    /// <summary>Executes a reusable fixture and compares exact finding IDs and multiplicity.</summary>
    public static EventDetectionFixtureResult TestFixture(
        EventDetectionFixture fixture,
        EventDetectionPlan plan,
        EventDetectionEngineOptions? options = null) {

        if (fixture == null) {
            throw new ArgumentNullException(nameof(fixture));
        }
        string name = fixture.Name?.Trim() ?? string.Empty;
        if (name.Length == 0) {
            throw new ArgumentException("Fixture Name is required.", nameof(fixture));
        }
        EventObservation[] observations = (fixture.Observations ?? Array.Empty<EventObservation>()).ToArray();
        string[] expected = (fixture.ExpectedRuleIds ?? Array.Empty<string>())
            .Select(static id => id?.Trim() ?? string.Empty)
            .ToArray();
        if (observations.Any(static item => item == null) || expected.Any(static id => id.Length == 0)) {
            throw new ArgumentException("Fixture observations and expected rule IDs cannot contain null or empty values.", nameof(fixture));
        }
        return new EventDetectionFixtureResult(name, Evaluate(observations, plan, options), expected);
    }

    private sealed partial class Evaluator {
        private readonly EventDetectionPlan _plan;
        private readonly EventDetectionEngineOptions _options;
        private readonly Dictionary<StateKey, ThresholdState> _thresholdStates = new();
        private readonly Dictionary<StateKey, ThresholdState> _distinctStates = new();
        private readonly Dictionary<StateKey, TemporalState> _temporalStates = new();
        private readonly List<EventDetectionFinding> _findings = new();
        private readonly int[] _matchingStepIndexes;
        private long _observations;
        private int _stateObservations;
        private long _stateBytes;
        private bool _observationBoundReported;
        private bool _groupBoundReported;
        private bool _stateBoundReported;
        private DateTime _nextStateExpiryUtc = DateTime.MaxValue;
        private readonly HashSet<string> _missingDistinctFieldsReported = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _missingGroupFieldsReported = new(StringComparer.OrdinalIgnoreCase);

        internal Evaluator(EventDetectionPlan plan, EventDetectionEngineOptions? options) {
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
            _options = SnapshotOptions(options);
            _matchingStepIndexes = new int[plan.CompiledRules
                .Select(static rule => rule.Steps.Length)
                .DefaultIfEmpty(0)
                .Max()];
        }

        internal List<EventDetectionFinding> Process(EventObservation observation) {
            _findings.Clear();
            _observations++;
            if (_options.MaximumObservations > 0 && _observations > _options.MaximumObservations) {
                if (_observationBoundReported) {
                    return _findings;
                }
                _observationBoundReported = true;
                _findings.Add(CreateIncomplete(
                    observation,
                    $"MaximumObservations limit of {_options.MaximumObservations} was reached."));
                return _findings;
            }
            EvictExpiredStates(observation.EventTimeUtc);

            List<EventDetectionFinding> findings = _findings;
            EventDetectionPlan.CompiledRule[] candidates = _plan.GetCandidates(observation);
            if (candidates.Length > _options.MaximumCandidateRules) {
                findings.Add(CreateIncomplete(
                    observation,
                    $"MaximumCandidateRules limit of {_options.MaximumCandidateRules} was reached."));
                return findings;
            }
            for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++) {
                EventDetectionPlan.CompiledRule rule = candidates[candidateIndex];
                try {
                    if (!rule.Matches(observation) || rule.IsSuppressed(observation)) {
                        continue;
                    }
                    switch (rule.Definition.Kind) {
                        case EventDetectionRuleKind.Stateless:
                            findings.Add(CreateFinding(rule, new[] { observation }, groupValue: null));
                            break;
                        case EventDetectionRuleKind.Threshold:
                            ProcessThreshold(rule, observation, findings);
                            break;
                        case EventDetectionRuleKind.DistinctValue:
                            ProcessDistinct(rule, observation, findings);
                            break;
                        case EventDetectionRuleKind.Temporal:
                        case EventDetectionRuleKind.OrderedTemporal:
                            ProcessTemporal(rule, observation, findings);
                            break;
                    }
                } catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException) {
                    findings.Add(CreateError(rule, observation, exception));
                }
            }
            return findings;
        }

        private void ProcessThreshold(
            EventDetectionPlan.CompiledRule rule,
            EventObservation observation,
            ICollection<EventDetectionFinding> findings) {

            if (!TryResolveGroupValue(rule, observation, findings, out string groupValue)) {
                return;
            }
            var key = new StateKey(rule.Definition.RuleId, groupValue);
            if (!_thresholdStates.TryGetValue(key, out ThresholdState? state)) {
                if (!CanCreateState(observation, findings)) {
                    return;
                }
                state = new ThresholdState();
                _thresholdStates.Add(key, state);
            }
            state.ExpiresUtc = Later(state.ExpiresUtc, observation.EventTimeUtc + rule.Definition.Window);
            TrackStateExpiry(state.ExpiresUtc);

            PruneWindow(state.Observations, observation.EventTimeUtc, rule.Definition.Window);
            if (!CanRetainObservation(observation, findings)) {
                return;
            }
            InsertChronologically(state.Observations, observation);
            Retain(observation);
            PruneWindow(
                state.Observations,
                state.Observations[state.Observations.Count - 1].EventTimeUtc,
                rule.Definition.Window);
            if (state.Observations.Count < rule.Definition.Threshold) {
                return;
            }

            EventObservation[] evidence = state.Observations
                .Skip(state.Observations.Count - rule.Definition.Threshold)
                .ToArray();
            findings.Add(CreateFinding(rule, evidence, groupValue));
            Release(state.Observations);
            state.Observations.Clear();
            _thresholdStates.Remove(key);
        }

        private void ProcessDistinct(
            EventDetectionPlan.CompiledRule rule,
            EventObservation observation,
            ICollection<EventDetectionFinding> findings) {

            string distinctBy = rule.Definition.DistinctBy!;
            if (!TryResolveFieldValue(distinctBy, observation, out _)) {
                string diagnosticKey = rule.Definition.RuleId + "\n" + distinctBy;
                if (_missingDistinctFieldsReported.Add(diagnosticKey)) {
                    findings.Add(CreateIncomplete(
                        observation,
                        $"Distinct-value rule '{rule.Definition.RuleId}' could not evaluate required field '{distinctBy}'."));
                }
                return;
            }
            if (!TryResolveGroupValue(rule, observation, findings, out string groupValue)) {
                return;
            }
            var key = new StateKey(rule.Definition.RuleId, groupValue);
            if (!_distinctStates.TryGetValue(key, out ThresholdState? state)) {
                if (!CanCreateState(observation, findings)) {
                    return;
                }
                state = new ThresholdState();
                _distinctStates.Add(key, state);
            }
            state.ExpiresUtc = Later(state.ExpiresUtc, observation.EventTimeUtc + rule.Definition.Window);
            TrackStateExpiry(state.ExpiresUtc);
            PruneWindow(state.Observations, observation.EventTimeUtc, rule.Definition.Window);
            if (!CanRetainObservation(observation, findings)) {
                return;
            }
            InsertChronologically(state.Observations, observation);
            Retain(observation);
            PruneWindow(
                state.Observations,
                state.Observations[state.Observations.Count - 1].EventTimeUtc,
                rule.Definition.Window);
            EventObservation[] evidence = state.Observations
                .GroupBy(item => ResolveGroupValue(distinctBy, item), StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.Last())
                .Take(rule.Definition.Threshold)
                .OrderBy(static item => item.EventTimeUtc)
                .ToArray();
            if (evidence.Length < rule.Definition.Threshold) {
                return;
            }
            findings.Add(CreateFinding(rule, evidence, groupValue));
            Release(state.Observations);
            state.Observations.Clear();
            _distinctStates.Remove(key);
        }

        private void ProcessTemporal(
            EventDetectionPlan.CompiledRule rule,
            EventObservation observation,
            ICollection<EventDetectionFinding> findings) {

            int matchingStepCount = rule.CopyMatchingStepIndexes(observation, _matchingStepIndexes);
            if (matchingStepCount == 0) {
                return;
            }
            if (!TryResolveGroupValue(rule, observation, findings, out string groupValue)) {
                return;
            }
            var key = new StateKey(rule.Definition.RuleId, groupValue);
            if (!_temporalStates.TryGetValue(key, out TemporalState? state)) {
                if (!CanCreateState(observation, findings)) {
                    return;
                }
                state = new TemporalState();
                _temporalStates.Add(key, state);
            }
            state.ExpiresUtc = Later(state.ExpiresUtc, observation.EventTimeUtc + rule.Definition.Window);
            TrackStateExpiry(state.ExpiresUtc);
            if (state.UnorderedSafetyLimitReached) {
                return;
            }
            if (rule.Definition.Kind == EventDetectionRuleKind.OrderedTemporal) {
                ProcessOrderedTemporal(rule, observation, _matchingStepIndexes, matchingStepCount, groupValue, key, state, findings);
            } else {
                ProcessUnorderedTemporal(rule, observation, _matchingStepIndexes, matchingStepCount, groupValue, key, state, findings);
            }
        }

        private void ProcessUnorderedTemporal(
            EventDetectionPlan.CompiledRule rule,
            EventObservation observation,
            int[] matchingSteps,
            int matchingStepCount,
            string groupValue,
            StateKey key,
            TemporalState state,
            ICollection<EventDetectionFinding> findings) {

            PruneUnorderedWindow(state, observation.EventTimeUtc, rule.Definition.Window);
            int[] stepIndexes = new int[matchingStepCount];
            Array.Copy(matchingSteps, stepIndexes, matchingStepCount);
            long candidateStateBytes = EstimateUnorderedCandidateBytes(stepIndexes.Length);
            int redundantCandidate = FindRedundantCandidate(state.UnorderedEvidence, stepIndexes);
            int candidateLimit = MaximumUnorderedCandidates(rule.Steps.Length);
            if (redundantCandidate < 0 && state.UnorderedEvidence.Count >= candidateLimit) {
                DisableUnorderedTemporalState(
                    rule,
                    state,
                    observation,
                    findings,
                    $"retained candidate limit of {candidateLimit}");
                return;
            }
            if (redundantCandidate >= 0) {
                UnorderedTemporalCandidate existing = state.UnorderedEvidence[redundantCandidate];
                if (!CanReplaceRetainedObservation(
                        existing.Observation,
                        existing.StateBytes,
                        observation,
                        candidateStateBytes,
                        findings)) {
                    return;
                }
                Release(existing.Observation, existing.StateBytes);
                state.UnorderedEvidence.RemoveAt(redundantCandidate);
            } else if (!CanRetainObservation(observation, candidateStateBytes, findings)) {
                return;
            }
            InsertUnorderedCandidate(
                state.UnorderedEvidence,
                new UnorderedTemporalCandidate(observation, stepIndexes, candidateStateBytes));
            Retain(observation, candidateStateBytes);
            long matchingWorkLimit = MaximumUnorderedMatchingWork(rule.Steps.Length);
            long matchingWork = state.UnorderedMatchingWork;
            bool matched = TrySelectUnorderedEvidence(
                    rule,
                    state.UnorderedEvidence,
                    ref matchingWork,
                    matchingWorkLimit,
                    out EventObservation[] evidence,
                    out bool workLimitReached);
            state.UnorderedMatchingWork = matchingWork;
            if (!matched) {
                if (workLimitReached) {
                    DisableUnorderedTemporalState(
                        rule,
                        state,
                        observation,
                        findings,
                        $"matching work limit of {matchingWorkLimit} selector checks");
                }
                return;
            }
            DateTime maximum = evidence.Max(static item => item.EventTimeUtc);
            DateTime earliest = evidence.Min(static item => item.EventTimeUtc);
            if (maximum - earliest > rule.Definition.Window) {
                return;
            }
            findings.Add(CreateFinding(rule, evidence.OrderBy(static item => item.EventTimeUtc).ToArray(), groupValue));
            ReleaseUnorderedCandidates(state.UnorderedEvidence);
            state.UnorderedEvidence.Clear();
            _temporalStates.Remove(key);
        }

        private static bool ContainsIndex(int[] values, int count, int expected) {
            for (int index = 0; index < count; index++) {
                if (values[index] == expected) {
                    return true;
                }
            }
            return false;
        }

        private int StateGroupCount => _thresholdStates.Count + _distinctStates.Count + _temporalStates.Count;

        private void EvictExpiredStates(DateTime current) {
            if (current <= _nextStateExpiryUtc) {
                return;
            }
            foreach (KeyValuePair<StateKey, ThresholdState> item in _thresholdStates
                         .Where(item => item.Value.ExpiresUtc < current)
                         .ToArray()) {
                Release(item.Value.Observations);
                _thresholdStates.Remove(item.Key);
            }
            foreach (KeyValuePair<StateKey, ThresholdState> item in _distinctStates
                         .Where(item => item.Value.ExpiresUtc < current)
                         .ToArray()) {
                Release(item.Value.Observations);
                _distinctStates.Remove(item.Key);
            }
            foreach (KeyValuePair<StateKey, TemporalState> item in _temporalStates
                         .Where(item => item.Value.ExpiresUtc < current)
                         .ToArray()) {
                ReleaseUnorderedCandidates(item.Value.UnorderedEvidence);
                ReleaseOrderedPrefixes(item.Value.OrderedPrefixes);
                _temporalStates.Remove(item.Key);
            }
            _nextStateExpiryUtc = _thresholdStates.Values.Select(static state => state.ExpiresUtc)
                .Concat(_distinctStates.Values.Select(static state => state.ExpiresUtc))
                .Concat(_temporalStates.Values.Select(static state => state.ExpiresUtc))
                .DefaultIfEmpty(DateTime.MaxValue)
                .Min();
        }

        private void TrackStateExpiry(DateTime expiryUtc) {
            if (expiryUtc < _nextStateExpiryUtc) {
                _nextStateExpiryUtc = expiryUtc;
            }
        }

        private static DateTime Later(DateTime left, DateTime right) => left >= right ? left : right;

        private bool CanCreateState(
            EventObservation observation,
            ICollection<EventDetectionFinding> findings) {

            if (StateGroupCount < _options.MaximumGroups) {
                return true;
            }
            if (!_groupBoundReported) {
                _groupBoundReported = true;
                findings.Add(CreateIncomplete(
                    observation,
                    $"MaximumGroups limit of {_options.MaximumGroups} was reached."));
            }
            return false;
        }

        private bool CanRetainObservation(
            EventObservation observation,
            ICollection<EventDetectionFinding> findings) {

            return CanRetainObservation(observation, 0, findings);
        }

        private bool CanRetainObservation(
            EventObservation observation,
            long additionalStateBytes,
            ICollection<EventDetectionFinding> findings) {

            long observationBytes = EstimateObservationBytes(observation) + additionalStateBytes;
            if (_stateObservations < _options.MaximumStateObservations &&
                _stateBytes <= _options.MaximumStateBytes - observationBytes) {
                return true;
            }
            if (!_stateBoundReported) {
                _stateBoundReported = true;
                findings.Add(CreateIncomplete(
                    observation,
                    $"Detection state limit was reached. MaximumStateObservations={_options.MaximumStateObservations}; " +
                    $"MaximumStateBytes={_options.MaximumStateBytes}."));
            }
            return false;
        }

        private bool CanReplaceRetainedObservation(
            EventObservation existing,
            long existingAdditionalBytes,
            EventObservation replacement,
            long replacementAdditionalBytes,
            ICollection<EventDetectionFinding> findings) {

            long releasedBytes = EstimateObservationBytes(existing) + existingAdditionalBytes;
            long replacementBytes = EstimateObservationBytes(replacement) + replacementAdditionalBytes;
            long retainedBytes = Math.Max(0, _stateBytes - releasedBytes);
            if (replacementBytes <= _options.MaximumStateBytes &&
                retainedBytes <= _options.MaximumStateBytes - replacementBytes) {
                return true;
            }
            if (!_stateBoundReported) {
                _stateBoundReported = true;
                findings.Add(CreateIncomplete(
                    replacement,
                    $"Detection state limit was reached. MaximumStateObservations={_options.MaximumStateObservations}; " +
                    $"MaximumStateBytes={_options.MaximumStateBytes}."));
            }
            return false;
        }

        private void PruneWindow(List<EventObservation> observations, DateTime current, TimeSpan window) {
            DateTime minimum = current - window;
            int removeCount = 0;
            while (removeCount < observations.Count && observations[removeCount].EventTimeUtc < minimum) {
                Release(observations[removeCount]);
                removeCount++;
            }
            if (removeCount > 0) {
                observations.RemoveRange(0, removeCount);
            }
        }

        private static void InsertChronologically(
            List<EventObservation> observations,
            EventObservation observation) {

            if (observations.Count == 0 ||
                observations[observations.Count - 1].EventTimeUtc <= observation.EventTimeUtc) {
                observations.Add(observation);
                return;
            }
            int low = 0;
            int high = observations.Count;
            while (low < high) {
                int middle = low + ((high - low) / 2);
                if (observations[middle].EventTimeUtc <= observation.EventTimeUtc) {
                    low = middle + 1;
                } else {
                    high = middle;
                }
            }
            observations.Insert(low, observation);
        }

        private void Retain(EventObservation observation, long additionalStateBytes = 0) {
            _stateObservations++;
            _stateBytes += EstimateObservationBytes(observation) + additionalStateBytes;
        }

        private void Release(EventObservation observation, long additionalStateBytes = 0) {
            _stateObservations--;
            _stateBytes -= EstimateObservationBytes(observation) + additionalStateBytes;
        }

        private void Release(IEnumerable<EventObservation> observations) {
            foreach (EventObservation observation in observations) {
                Release(observation);
            }
        }

        private void ReleaseUnorderedCandidates(IEnumerable<UnorderedTemporalCandidate> candidates) {
            foreach (UnorderedTemporalCandidate candidate in candidates) {
                Release(candidate.Observation, candidate.StateBytes);
            }
        }

        private static long EstimateUnorderedCandidateBytes(int matchingStepCount) =>
            96L + (matchingStepCount * sizeof(int));

        private static int MaximumUnorderedCandidates(int stepCount) => stepCount * stepCount;

        private static long MaximumUnorderedMatchingWork(int stepCount) => 16L * stepCount * stepCount;

        private void DisableUnorderedTemporalState(
            EventDetectionPlan.CompiledRule rule,
            TemporalState state,
            EventObservation observation,
            ICollection<EventDetectionFinding> findings,
            string limit) {

            ReleaseUnorderedCandidates(state.UnorderedEvidence);
            state.UnorderedEvidence.Clear();
            state.UnorderedSafetyLimitReached = true;
            findings.Add(CreateIncomplete(
                observation,
                $"Temporal rule '{rule.Definition.RuleId}' reached its {limit}; " +
                "this group will remain incomplete until its active window expires."));
        }

        private static long EstimateObservationBytes(EventObservation observation) {
            long bytes = 256L +
                         StringBytes(observation.Identity) +
                         StringBytes(observation.TypeName) +
                         StringBytes(observation.ProviderName) +
                         StringBytes(observation.SourceLog) +
                         StringBytes(observation.ContainerLog) +
                         StringBytes(observation.SourceComputer) +
                         StringBytes(observation.CollectorComputer);
            foreach (KeyValuePair<string, object?> field in observation.Fields) {
                bytes += 64L + StringBytes(field.Key);
                if (field.Value is string text) {
                    bytes += StringBytes(text);
                }
            }
            return bytes;
        }

        private static long StringBytes(string? value) =>
            value == null ? 0L : 24L + (value.Length * 2L);

        private EventDetectionFinding CreateFinding(
            EventDetectionPlan.CompiledRule rule,
            IReadOnlyList<EventObservation> evidence,
            string? groupValue) {

            EventDetectionRuleDefinition definition = rule.Definition;
            var entities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(definition.GroupBy) && groupValue != null) {
                entities[definition.GroupBy!] = groupValue;
            }
            string explanation = definition.Kind switch {
                EventDetectionRuleKind.Stateless => $"Observation matched detection rule {definition.RuleId}.",
                EventDetectionRuleKind.DistinctValue =>
                    $"Observed {evidence.Count} distinct {definition.DistinctBy} values within {definition.Window}.",
                EventDetectionRuleKind.Temporal =>
                    $"Observed all {definition.Steps.Count} temporal steps within {definition.Window}.",
                EventDetectionRuleKind.OrderedTemporal =>
                    $"Observed all {definition.Steps.Count} temporal steps in order within {definition.Window}.",
                _ => $"Observed {evidence.Count} matching events within {definition.Window}."
            };
            return new EventDetectionFinding(
                definition.RuleId,
                definition.Version,
                definition.PackId,
                definition.PackVersion,
                definition.SourceKind,
                definition.SourceId,
                definition.SourceStatus,
                definition.SourceHash,
                definition.License,
                definition.Title,
                definition.Severity,
                definition.Confidence,
                EventDetectionFindingStatus.Matched,
                evidence.Min(static item => item.EventTimeUtc),
                evidence.Max(static item => item.EventTimeUtc),
                evidence,
                definition.Tags,
                definition.FalsePositives,
                definition.References,
                entities,
                _options.Coverage!,
                explanation,
                completenessDiagnostic: null);
        }

        private EventDetectionFinding CreateError(
            EventDetectionPlan.CompiledRule rule,
            EventObservation observation,
            Exception exception) {

            EventDetectionRuleDefinition definition = rule.Definition;
            return new EventDetectionFinding(
                definition.RuleId,
                definition.Version,
                definition.PackId,
                definition.PackVersion,
                definition.SourceKind,
                definition.SourceId,
                definition.SourceStatus,
                definition.SourceHash,
                definition.License,
                definition.Title,
                definition.Severity,
                definition.Confidence,
                EventDetectionFindingStatus.Error,
                observation.EventTimeUtc,
                observation.EventTimeUtc,
                new[] { observation },
                definition.Tags,
                definition.FalsePositives,
                definition.References,
                new Dictionary<string, string>(),
                _options.Coverage!,
                $"Detection evaluation failed: {exception.Message}",
                exception.GetType().FullName);
        }

        private EventDetectionFinding CreateIncomplete(
            EventObservation observation,
            string diagnostic) {

            return new EventDetectionFinding(
                "EVX-ENGINE-BOUNDS",
                "1.0.0",
                string.Empty,
                string.Empty,
                "Engine",
                "EVX-ENGINE-BOUNDS",
                string.Empty,
                string.Empty,
                string.Empty,
                "Detection execution incomplete",
                EventDetectionSeverity.Medium,
                100,
                EventDetectionFindingStatus.Incomplete,
                observation.EventTimeUtc,
                observation.EventTimeUtc,
                new[] { observation },
                new[] { "eventviewerx", "data-quality", "execution-bound" },
                Array.Empty<string>(),
                Array.Empty<string>(),
                new Dictionary<string, string>(),
                _options.Coverage!,
                diagnostic,
                diagnostic);
        }

        private bool TryResolveGroupValue(
            EventDetectionPlan.CompiledRule rule,
            EventObservation observation,
            ICollection<EventDetectionFinding> findings,
            out string value) {

            string? field = rule.Definition.GroupBy;
            if (string.IsNullOrWhiteSpace(field)) {
                value = "*";
                return true;
            }
            if (TryResolveFieldValue(field!, observation, out value)) {
                return true;
            }
            string diagnosticKey = rule.Definition.RuleId + "\n" + field;
            if (_missingGroupFieldsReported.Add(diagnosticKey)) {
                findings.Add(CreateIncomplete(
                    observation,
                    $"Stateful rule '{rule.Definition.RuleId}' could not evaluate required grouping field '{field}'."));
            }
            return false;
        }

        private static string ResolveGroupValue(string field, EventObservation observation) {
            return TryResolveFieldValue(field, observation, out string value)
                ? value
                : string.Empty;
        }

        private static bool TryResolveFieldValue(
            string field,
            EventObservation observation,
            out string value) {

            if (!observation.Fields.TryGetValue(field, out object? raw) || raw == null) {
                value = string.Empty;
                return false;
            }
            value = raw is IFormattable formattable
                ? formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty
                : raw.ToString() ?? string.Empty;
            return true;
        }

        private static EventDetectionEngineOptions SnapshotOptions(EventDetectionEngineOptions? options) {
            options ??= new EventDetectionEngineOptions();
            if (options.MaximumObservations < 0) {
                throw new ArgumentOutOfRangeException(nameof(options.MaximumObservations));
            }
            if (options.MaximumGroups <= 0) {
                throw new ArgumentOutOfRangeException(nameof(options.MaximumGroups));
            }
            if (options.MaximumStateObservations <= 0) {
                throw new ArgumentOutOfRangeException(nameof(options.MaximumStateObservations));
            }
            if (options.MaximumStateBytes <= 0) {
                throw new ArgumentOutOfRangeException(nameof(options.MaximumStateBytes));
            }
            if (options.MaximumCandidateRules <= 0) {
                throw new ArgumentOutOfRangeException(nameof(options.MaximumCandidateRules));
            }
            return new EventDetectionEngineOptions(
                options.MaximumObservations,
                options.MaximumGroups,
                options.MaximumStateObservations,
                options.MaximumStateBytes,
                options.MaximumCandidateRules,
                options.Coverage ?? EventDetectionCoverage.Unknown());
        }

        private readonly struct StateKey : IEquatable<StateKey> {
            internal StateKey(string ruleId, string groupValue) {
                RuleId = ruleId;
                GroupValue = groupValue;
            }

            private string RuleId { get; }
            private string GroupValue { get; }

            public bool Equals(StateKey other) =>
                string.Equals(RuleId, other.RuleId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(GroupValue, other.GroupValue, StringComparison.OrdinalIgnoreCase);

            public override bool Equals(object? obj) => obj is StateKey other && Equals(other);

            public override int GetHashCode() {
                unchecked {
                    return (StringComparer.OrdinalIgnoreCase.GetHashCode(RuleId) * 397) ^
                           StringComparer.OrdinalIgnoreCase.GetHashCode(GroupValue);
                }
            }
        }

        private sealed class ThresholdState {
            internal List<EventObservation> Observations { get; } = new();
            internal DateTime ExpiresUtc { get; set; }
        }

        private sealed class TemporalState {
            internal List<UnorderedTemporalCandidate> UnorderedEvidence { get; } = new();
            internal List<OrderedTemporalPrefix?> OrderedPrefixes { get; } = new();
            internal long UnorderedMatchingWork { get; set; }
            internal bool UnorderedSafetyLimitReached { get; set; }
            internal DateTime ExpiresUtc { get; set; }
        }

    }
}
