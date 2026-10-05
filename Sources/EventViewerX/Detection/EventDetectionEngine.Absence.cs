namespace EventViewerX;

public static partial class EventDetectionEngine {
    internal sealed partial class Evaluator {
        private readonly Dictionary<StateKey, List<EventObservation>> _absenceStates = new();
        private DateTime _absenceLatestUtc = DateTime.MinValue;

        private void ProcessAbsence(EventDetectionPlan.CompiledRule rule, EventObservation observation, ICollection<EventDetectionFinding> findings) {
            if (observation.EventTimeUtc < _absenceLatestUtc) {
                throw new InvalidDataException("Absence rules require event-time ordered input. Sort historical input or configure bounded event-time ordering.");
            }
            _absenceLatestUtc = observation.EventTimeUtc;
            if (!TryResolveGroupValue(rule, observation, findings, out string group)) { return; }
            var key = new StateKey(rule.Definition.RuleId, group);
            bool completes = rule.Steps[1].Matches(observation);
            bool starts = rule.Steps[0].Matches(observation);
            if (_absenceStates.TryGetValue(key, out List<EventObservation>? pending) && completes) {
                // Each completion acknowledges the oldest eligible trigger, exactly once.
                int index = pending.FindIndex(start => observation.EventTimeUtc >= start.EventTimeUtc &&
                    observation.EventTimeUtc - start.EventTimeUtc < rule.Definition.Window);
                if (index >= 0) { Release(new[] { pending[index] }); pending.RemoveAt(index); }
                if (pending.Count == 0) { _absenceStates.Remove(key); pending = null; }
            }
            if (!starts) { return; }
            if (pending == null) {
                if (!CanCreateState(observation, findings)) { return; }
                pending = new List<EventObservation>();
                _absenceStates.Add(key, pending);
            }
            if (!CanRetainObservation(observation, findings)) { return; }
            _ = observation.EventTimeUtc.Add(rule.Definition.Window); // Validate representable deadline before retaining.
            pending.Add(observation); Retain(observation);
        }

        private IEnumerable<EventDetectionFinding> CompleteAbsence() => FinalizeAbsence(_options.AbsenceWindow, false);

        internal IEnumerable<EventDetectionFinding> FinalizeAbsence(EventDetectionAbsenceWindow? window, bool onlyDue) {
            foreach (KeyValuePair<StateKey, List<EventObservation>> state in _absenceStates.ToArray()) {
                EventDetectionPlan.CompiledRule rule = _plan.CompiledRules.First(item =>
                    string.Equals(item.Definition.RuleId, state.Key.RuleId, StringComparison.OrdinalIgnoreCase));
                var finalized = new List<EventObservation>();
                foreach (EventObservation trigger in state.Value) {
                    _cancellationToken.ThrowIfCancellationRequested();
                    DateTime deadline = trigger.EventTimeUtc.Add(rule.Definition.Window);
                    if (onlyDue && (window == null || deadline > window.AsOfUtc)) { continue; }
                    bool complete = !_evaluationIncomplete && !_failedRules.Contains(rule.Definition.RuleId) &&
                        _options.Coverage!.Failures.Count == 0 && (!_options.Coverage.IsDeclared || _options.Coverage.IsComplete) &&
                        window?.Covers(rule.Definition.CoverageScope!, trigger.EventTimeUtc, deadline) == true;
                    string explanation = complete
                        ? $"No completion for trigger {trigger.Identity} occurred before {deadline:O}; exact source/window coverage is complete."
                        : $"Completion is not observed for trigger {trigger.Identity}; absence is uncertain because its deadline or complete source/window coverage is not established.";
                    EventDetectionRuleDefinition definition = rule.Definition;
                    finalized.Add(trigger);
                    yield return new EventDetectionFinding(definition.RuleId, definition.Version, definition.PackId, definition.PackVersion,
                        definition.SourceKind, definition.SourceId, definition.SourceStatus, definition.SourceHash, definition.License,
                        definition.Title, definition.Severity, definition.Confidence,
                        complete ? EventDetectionFindingStatus.Matched : EventDetectionFindingStatus.Incomplete,
                        trigger.EventTimeUtc, deadline, new[] { trigger }, definition.Tags, definition.FalsePositives, definition.References,
                        new Dictionary<string, string> { [definition.GroupBy ?? "Group"] = state.Key.GroupValue },
                        _options.Coverage!, explanation, complete ? null : explanation);
                }
                Release(finalized);
                state.Value.RemoveRange(0, finalized.Count);
                if (state.Value.Count == 0) { _absenceStates.Remove(state.Key); }
            }
        }
    }
}
