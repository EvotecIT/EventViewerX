using System.Globalization;

namespace EventViewerX.Reporting;

/// <summary>Executes bounded, deterministic managed aggregations over normalized event rows.</summary>
public static partial class EventAggregationEngine {
    /// <summary>Aggregates a report while preserving its source-completeness evidence.</summary>
    public static EventAggregationResult Aggregate(EventReport report, EventAggregationDefinition definition) {
        if (report == null) {
            throw new ArgumentNullException(nameof(report));
        }
        EventAggregationInputCompleteness completeness = report.ScanLimitReached ||
            report.Coverage.Any(static coverage => !coverage.Succeeded)
                ? EventAggregationInputCompleteness.Incomplete
                : EventAggregationInputCompleteness.Complete;
        return AggregateCore(report.Rows, definition, completeness, report.CompletenessDiagnostic);
    }

    /// <summary>Aggregates source rows. Plain row collections are incomplete unless evidence says otherwise.</summary>
    public static EventAggregationResult Aggregate(
        IEnumerable<EventReportRow> rows,
        EventAggregationDefinition definition,
        EventAggregationInputCompleteness inputCompleteness = EventAggregationInputCompleteness.Unknown) {

        return AggregateCore(rows, definition, inputCompleteness, inputDiagnostic: null);
    }

    private static EventAggregationResult AggregateCore(
        IEnumerable<EventReportRow> rows,
        EventAggregationDefinition definition,
        EventAggregationInputCompleteness inputCompleteness,
        string? inputDiagnostic) {

        if (rows == null) {
            throw new ArgumentNullException(nameof(rows));
        }
        if (!Enum.IsDefined(typeof(EventAggregationInputCompleteness), inputCompleteness)) {
            throw new ArgumentOutOfRangeException(nameof(inputCompleteness));
        }
        EventAggregationDefinition snapshot = ValidateAndSnapshot(definition);
        EventReportRow[] source = rows.ToArray();
        if (source.Any(static row => row == null)) {
            throw new ArgumentException("Aggregation input cannot contain null rows.", nameof(rows));
        }
        try {
            var states = new Dictionary<string, AggregationState>(StringComparer.Ordinal);
            var rankingStates = new Dictionary<string, AggregationState>(StringComparer.Ordinal);
            bool requiresGlobalRanking = snapshot.Top > 0 &&
                snapshot.Bucket != EventAggregationBucket.None &&
                snapshot.TopScope == EventAggregationTopScope.GlobalGroup;
            EventAggregationMeasure[] rankingMeasures = requiresGlobalRanking
                ? snapshot.Measures.Where(measure => string.Equals(
                    measure.OutputName,
                    snapshot.RankingMeasure,
                    StringComparison.OrdinalIgnoreCase)).ToArray()
                : Array.Empty<EventAggregationMeasure>();
            long stateBytes = 0;
            foreach (EventReportRow row in source) {
                IReadOnlyDictionary<string, object?> values = row.ToNormalizedDictionary();
                if (!TryCreateGroup(snapshot, values, out AggregationGroup group)) {
                    continue;
                }
                AggregationBucketRange bucket = CreateBucket(snapshot, row.TimeCreated);
                string stateKey = bucket.Identity + "\0" + group.Identity;
                AggregationState state = GetOrCreate(
                    states,
                    stateKey,
                    group,
                    bucket,
                    snapshot,
                    ref stateBytes);
                stateBytes += state.Add(row, values, snapshot.MaximumDistinctValues);
                if (requiresGlobalRanking) {
                    AggregationState ranking = GetOrCreate(
                        rankingStates,
                        group.Identity,
                        group,
                        AggregationBucketRange.None,
                        snapshot,
                        ref stateBytes,
                        rankingMeasures);
                    stateBytes += ranking.Add(row, values, snapshot.MaximumDistinctValues);
                }
                if (stateBytes > snapshot.MaximumStateBytes) {
                    throw new AggregationBoundException(
                        $"Aggregation state exceeded MaximumStateBytes {snapshot.MaximumStateBytes:N0}.");
                }
            }
            IReadOnlyList<AggregationState> selected = ApplyTop(states.Values, rankingStates, snapshot);
            EventAggregationRow[] resultRows = selected
                .OrderBy(static state => state.Bucket.StartUtc)
                .ThenBy(static state => state.Group.Identity, StringComparer.Ordinal)
                .Select(state => state.CreateRow(snapshot))
                .ToArray();
            string? completenessDiagnostic = inputCompleteness switch {
                EventAggregationInputCompleteness.Unknown =>
                    "Aggregation is exhaustive for supplied rows, but source-query completeness is unknown.",
                EventAggregationInputCompleteness.Incomplete =>
                    "Aggregation is exhaustive for supplied rows, but at least one source query was incomplete.",
                _ => null
            };
            string? diagnostic = EventCompletenessDiagnostic.Compose(
                inputDiagnostic,
                completenessDiagnostic);
            return new EventAggregationResult(
                snapshot,
                resultRows,
                inputCompleteness,
                aggregationComplete: true,
                diagnostic,
                EventAggregationExecutionMode.Managed,
                source.LongLength);
        } catch (AggregationBoundException exception) {
            return new EventAggregationResult(
                snapshot,
                Array.Empty<EventAggregationRow>(),
                inputCompleteness,
                aggregationComplete: false,
                EventCompletenessDiagnostic.Compose(exception.Message, inputDiagnostic),
                EventAggregationExecutionMode.Managed,
                source.LongLength);
        }
    }

