using System.Text.Json;

namespace EventViewerX;

public static partial class EventDetectionEngine {
    internal sealed partial class Evaluator {
        internal string CaptureState() {
            if (_evaluationIncomplete || _failedRules.Count != 0) {
                throw new InvalidOperationException("Incomplete or failed detection state cannot become a reusable checkpoint.");
            }
            if (_options.EventTimeOrdering != null) { throw new InvalidOperationException("Durable replay requires ordered input without a live reorder buffer."); }
            var groups = new List<CheckpointGroup>();
            foreach (var pair in _thresholdStates) {
                groups.Add(new CheckpointGroup { RuleId = pair.Key.RuleId, Group = pair.Key.GroupValue,
                    Kind = EventDetectionRuleKind.Threshold, ExpiresUtc = pair.Value.ExpiresUtc,
                    Observations = pair.Value.Observations.Select(EventObservationSnapshot.Capture).ToArray() });
            }
            foreach (var pair in _distinctStates) {
                groups.Add(new CheckpointGroup { RuleId = pair.Key.RuleId, Group = pair.Key.GroupValue,
                    Kind = EventDetectionRuleKind.DistinctValue, ExpiresUtc = pair.Value.ExpiresUtc, LatestUtc = pair.Value.LatestEventUtc,
                    Distinct = pair.Value.Chronological.Select(item => new CheckpointDistinct {
                        Value = item.Value, Observation = EventObservationSnapshot.Capture(item.Observation) }).ToArray() });
            }
            foreach (var pair in _temporalStates) {
                EventDetectionRuleKind kind = _plan.CompiledRules.First(rule => rule.Definition.RuleId.Equals(pair.Key.RuleId, StringComparison.OrdinalIgnoreCase)).Definition.Kind;
                groups.Add(new CheckpointGroup { RuleId = pair.Key.RuleId, Group = pair.Key.GroupValue, Kind = kind,
                    ExpiresUtc = pair.Value.ExpiresUtc, MatchingWork = pair.Value.UnorderedMatchingWork,
                    Candidates = pair.Value.UnorderedEvidence.Select(item => new CheckpointCandidate {
                        Observation = EventObservationSnapshot.Capture(item.Observation), MatchingSteps = item.MatchingSteps }).ToArray(),
                    Prefixes = pair.Value.OrderedPrefixes.Select(item => item?.Evidence.Select(EventObservationSnapshot.Capture).ToArray()).ToArray() });
            }
            foreach (var pair in _absenceStates) {
                groups.Add(new CheckpointGroup { RuleId = pair.Key.RuleId, Group = pair.Key.GroupValue,
                    Kind = EventDetectionRuleKind.Absence, Observations = pair.Value.Select(EventObservationSnapshot.Capture).ToArray() });
            }
            return JsonSerializer.Serialize(new CheckpointState { Groups = groups.ToArray(), AbsenceLatestUtc = _absenceLatestUtc }, EventAnalysisJson.CreateSerializerOptions());
        }

