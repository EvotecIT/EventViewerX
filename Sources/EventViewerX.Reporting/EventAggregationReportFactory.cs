namespace EventViewerX.Reporting;

/// <summary>Adapts shared aggregation results to the normal report and export contract.</summary>
public static class EventAggregationReportFactory {
    /// <summary>Creates a homogeneous report without discarding aggregation completeness evidence.</summary>
    public static EventReport Create(EventAggregationResult result, string? title = null) {
        if (result == null) {
            throw new ArgumentNullException(nameof(result));
        }
        var columns = new List<EventReportColumnSchema> {
            Column("BucketStartUtc", "Bucket start", typeof(DateTime?)),
            Column("BucketEndUtc", "Bucket end", typeof(DateTime?)),
            Column("BucketLabel", "Bucket", typeof(string))
        };
        columns.AddRange(result.Definition.GroupBy.Select(field => Column(field, field, typeof(object))));
        columns.AddRange(result.Definition.Measures.Select(measure => Column(
            measure.OutputName!,
            measure.OutputName!,
            measure.Operation switch {
                EventAggregationOperation.Count or EventAggregationOperation.DistinctCount => typeof(long),
                EventAggregationOperation.Rate => typeof(double),
                EventAggregationOperation.FirstSeen or EventAggregationOperation.LastSeen => typeof(DateTime?),
                _ => typeof(object)
            })));
        var schema = new EventReportSectionSchema {
            Name = "EventAggregation",
            DisplayName = "Event aggregation",
            Description = result.Diagnostic ?? "Deterministic EventViewerX aggregation rows.",
            Kind = EventReportSectionKind.Custom,
            Columns = columns
        };
        EventReportRow[] rows = result.Rows.Select(row => {
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) {
                ["BucketStartUtc"] = row.BucketStartUtc,
                ["BucketEndUtc"] = row.BucketEndUtc,
                ["BucketLabel"] = row.BucketLabel
            };
            foreach (KeyValuePair<string, object?> value in row.Group) {
                values[value.Key] = value.Value;
            }
            foreach (KeyValuePair<string, object?> value in row.Measures) {
                values[value.Key] = value.Value;
            }
            return new EventReportRow {
                TimeCreated = row.BucketStartUtc ?? result.Definition.WindowStart ??
                    new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Type = "EventAggregation",
                Values = values
            };
        }).ToArray();
        return EventReportEngine.CreateStored(
            rows,
            new[] { schema },
            string.IsNullOrWhiteSpace(title) ? "EventViewerX aggregation" : title,
            eventsScanned: result.InputRows,
            scanLimitReached: !result.IsComplete);
    }

    private static EventReportColumnSchema Column(string name, string displayName, Type type) => new() {
        Name = name,
        DisplayName = displayName,
        ValueTypeName = EventReportColumnSchema.GetStableTypeName(type)
    };
}
