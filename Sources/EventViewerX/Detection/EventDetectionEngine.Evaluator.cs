using System.Globalization;

namespace EventViewerX;

public static partial class EventDetectionEngine {
    private sealed partial class Evaluator {
        private readonly EventDetectionPlan _plan;
        private readonly EventDetectionEngineOptions _options;
        private readonly CancellationToken _cancellationToken;
        private readonly Dictionary<StateKey, ThresholdState> _thresholdStates = new();
        private readonly Dictionary<StateKey, DistinctState> _distinctStates = new();
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

        internal Evaluator(EventDetectionPlan plan, EventDetectionEngineOptions? options, CancellationToken cancellationToken = default) {
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
            _options = SnapshotOptions(options);
            _cancellationToken = cancellationToken;
            _matchingStepIndexes = new int[plan.CompiledRules
                .Select(static rule => rule.Steps.Length)
                .DefaultIfEmpty(0)
                .Max()];
        }

        private List<EventDetectionFinding> ProcessOrdered(EventObservation observation) {
            _cancellationToken.ThrowIfCancellationRequested();
            _findings.Clear();
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
                _cancellationToken.ThrowIfCancellationRequested();
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
                } catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException &&
                    !(exception is OperationCanceledException && _cancellationToken.IsCancellationRequested)) {
                    findings.Add(CreateError(rule, observation, exception));
                }
            }
            _cancellationToken.ThrowIfCancellationRequested();
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

    }
}
