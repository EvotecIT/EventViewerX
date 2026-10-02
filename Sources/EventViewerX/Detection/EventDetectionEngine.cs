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
        foreach (EventDetectionFinding finding in evaluator.Complete()) {
            yield return finding;
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
        foreach (EventDetectionFinding finding in evaluator.Complete()) {
            cancellationToken.ThrowIfCancellationRequested();
            yield return finding;
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
        foreach (EventDetectionFinding finding in evaluator.Complete()) {
            cancellationToken.ThrowIfCancellationRequested();
            yield return finding;
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

}
