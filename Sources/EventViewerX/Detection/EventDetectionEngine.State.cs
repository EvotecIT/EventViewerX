using System.Globalization;

namespace EventViewerX;

public static partial class EventDetectionEngine {
    private sealed partial class Evaluator {
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
            return options.EventTimeOrdering == null
                ? new EventDetectionEngineOptions(options.MaximumObservations, options.MaximumGroups,
                    options.MaximumStateObservations, options.MaximumStateBytes, options.MaximumCandidateRules,
                    options.Coverage ?? EventDetectionCoverage.Unknown())
                : new EventDetectionEngineOptions(options.EventTimeOrdering, options.MaximumObservations,
                    options.MaximumGroups, options.MaximumStateObservations, options.MaximumStateBytes,
                    options.MaximumCandidateRules, options.Coverage ?? EventDetectionCoverage.Unknown());
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