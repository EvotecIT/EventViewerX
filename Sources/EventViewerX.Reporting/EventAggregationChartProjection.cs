using System.Globalization;

namespace EventViewerX.Reporting;

internal static class EventAggregationChartProjection {
    internal static EventAggregationChartData? Create(EventAggregationResult result, int maximumSeries = 12) {
        EventAggregationMeasure? measure = result.Definition.Measures.FirstOrDefault(static item =>
            item.Operation is EventAggregationOperation.Count or EventAggregationOperation.DistinctCount or EventAggregationOperation.Rate);
        if (measure == null || result.Definition.Bucket == EventAggregationBucket.None || result.Rows.Count == 0) {
            return null;
        }
        string[] categories = result.Rows
            .Where(static row => row.BucketStartUtc.HasValue)
            .OrderBy(static row => row.BucketStartUtc)
            .Select(static row => row.BucketLabel ?? row.BucketStartUtc!.Value.ToString("O", CultureInfo.InvariantCulture))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (categories.Length == 0) {
            return null;
        }
        var grouped = result.Rows
            .GroupBy(row => CreateSeriesLabel(row, result.Definition.GroupBy), StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .ToArray();
        EventAggregationChartSeries[] series = grouped.Take(maximumSeries).Select(group => {
            Dictionary<string, double> values = group.ToDictionary(
                static row => row.BucketLabel ?? row.BucketStartUtc!.Value.ToString("O", CultureInfo.InvariantCulture),
                row => Convert.ToDouble(row.Measures[measure.OutputName!], CultureInfo.InvariantCulture),
                StringComparer.Ordinal);
            return new EventAggregationChartSeries(
                group.Key,
                categories.Select(category => values.TryGetValue(category, out double value) ? (double?)value : null).ToArray());
        }).ToArray();
        return new EventAggregationChartData(
            measure.OutputName!,
            categories,
            series,
            grouped.Length > series.Length);
    }

    private static string CreateSeriesLabel(EventAggregationRow row, IReadOnlyList<string> dimensions) {
        if (dimensions.Count == 0) {
            return "All events";
        }
        return string.Join(" · ", dimensions.Select(dimension =>
            dimension + "=" + (Convert.ToString(row.Group[dimension], CultureInfo.InvariantCulture) ?? "(null)")));
    }
}

internal sealed class EventAggregationChartData {
    internal EventAggregationChartData(
        string measure,
        IReadOnlyList<string> categories,
        IReadOnlyList<EventAggregationChartSeries> series,
        bool isTruncated) {
        Measure = measure;
        Categories = categories;
        Series = series;
        IsTruncated = isTruncated;
    }
    internal string Measure { get; }
    internal IReadOnlyList<string> Categories { get; }
    internal IReadOnlyList<EventAggregationChartSeries> Series { get; }
    internal bool IsTruncated { get; }
}

internal sealed class EventAggregationChartSeries {
    internal EventAggregationChartSeries(string name, IReadOnlyList<double?> points) {
        Name = name;
        Points = points;
    }
    internal string Name { get; }
    internal IReadOnlyList<double?> Points { get; }
}
