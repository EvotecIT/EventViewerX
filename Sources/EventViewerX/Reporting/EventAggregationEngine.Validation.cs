namespace EventViewerX.Reporting;

public static partial class EventAggregationEngine {
    internal static EventAggregationDefinition ValidateAndSnapshot(EventAggregationDefinition definition) {
        if (definition == null) {
            throw new ArgumentNullException(nameof(definition));
        }
        if (!Enum.IsDefined(typeof(EventAggregationBucket), definition.Bucket) ||
            !Enum.IsDefined(typeof(EventAggregationTopScope), definition.TopScope) ||
            !Enum.IsDefined(typeof(EventAggregationNullPolicy), definition.GroupNulls)) {
            throw new ArgumentException("Aggregation contains an undefined enum value.", nameof(definition));
        }
        if (definition.Top < 0 || definition.MaximumGroups <= 0 ||
            definition.MaximumDistinctValues <= 0 || definition.MaximumStateBytes <= 0) {
            throw new ArgumentOutOfRangeException(nameof(definition), "Aggregation bounds must be positive and Top cannot be negative.");
        }
        string[] groupBy = (definition.GroupBy ?? Array.Empty<string>())
            .Where(static field => !string.IsNullOrWhiteSpace(field))
            .Select(static field => field.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (groupBy.Length != (definition.GroupBy ?? Array.Empty<string>()).Count) {
            throw new ArgumentException("GroupBy fields must be non-empty and unique case-insensitively.", nameof(definition));
        }
        EventAggregationMeasure[] measures = (definition.Measures ?? Array.Empty<EventAggregationMeasure>())
            .Select(SnapshotMeasure)
            .ToArray();
        if (measures.Length == 0) {
            throw new ArgumentException("At least one aggregation measure is required.", nameof(definition));
        }
        var names = new HashSet<string>(groupBy, StringComparer.OrdinalIgnoreCase);
        foreach (EventAggregationMeasure measure in measures) {
            if (!names.Add(measure.OutputName!)) {
                throw new ArgumentException(
                    $"Aggregation output name '{measure.OutputName}' conflicts with a group field or another measure.",
                    nameof(definition));
            }
        }
        string ranking = string.IsNullOrWhiteSpace(definition.RankingMeasure)
            ? measures[0].OutputName!
            : definition.RankingMeasure!.Trim();
        if (!measures.Any(measure => string.Equals(
                measure.OutputName,
                ranking,
                StringComparison.OrdinalIgnoreCase))) {
            throw new ArgumentException($"RankingMeasure '{ranking}' does not identify a measure output.", nameof(definition));
        }
        DateTime? start = definition.WindowStart.HasValue
            ? NormalizeDateTimeUtc(definition.WindowStart.Value)
            : null;
        DateTime? end = definition.WindowEnd.HasValue
            ? NormalizeDateTimeUtc(definition.WindowEnd.Value)
            : null;
        if (start.HasValue != end.HasValue || start.HasValue && start >= end) {
            throw new ArgumentException("WindowStart and WindowEnd must define one non-zero ordered UTC interval.", nameof(definition));
        }
        if (measures.Any(static measure => measure.Operation == EventAggregationOperation.Rate) &&
            definition.Bucket == EventAggregationBucket.None && !start.HasValue) {
            throw new ArgumentException("An unbucketed Rate requires WindowStart and WindowEnd.", nameof(definition));
        }
        string timeZoneId = string.IsNullOrWhiteSpace(definition.TimeZoneId)
            ? "UTC"
            : definition.TimeZoneId.Trim();
        _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return new EventAggregationDefinition {
            GroupBy = groupBy,
            Bucket = definition.Bucket,
            TimeZoneId = timeZoneId,
            Measures = measures,
            Top = definition.Top,
            TopScope = definition.TopScope,
            RankingMeasure = ranking,
            GroupNulls = definition.GroupNulls,
            WindowStart = start,
            WindowEnd = end,
            MaximumGroups = definition.MaximumGroups,
            MaximumDistinctValues = definition.MaximumDistinctValues,
            MaximumStateBytes = definition.MaximumStateBytes
        };
    }

    private static EventAggregationMeasure SnapshotMeasure(EventAggregationMeasure measure) {
        if (measure == null) {
            throw new ArgumentException("Measures cannot contain null values.", nameof(measure));
        }
        if (!Enum.IsDefined(typeof(EventAggregationOperation), measure.Operation) ||
            !Enum.IsDefined(typeof(EventAggregationNullPolicy), measure.Nulls)) {
            throw new ArgumentException("A measure contains an undefined enum value.", nameof(measure));
        }
        string? field = string.IsNullOrWhiteSpace(measure.Field) ? null : measure.Field!.Trim();
        if (measure.Operation is EventAggregationOperation.DistinctCount or
            EventAggregationOperation.FirstSeen or EventAggregationOperation.LastSeen && field == null) {
            throw new ArgumentException($"{measure.Operation} requires a field operand.", nameof(measure));
        }
        if (measure.Operation is EventAggregationOperation.Count or EventAggregationOperation.Rate && field != null) {
            throw new ArgumentException($"{measure.Operation} does not accept a field operand.", nameof(measure));
        }
        if (measure.Operation == EventAggregationOperation.Rate &&
            (!measure.RateUnit.HasValue || measure.RateUnit.Value <= TimeSpan.Zero)) {
            throw new ArgumentException("Rate requires a positive RateUnit.", nameof(measure));
        }
        string outputName = string.IsNullOrWhiteSpace(measure.OutputName)
            ? measure.Operation + (field == null ? string.Empty : field)
            : measure.OutputName!.Trim();
        return new EventAggregationMeasure {
            Operation = measure.Operation,
            Field = field,
            OutputName = outputName,
            Nulls = measure.Nulls,
            RateUnit = measure.RateUnit
        };
    }
}