        internal void RestoreState(string json) {
            if (StateGroupCount != 0 || _observations != 0) { throw new InvalidOperationException("State can only be restored into a new evaluator."); }
            CheckpointState document = JsonSerializer.Deserialize<CheckpointState>(json, EventAnalysisJson.CreateSerializerOptions())
                ?? throw new InvalidDataException("Missing correlation checkpoint state.");
            if (document.Groups == null || document.Groups.Length > _options.MaximumGroups) { throw new InvalidDataException("Checkpoint exceeds the group bound."); }
            var seen = new HashSet<StateKey>();
            EventObservation Restore(EventObservationSnapshot snapshot, long overhead = 0) {
                EventObservation observation = snapshot.Restore();
                long bytes = EstimateObservationBytes(observation) + overhead;
                if (_stateObservations >= _options.MaximumStateObservations || bytes > _options.MaximumStateBytes - _stateBytes) {
                    throw new InvalidDataException("Checkpoint exceeds the configured correlation state bounds.");
                }
                Retain(observation, overhead);
                return observation;
            }
            foreach (CheckpointGroup group in document.Groups) {
                _cancellationToken.ThrowIfCancellationRequested();
                EventDetectionPlan.CompiledRule rule = _plan.CompiledRules.FirstOrDefault(item => item.Definition.RuleId.Equals(group.RuleId, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidDataException("Checkpoint references an unknown rule.");
                var key = new StateKey(group.RuleId, group.Group);
                if (group.Kind != rule.Definition.Kind || !seen.Add(key)) { throw new InvalidDataException("Invalid checkpoint group identity."); }
                switch (group.Kind) {
                    case EventDetectionRuleKind.Threshold:
                        if (group.Observations.Length >= rule.Definition.Threshold) { throw new InvalidDataException("Invalid unconsumed threshold state."); }
                        var threshold = new ThresholdState { ExpiresUtc = group.ExpiresUtc };
                        threshold.Observations.AddRange(group.Observations.Select(item => Restore(item)));
                        _thresholdStates.Add(key, threshold);
                        break;
                    case EventDetectionRuleKind.DistinctValue:
                        if (group.Distinct.Length >= rule.Definition.Threshold) { throw new InvalidDataException("Invalid unconsumed distinct state."); }
                        var distinct = new DistinctState { ExpiresUtc = group.ExpiresUtc, LatestEventUtc = group.LatestUtc };
                        foreach (CheckpointDistinct item in group.Distinct) {
                            long overhead = 160L + StringBytes(item.Value);
                            var entry = new DistinctEntry(item.Value, Restore(item.Observation, overhead), overhead);
                            distinct.ByValue.Add(item.Value, distinct.Chronological.AddLast(entry));
                        }
                        _distinctStates.Add(key, distinct);
                        break;
                    case EventDetectionRuleKind.Temporal:
                    case EventDetectionRuleKind.OrderedTemporal:
                        if (group.Candidates.Length > MaximumUnorderedCandidates(rule.Steps.Length) || group.Prefixes.Length > rule.Steps.Length ||
                            group.MatchingWork < 0 || group.MatchingWork > MaximumUnorderedMatchingWork(rule.Steps.Length)) {
                            throw new InvalidDataException("Invalid temporal checkpoint bounds.");
                        }
                        var temporal = new TemporalState { ExpiresUtc = group.ExpiresUtc, UnorderedMatchingWork = group.MatchingWork };
                        foreach (CheckpointCandidate item in group.Candidates) {
                            if (item.MatchingSteps.Length == 0 || item.MatchingSteps.Any(index => index < 0 || index >= rule.Steps.Length)) {
                                throw new InvalidDataException("Invalid temporal checkpoint steps.");
                            }
                            long overhead = EstimateUnorderedCandidateBytes(item.MatchingSteps.Length);
                            temporal.UnorderedEvidence.Add(new UnorderedTemporalCandidate(Restore(item.Observation, overhead), item.MatchingSteps, overhead));
                        }
                        for (int index = 0; index < group.Prefixes.Length; index++) {
                            EventObservationSnapshot[]? prefix = group.Prefixes[index];
                            if (prefix != null && prefix.Length != index + 1) { throw new InvalidDataException("Invalid ordered temporal prefix."); }
                            temporal.OrderedPrefixes.Add(prefix == null ? null : new OrderedTemporalPrefix(prefix.Select(item => Restore(item)).ToList()));
                        }
                        _temporalStates.Add(key, temporal);
                        break;
                    case EventDetectionRuleKind.Absence:
                        _absenceStates.Add(key, group.Observations.Select(item => Restore(item)).ToList());
                        break;
                    default: throw new InvalidDataException("Unsupported checkpoint rule state.");
                }
                if (group.Kind != EventDetectionRuleKind.Absence) { TrackStateExpiry(group.ExpiresUtc); }
            }
            _absenceLatestUtc = document.AbsenceLatestUtc;
        }

        private sealed class CheckpointState {
            public CheckpointGroup[] Groups { get; set; } = Array.Empty<CheckpointGroup>();
            public DateTime AbsenceLatestUtc { get; set; }
        }
        private sealed class CheckpointGroup {
            public string RuleId { get; set; } = string.Empty;
            public string Group { get; set; } = string.Empty;
            public EventDetectionRuleKind Kind { get; set; }
            public DateTime ExpiresUtc { get; set; }
            public DateTime LatestUtc { get; set; }
            public EventObservationSnapshot[] Observations { get; set; } = Array.Empty<EventObservationSnapshot>();
            public CheckpointDistinct[] Distinct { get; set; } = Array.Empty<CheckpointDistinct>();
            public CheckpointCandidate[] Candidates { get; set; } = Array.Empty<CheckpointCandidate>();
            public EventObservationSnapshot[]?[] Prefixes { get; set; } = Array.Empty<EventObservationSnapshot[]?>();
            public long MatchingWork { get; set; }
        }
        private sealed class CheckpointDistinct {
            public string Value { get; set; } = string.Empty;
            public EventObservationSnapshot Observation { get; set; } = new();
        }
        private sealed class CheckpointCandidate {
            public EventObservationSnapshot Observation { get; set; } = new();
            public int[] MatchingSteps { get; set; } = Array.Empty<int>();
        }
    }
}
