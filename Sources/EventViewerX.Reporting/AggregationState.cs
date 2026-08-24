using System.Globalization;

namespace EventViewerX.Reporting;

internal sealed class AggregationState {
    private readonly IReadOnlyList<EventAggregationMeasure> _definitions;
    private readonly Dictionary<string, MeasureState> _measures;

    internal AggregationState(
        AggregationGroup group,
        AggregationBucketRange bucket,
        IReadOnlyList<EventAggregationMeasure> measures) {

        Group = group;
        Bucket = bucket;
        _definitions = measures;
        _measures = measures.ToDictionary(
            static measure => measure.OutputName!,
            static measure => new MeasureState(measure),
            StringComparer.OrdinalIgnoreCase);
        EstimatedBytes = EstimateBytes(group.Identity, bucket.Identity, measures);
    }

    internal AggregationGroup Group { get; }
    internal AggregationBucketRange Bucket { get; }
    internal long EstimatedBytes { get; }

    internal static long EstimateBytes(
        string groupIdentity,
        string bucketIdentity,
        IReadOnlyList<EventAggregationMeasure> measures) {

        long bytes = checked(256L + groupIdentity.Length * 2L + bucketIdentity.Length * 2L);
        foreach (EventAggregationMeasure measure in measures) {
            bytes = checked(bytes + 192L +
                (measure.OutputName?.Length ?? 0) * 2L +
                (measure.Field?.Length ?? 0) * 2L);
        }
        return bytes;
    }

    internal void MergeGroupDisplay(AggregationGroup candidate) => Group.MergeDisplay(candidate);

    internal long Add(
        EventReportRow row,
        IReadOnlyDictionary<string, object?> values,
        int maximumDistinctValues) {

        long bytes = 0;
        foreach (EventAggregationMeasure definition in _definitions) {
            bytes += _measures[definition.OutputName!].Add(row, values, maximumDistinctValues);
        }
        return bytes;
    }

    internal EventAggregationRow CreateRow(EventAggregationDefinition definition) {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (EventAggregationMeasure measure in _definitions) {
            values[measure.OutputName!] = _measures[measure.OutputName!].GetValue(
                Bucket,
                definition.WindowStart,
                definition.WindowEnd);
        }
        return new EventAggregationRow {
            Group = Group.Dimensions.ToDictionary(
                static item => item.Key,
                static item => item.Value,
                StringComparer.OrdinalIgnoreCase),
            BucketStartUtc = Bucket.StartUtc,
            BucketEndUtc = Bucket.EndUtc,
            BucketLabel = Bucket.Label,
            Measures = values
        };
    }

    internal object? GetRankingValue(string outputName, EventAggregationDefinition definition) =>
        _measures.TryGetValue(outputName, out MeasureState? state)
            ? state.GetValue(Bucket, definition.WindowStart, definition.WindowEnd)
            : null;

    private sealed class MeasureState {
        private readonly EventAggregationMeasure _definition;
        private HashSet<string>? _distinct;
        private long _count;
        private DateTime? _first;
        private DateTime? _last;

        internal MeasureState(EventAggregationMeasure definition) {
            _definition = definition;
        }

        internal long Add(
            EventReportRow row,
            IReadOnlyDictionary<string, object?> values,
            int maximumDistinctValues) {

            _count++;
            if (_definition.Operation is EventAggregationOperation.Count or EventAggregationOperation.Rate) {
                return 0;
            }
            values.TryGetValue(_definition.Field!, out object? operand);
            if (_definition.Operation == EventAggregationOperation.DistinctCount) {
                if (operand == null && _definition.Nulls == EventAggregationNullPolicy.Exclude) {
                    return 0;
                }
                string key = EventAggregationEngine.Canonicalize(operand);
                _distinct ??= new HashSet<string>(StringComparer.Ordinal);
                if (_distinct.Contains(key)) {
                    return 0;
                }
                if (_distinct.Count >= maximumDistinctValues) {
                    throw new AggregationBoundException(
                        $"Distinct measure '{_definition.OutputName}' exceeds MaximumDistinctValues {maximumDistinctValues:N0}.");
                }
                _distinct.Add(key);
                return 64 + key.Length * 2L;
            }
            if (operand == null) {
                return 0;
            }
            DateTime value = ConvertDateTime(operand, _definition.Field!);
            if (!_first.HasValue || value < _first.Value) {
                _first = value;
            }
            if (!_last.HasValue || value > _last.Value) {
                _last = value;
            }
            return 0;
        }

        internal object? GetValue(
            AggregationBucketRange bucket,
            DateTime? windowStart,
            DateTime? windowEnd) => _definition.Operation switch {
                EventAggregationOperation.Count => _count,
                EventAggregationOperation.DistinctCount => (long)(_distinct?.Count ?? 0),
                EventAggregationOperation.FirstSeen => _first,
                EventAggregationOperation.LastSeen => _last,
                EventAggregationOperation.Rate => GetRate(bucket, windowStart, windowEnd),
                _ => throw new ArgumentOutOfRangeException()
            };

        private double GetRate(
            AggregationBucketRange bucket,
            DateTime? windowStart,
            DateTime? windowEnd) {

            DateTime start = bucket.StartUtc ?? windowStart ?? throw new InvalidOperationException("Rate start is unavailable.");
            DateTime end = bucket.EndUtc ?? windowEnd ?? throw new InvalidOperationException("Rate end is unavailable.");
            double units = (end - start).Ticks / (double)_definition.RateUnit!.Value.Ticks;
            if (units <= 0) {
                throw new InvalidOperationException("Rate interval must be positive.");
            }
            return _count / units;
        }

        private static DateTime ConvertDateTime(object value, string field) {
            if (value is DateTime date) {
                return date.ToUniversalTime();
            }
            if (value is DateTimeOffset offset) {
                return offset.UtcDateTime;
            }
            string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            if (DateTime.TryParse(
                    text,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTime parsed)) {
                return parsed.ToUniversalTime();
            }
            throw new ArgumentException($"Aggregation field '{field}' value '{text}' is not a date-time.");
        }
    }
}