    private static AggregationState GetOrCreate(
        IDictionary<string, AggregationState> states,
        string key,
        AggregationGroup group,
        AggregationBucketRange bucket,
        EventAggregationDefinition definition,
        ref long stateBytes,
        IReadOnlyList<EventAggregationMeasure>? measures = null) {

        if (states.TryGetValue(key, out AggregationState? state)) {
            state.MergeGroupDisplay(group);
            return state;
        }
        if (states.Count >= definition.MaximumGroups) {
            throw new AggregationBoundException(
                $"Aggregation group count exceeds MaximumGroups {definition.MaximumGroups:N0}.");
        }
        state = new AggregationState(group, bucket, measures ?? definition.Measures);
        states.Add(key, state);
        stateBytes += state.EstimatedBytes;
        return state;
    }

    private static IReadOnlyList<AggregationState> ApplyTop(
        IEnumerable<AggregationState> states,
        IReadOnlyDictionary<string, AggregationState> rankingStates,
        EventAggregationDefinition definition) {

        AggregationState[] values = states.ToArray();
        if (definition.Top == 0 || values.Length <= definition.Top) {
            return values;
        }
        string rankingName = definition.RankingMeasure!;
        if (definition.Bucket == EventAggregationBucket.None) {
            return values
                .OrderByDescending(state => state.GetRankingValue(rankingName, definition), AggregationValueComparer.Instance)
                .ThenBy(static state => state.Group.Identity, StringComparer.Ordinal)
                .Take(definition.Top)
                .ToArray();
        }
        if (definition.TopScope == EventAggregationTopScope.GlobalGroup) {
            HashSet<string> retained = rankingStates.Values
                .OrderByDescending(state => state.GetRankingValue(rankingName, definition), AggregationValueComparer.Instance)
                .ThenBy(static state => state.Group.Identity, StringComparer.Ordinal)
                .Take(definition.Top)
                .Select(static state => state.Group.Identity)
                .ToHashSet(StringComparer.Ordinal);
            return values.Where(state => retained.Contains(state.Group.Identity)).ToArray();
        }
        return values.GroupBy(static state => state.Bucket.Identity, StringComparer.Ordinal)
            .SelectMany(group => group
                .OrderByDescending(state => state.GetRankingValue(rankingName, definition), AggregationValueComparer.Instance)
                .ThenBy(static state => state.Group.Identity, StringComparer.Ordinal)
                .Take(definition.Top))
            .ToArray();
    }

    private sealed class AggregationValueComparer : IComparer<object?> {
        internal static readonly AggregationValueComparer Instance = new();

        public int Compare(object? x, object? y) {
            if (ReferenceEquals(x, y)) {
                return 0;
            }
            if (x == null) {
                return -1;
            }
            if (y == null) {
                return 1;
            }
            if (x is IConvertible && y is IConvertible &&
                x is not DateTime && y is not DateTime) {
                try {
                    return Convert.ToDecimal(x, CultureInfo.InvariantCulture)
                        .CompareTo(Convert.ToDecimal(y, CultureInfo.InvariantCulture));
                } catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException) {
                }
            }
            if (x is IComparable comparable && x.GetType().IsInstanceOfType(y)) {
                return comparable.CompareTo(y);
            }
            return string.CompareOrdinal(
                Convert.ToString(x, CultureInfo.InvariantCulture),
                Convert.ToString(y, CultureInfo.InvariantCulture));
        }
    }

    internal static IComparer<object?> ValueComparer => AggregationValueComparer.Instance;
}

internal sealed class AggregationBoundException : Exception {
    internal AggregationBoundException(string message) : base(message) {
    }
}
