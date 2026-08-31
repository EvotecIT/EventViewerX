namespace EventViewerX;

public static partial class EventDetectionEngine {
    private sealed partial class Evaluator {
        private void ProcessOrderedTemporal(
            EventDetectionPlan.CompiledRule rule,
            EventObservation observation,
            int[] matchingSteps,
            int matchingStepCount,
            string groupValue,
            StateKey key,
            TemporalState state,
            ICollection<EventDetectionFinding> findings) {

            EnsureOrderedPrefixCapacity(state.OrderedPrefixes, rule.Steps.Length - 1);
            PruneOrderedPrefixes(state.OrderedPrefixes, observation.EventTimeUtc, rule.Definition.Window);

            for (int matchedCount = rule.Steps.Length - 1; matchedCount >= 1; matchedCount--) {
                OrderedTemporalPrefix? source = state.OrderedPrefixes[matchedCount - 1];
                if (source == null || !ContainsIndex(matchingSteps, matchingStepCount, matchedCount)) {
                    continue;
                }
                if (matchedCount + 1 < rule.Steps.Length) {
                    OrderedTemporalPrefix? existing = state.OrderedPrefixes[matchedCount];
                    if (existing != null &&
                        existing.Evidence[0].EventTimeUtc >= source.Evidence[0].EventTimeUtc) {
                        continue;
                    }
                }
                var evidence = new List<EventObservation>(source.Evidence.Count + 1);
                evidence.AddRange(source.Evidence);
                evidence.Add(observation);
                if (matchedCount + 1 == rule.Steps.Length) {
                    findings.Add(CreateFinding(rule, evidence, groupValue));
                    ReleaseOrderedPrefixes(state.OrderedPrefixes);
                    state.OrderedPrefixes.Clear();
                    _temporalStates.Remove(key);
                    return;
                }
                ReplaceOrderedPrefix(
                    state.OrderedPrefixes,
                    matchedCount,
                    new OrderedTemporalPrefix(evidence),
                    observation,
                    findings);
            }

            if (ContainsIndex(matchingSteps, matchingStepCount, 0)) {
                ReplaceOrderedPrefix(
                    state.OrderedPrefixes,
                    0,
                    new OrderedTemporalPrefix(new List<EventObservation> { observation }),
                    observation,
                    findings);
            }
        }

        private bool CanReplaceOrderedPrefix(
            OrderedTemporalPrefix? existing,
            IReadOnlyList<EventObservation> replacement,
            EventObservation observation,
            ICollection<EventDetectionFinding> findings) {

            int releasedCount = existing?.Evidence.Count ?? 0;
            int retainedCount = Math.Max(0, _stateObservations - releasedCount);
            long releasedBytes = existing == null ? 0 : EstimateObservationBytes(existing.Evidence);
            long retainedBytes = Math.Max(0, _stateBytes - releasedBytes);
            long replacementBytes = EstimateObservationBytes(replacement);
            if (replacement.Count <= _options.MaximumStateObservations - retainedCount &&
                replacementBytes <= _options.MaximumStateBytes - retainedBytes) {
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

        private void ReplaceOrderedPrefix(
            IList<OrderedTemporalPrefix?> prefixes,
            int index,
            OrderedTemporalPrefix replacement,
            EventObservation observation,
            ICollection<EventDetectionFinding> findings) {

            OrderedTemporalPrefix? existing = prefixes[index];
            if (existing != null &&
                existing.Evidence[0].EventTimeUtc >= replacement.Evidence[0].EventTimeUtc) {
                return;
            }
            if (!CanReplaceOrderedPrefix(existing, replacement.Evidence, observation, findings)) {
                return;
            }
            if (existing != null) {
                Release(existing.Evidence);
            }
            prefixes[index] = replacement;
            Retain(replacement.Evidence);
        }

        private void PruneOrderedPrefixes(
            IList<OrderedTemporalPrefix?> prefixes,
            DateTime current,
            TimeSpan window) {

            for (int index = 0; index < prefixes.Count; index++) {
                OrderedTemporalPrefix? prefix = prefixes[index];
                if (prefix == null || current - prefix.Evidence[0].EventTimeUtc <= window) {
                    continue;
                }
                Release(prefix.Evidence);
                prefixes[index] = null;
            }
        }

        private static void EnsureOrderedPrefixCapacity(
            IList<OrderedTemporalPrefix?> prefixes,
            int capacity) {

            while (prefixes.Count < capacity) {
                prefixes.Add(null);
            }
        }

        private void Retain(IEnumerable<EventObservation> observations) {
            foreach (EventObservation observation in observations) {
                Retain(observation);
            }
        }

        private void ReleaseOrderedPrefixes(IEnumerable<OrderedTemporalPrefix?> prefixes) {
            foreach (OrderedTemporalPrefix? prefix in prefixes) {
                if (prefix != null) {
                    Release(prefix.Evidence);
                }
            }
        }

        private static long EstimateObservationBytes(IEnumerable<EventObservation> observations) {
            long bytes = 0;
            foreach (EventObservation observation in observations) {
                bytes += EstimateObservationBytes(observation);
            }
            return bytes;
        }

        private sealed class OrderedTemporalPrefix {
            internal OrderedTemporalPrefix(List<EventObservation> evidence) {
                Evidence = evidence;
            }

            internal List<EventObservation> Evidence { get; }
        }
    }
}
