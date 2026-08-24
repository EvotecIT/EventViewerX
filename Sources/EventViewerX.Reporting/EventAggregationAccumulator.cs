namespace EventViewerX.Reporting;

/// <summary>Incrementally computes one bounded aggregation without retaining source rows.</summary>
public sealed class EventAggregationAccumulator {
    private readonly EventAggregationDefinition _definition;
    private readonly EventAggregationInputCompleteness _inputCompleteness;
    private readonly string? _inputDiagnostic;
    private readonly Dictionary<string, AggregationState> _states = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AggregationState> _rankingStates = new(StringComparer.Ordinal);
    private readonly bool _requiresGlobalRanking;
    private readonly EventAggregationMeasure[] _rankingMeasures;
    private long _inputRows;
    private long _stateBytes;
    private string? _boundDiagnostic;
    private bool _completed;

    internal EventAggregationAccumulator(
        EventAggregationDefinition definition,
        EventAggregationInputCompleteness inputCompleteness,
        string? inputDiagnostic) {

        if (!Enum.IsDefined(typeof(EventAggregationInputCompleteness), inputCompleteness)) {
            throw new ArgumentOutOfRangeException(nameof(inputCompleteness));
        }
        _definition = EventAggregationEngine.ValidateAndSnapshot(definition);
        _inputCompleteness = inputCompleteness;
        _inputDiagnostic = inputDiagnostic;
        _requiresGlobalRanking = _definition.Top > 0 &&
            _definition.Bucket != EventAggregationBucket.None &&
            _definition.TopScope == EventAggregationTopScope.GlobalGroup;
        _rankingMeasures = ResolveRankingMeasures(_definition, _requiresGlobalRanking);
    }

    /// <summary>Whether additional rows can still be evaluated within the configured bounds.</summary>
    public bool CanAcceptRows => !_completed && _boundDiagnostic == null;

    /// <summary>Adds one row. Returns false after an aggregation bound is reached.</summary>
    public bool Add(EventReportRow row) {
        if (_completed) {
            throw new InvalidOperationException("The aggregation accumulator is already complete.");
        }
        if (_boundDiagnostic != null) {
            return false;
        }
        if (row == null) {
            throw new ArgumentNullException(nameof(row));
        }
        _inputRows = checked(_inputRows + 1);
        try {
            if (row.NormalizedValues.Count == 0 && row.Values.Count > 0) {
                EventValueNormalizationEngine.Populate(row);
            }
            IReadOnlyDictionary<string, object?> values = row.ToNormalizedDictionary();
            if (!EventAggregationEngine.TryCreateGroup(_definition, values, out AggregationGroup group)) {
                return true;
            }
            AggregationBucketRange bucket = EventAggregationEngine.CreateBucket(_definition, row.TimeCreated);
            string stateKey = bucket.Identity + "\0" + group.Identity;
            AggregationState state = EventAggregationEngine.GetOrCreate(
                _states,
                stateKey,
                group,
                bucket,
                _definition,
                ref _stateBytes,
                auxiliaryStateCount: _rankingStates.Count);
            _stateBytes += state.Add(row, values, _definition.MaximumDistinctValues);
            if (_requiresGlobalRanking) {
                AggregationState ranking = EventAggregationEngine.GetOrCreate(
                    _rankingStates,
                    group.Identity,
                    group,
                    AggregationBucketRange.None,
                    _definition,
                    ref _stateBytes,
                    _rankingMeasures,
                    _states.Count);
                _stateBytes += ranking.Add(row, values, _definition.MaximumDistinctValues);
            }
            if (_stateBytes > _definition.MaximumStateBytes) {
                throw new AggregationBoundException(
                    $"Aggregation state exceeded MaximumStateBytes {_definition.MaximumStateBytes:N0}.");
            }
            return true;
        } catch (AggregationBoundException exception) {
            _boundDiagnostic = exception.Message;
            _states.Clear();
            _rankingStates.Clear();
            _stateBytes = 0;
            return false;
        }
    }

    /// <summary>Completes the aggregation and returns its bounded result.</summary>
    public EventAggregationResult Complete() {
        return Complete(_inputCompleteness, _inputDiagnostic);
    }

    internal EventAggregationResult Complete(
        EventAggregationInputCompleteness inputCompleteness,
        string? inputDiagnostic) {

        if (_completed) {
            throw new InvalidOperationException("The aggregation accumulator is already complete.");
        }
        _completed = true;
        if (_boundDiagnostic != null) {
            return new EventAggregationResult(
                _definition,
                Array.Empty<EventAggregationRow>(),
                inputCompleteness,
                aggregationComplete: false,
                EventCompletenessDiagnostic.Compose(_boundDiagnostic, inputDiagnostic),
                EventAggregationExecutionMode.Managed,
                _inputRows);
        }
        IReadOnlyList<AggregationState> selected = EventAggregationEngine.ApplyTop(
            _states.Values,
            _rankingStates,
            _definition);
        EventAggregationRow[] rows = selected
            .OrderBy(static state => state.Bucket.StartUtc)
            .ThenBy(static state => state.Group.Identity, StringComparer.Ordinal)
            .Select(state => state.CreateRow(_definition))
            .ToArray();
        string? completenessDiagnostic = inputCompleteness switch {
            EventAggregationInputCompleteness.Unknown =>
                "Aggregation is exhaustive for supplied rows, but source-query completeness is unknown.",
            EventAggregationInputCompleteness.Incomplete =>
                "Aggregation is exhaustive for supplied rows, but at least one source query was incomplete.",
            _ => null
        };
        return new EventAggregationResult(
            _definition,
            rows,
            inputCompleteness,
            aggregationComplete: true,
            EventCompletenessDiagnostic.Compose(inputDiagnostic, completenessDiagnostic),
            EventAggregationExecutionMode.Managed,
            _inputRows);
    }

    private static EventAggregationMeasure[] ResolveRankingMeasures(
        EventAggregationDefinition definition,
        bool requiresGlobalRanking) {

        if (!requiresGlobalRanking) {
            return Array.Empty<EventAggregationMeasure>();
        }
        EventAggregationMeasure[] measures = definition.Measures.Where(measure => string.Equals(
            measure.OutputName,
            definition.RankingMeasure,
            StringComparison.OrdinalIgnoreCase)).ToArray();
        if (measures.Length == 1 && measures[0].Operation == EventAggregationOperation.Rate) {
            return new[] {
                new EventAggregationMeasure {
                    Operation = EventAggregationOperation.Count,
                    OutputName = measures[0].OutputName,
                    Nulls = measures[0].Nulls
                }
            };
        }
        return measures;
    }
}
