namespace EventViewerX;

public static partial class EventDetectionEngine {
    private sealed partial class Evaluator {
        private void PruneUnorderedWindow(
            TemporalState state,
            DateTime current,
            TimeSpan window) {

            DateTime minimum = current - window;
            int removeCount = 0;
            while (removeCount < state.UnorderedEvidence.Count &&
                   state.UnorderedEvidence[removeCount].Observation.EventTimeUtc < minimum) {
                UnorderedTemporalCandidate candidate = state.UnorderedEvidence[removeCount];
                Release(candidate.Observation, candidate.StateBytes);
                removeCount++;
            }
            if (removeCount > 0) {
                state.UnorderedEvidence.RemoveRange(0, removeCount);
            }
        }

        private static int FindRedundantCandidate(
            IReadOnlyList<UnorderedTemporalCandidate> candidates,
            IReadOnlyList<int> matchingSteps) {

            int equivalentCount = 0;
            int oldestEquivalent = -1;
            for (int index = 0; index < candidates.Count; index++) {
                if (!SameSteps(candidates[index].MatchingSteps, matchingSteps)) {
                    continue;
                }
                if (oldestEquivalent < 0) {
                    oldestEquivalent = index;
                }
                equivalentCount++;
            }
            return equivalentCount >= matchingSteps.Count
                ? oldestEquivalent
                : -1;
        }

        private static bool SameSteps(
            IReadOnlyList<int> left,
            IReadOnlyList<int> right) {

            if (left.Count != right.Count) {
                return false;
            }
            for (int index = 0; index < left.Count; index++) {
                if (left[index] != right[index]) {
                    return false;
                }
            }
            return true;
        }

        private static void InsertUnorderedCandidate(
            List<UnorderedTemporalCandidate> candidates,
            UnorderedTemporalCandidate candidate) {

            if (candidates.Count == 0 ||
                candidates[candidates.Count - 1].Observation.EventTimeUtc <= candidate.Observation.EventTimeUtc) {
                candidates.Add(candidate);
                return;
            }
            int low = 0;
            int high = candidates.Count;
            while (low < high) {
                int middle = low + ((high - low) / 2);
                if (candidates[middle].Observation.EventTimeUtc <= candidate.Observation.EventTimeUtc) {
                    low = middle + 1;
                } else {
                    high = middle;
                }
            }
            candidates.Insert(low, candidate);
        }

        private static bool TrySelectUnorderedEvidence(
            EventDetectionPlan.CompiledRule rule,
            IReadOnlyList<UnorderedTemporalCandidate> candidates,
            ref long matchingWork,
            long maximumMatchingWork,
            out EventObservation[] evidence,
            out bool workLimitReached) {

            workLimitReached = false;
            if (candidates.Count < rule.Steps.Length) {
                evidence = Array.Empty<EventObservation>();
                return false;
            }
            var candidateByStep = new int[rule.Steps.Length];
            for (int index = 0; index < candidateByStep.Length; index++) {
                candidateByStep[index] = -1;
            }
            var visitedSteps = new bool[rule.Steps.Length];
            var queuedCandidates = new int[rule.Steps.Length + 1];
            var parentPositions = new int[rule.Steps.Length + 1];
            var parentSteps = new int[rule.Steps.Length + 1];
            int assigned = 0;
            for (int candidateIndex = 0;
                 candidateIndex < candidates.Count && assigned < rule.Steps.Length;
                 candidateIndex++) {
                Array.Clear(visitedSteps, 0, visitedSteps.Length);
                if (TryAssignCandidate(
                        candidateIndex,
                        candidates,
                        candidateByStep,
                        visitedSteps,
                        queuedCandidates,
                        parentPositions,
                        parentSteps,
                        ref matchingWork,
                        maximumMatchingWork,
                        out workLimitReached)) {
                    assigned++;
                } else if (workLimitReached) {
                    evidence = Array.Empty<EventObservation>();
                    return false;
                }
            }
            if (assigned < rule.Steps.Length) {
                evidence = Array.Empty<EventObservation>();
                workLimitReached = false;
                return false;
            }
            evidence = candidateByStep
                .Select(index => candidates[index].Observation)
                .ToArray();
            workLimitReached = false;
            return true;
        }

        private static bool TryAssignCandidate(
            int startingCandidate,
            IReadOnlyList<UnorderedTemporalCandidate> candidates,
            int[] candidateByStep,
            bool[] visitedSteps,
            int[] queuedCandidates,
            int[] parentPositions,
            int[] parentSteps,
            ref long matchingWork,
            long maximumMatchingWork,
            out bool workLimitReached) {

            queuedCandidates[0] = startingCandidate;
            parentPositions[0] = -1;
            parentSteps[0] = -1;
            int readPosition = 0;
            int queuedCount = 1;
            while (readPosition < queuedCount) {
                int candidateIndex = queuedCandidates[readPosition];
                foreach (int stepIndex in candidates[candidateIndex].MatchingSteps) {
                    if (matchingWork >= maximumMatchingWork) {
                        workLimitReached = true;
                        return false;
                    }
                    matchingWork++;
                    if (visitedSteps[stepIndex]) {
                        continue;
                    }
                    visitedSteps[stepIndex] = true;
                    int currentCandidate = candidateByStep[stepIndex];
                    if (currentCandidate < 0) {
                        ApplyAugmentingPath(
                            stepIndex,
                            readPosition,
                            queuedCandidates,
                            parentPositions,
                            parentSteps,
                            candidateByStep);
                        workLimitReached = false;
                        return true;
                    }
                    queuedCandidates[queuedCount] = currentCandidate;
                    parentPositions[queuedCount] = readPosition;
                    parentSteps[queuedCount] = stepIndex;
                    queuedCount++;
                }
                readPosition++;
            }
            workLimitReached = false;
            return false;
        }

        private static void ApplyAugmentingPath(
            int availableStep,
            int candidatePosition,
            IReadOnlyList<int> queuedCandidates,
            IReadOnlyList<int> parentPositions,
            IReadOnlyList<int> parentSteps,
            int[] candidateByStep) {

            int stepIndex = availableStep;
            int position = candidatePosition;
            while (position >= 0) {
                candidateByStep[stepIndex] = queuedCandidates[position];
                stepIndex = parentSteps[position];
                position = parentPositions[position];
            }
        }

        private sealed class UnorderedTemporalCandidate {
            internal UnorderedTemporalCandidate(
                EventObservation observation,
                int[] matchingSteps,
                long stateBytes) {

                Observation = observation;
                MatchingSteps = matchingSteps;
                StateBytes = stateBytes;
            }

            internal EventObservation Observation { get; }
            internal int[] MatchingSteps { get; }
            internal long StateBytes { get; }
        }
    }
}
