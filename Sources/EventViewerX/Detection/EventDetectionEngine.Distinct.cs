namespace EventViewerX;

public static partial class EventDetectionEngine {
    private sealed partial class Evaluator {
        private void ProcessDistinct(EventDetectionPlan.CompiledRule rule,
            EventObservation observation, ICollection<EventDetectionFinding> findings) {

            string distinctBy = rule.Definition.DistinctBy!;
            if (!TryResolveFieldValue(distinctBy, observation, out string distinctValue)) {
                string diagnosticKey = rule.Definition.RuleId + "\n" + distinctBy;
                if (_missingDistinctFieldsReported.Add(diagnosticKey)) {
                    findings.Add(CreateIncomplete(observation,
                        $"Distinct-value rule '{rule.Definition.RuleId}' could not evaluate required field '{distinctBy}'."));
                }
                return;
            }
            if (!TryResolveGroupValue(rule, observation, findings, out string groupValue)) {
                return;
            }
            var key = new StateKey(rule.Definition.RuleId, groupValue);
            if (!_distinctStates.TryGetValue(key, out DistinctState? state)) {
                if (!CanCreateState(observation, findings)) {
                    return;
                }
                state = new DistinctState();
                _distinctStates.Add(key, state);
            }
            state.LatestEventUtc = Later(state.LatestEventUtc, observation.EventTimeUtc);
            state.ExpiresUtc = state.LatestEventUtc + rule.Definition.Window;
            TrackStateExpiry(state.ExpiresUtc);
            DateTime minimum = state.LatestEventUtc - rule.Definition.Window;
            while (state.Chronological.First is { } expired && expired.Value.Observation.EventTimeUtc < minimum) {
                RemoveDistinctEntry(state, expired);
            }
            if (observation.EventTimeUtc < minimum) {
                return;
            }
            long stateBytes = 160L + StringBytes(distinctValue);
            if (state.ByValue.TryGetValue(distinctValue, out LinkedListNode<DistinctEntry>? previous)) {
                if (previous.Value.Observation.EventTimeUtc > observation.EventTimeUtc) {
                    return;
                }
                if (!CanReplaceRetainedObservation(previous.Value.Observation, previous.Value.StateBytes,
                        observation, stateBytes, findings)) {
                    return;
                }
                RemoveDistinctEntry(state, previous);
            } else if (!CanRetainObservation(observation, stateBytes, findings)) {
                return;
            }

            var entry = new DistinctEntry(distinctValue, observation, stateBytes);
            LinkedListNode<DistinctEntry>? preceding = state.Chronological.Last;
            while (preceding != null && preceding.Value.Observation.EventTimeUtc > observation.EventTimeUtc) {
                preceding = preceding.Previous;
            }
            LinkedListNode<DistinctEntry> node = preceding == null
                ? state.Chronological.AddFirst(entry)
                : state.Chronological.AddAfter(preceding, entry);
            state.ByValue.Add(distinctValue, node);
            Retain(observation, stateBytes);
            if (state.ByValue.Count < rule.Definition.Threshold) {
                return;
            }
            EventObservation[] evidence = state.Chronological.Select(static item => item.Observation).ToArray();
            findings.Add(CreateFinding(rule, evidence, groupValue));
            ReleaseDistinctState(state);
            _distinctStates.Remove(key);
        }

        private void RemoveDistinctEntry(DistinctState state, LinkedListNode<DistinctEntry> node) {
            state.ByValue.Remove(node.Value.Value);
            state.Chronological.Remove(node);
            Release(node.Value.Observation, node.Value.StateBytes);
        }

        private void ReleaseDistinctState(DistinctState state) {
            foreach (DistinctEntry entry in state.Chronological) {
                Release(entry.Observation, entry.StateBytes);
            }
            state.Chronological.Clear();
            state.ByValue.Clear();
        }

        private sealed class DistinctState {
            internal Dictionary<string, LinkedListNode<DistinctEntry>> ByValue { get; } = new(StringComparer.OrdinalIgnoreCase);
            internal LinkedList<DistinctEntry> Chronological { get; } = new();
            internal DateTime LatestEventUtc { get; set; }
            internal DateTime ExpiresUtc { get; set; }
        }

        private sealed class DistinctEntry {
            internal DistinctEntry(string value, EventObservation observation, long stateBytes) {
                Value = value;
                Observation = observation;
                StateBytes = stateBytes;
            }

            internal string Value { get; }
            internal EventObservation Observation { get; }
            internal long StateBytes { get; }
        }
    }
}
