namespace EventViewerX.Reporting;

/// <summary>Adapts shared aggregation results to the normal report and export contract.</summary>
public static class EventAggregationReportFactory {
    /// <summary>Creates a homogeneous report without discarding aggregation completeness evidence.</summary>
    public static EventReport Create(EventAggregationResult result, string? title = null) {
        if (result == null) {
            throw new ArgumentNullException(nameof(result));
        }
        var columns = new List<EventReportColumnSchema> {
            Column("ResultKind", "Result kind", typeof(string)),
            Column("InputCompleteness", "Input completeness", typeof(string)),
            Column("AggregationComplete", "Aggregation complete", typeof(bool)),
            Column("Diagnostic", "Diagnostic", typeof(string)),
            Column("ExecutionMode", "Execution mode", typeof(string)),
            Column("InputRows", "Input rows", typeof(long)),
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
                ["ResultKind"] = "AggregationRow",
                ["InputCompleteness"] = result.InputCompleteness.ToString(),
                ["AggregationComplete"] = result.AggregationComplete,
                ["Diagnostic"] = result.Diagnostic,
                ["ExecutionMode"] = result.ExecutionMode.ToString(),
                ["InputRows"] = result.InputRows,
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
        if (rows.Length == 0) {
            rows = new[] {
                new EventReportRow {
                    Type = "EventAggregation",
                    Values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) {
                        ["ResultKind"] = "ResultMetadata",
                        ["InputCompleteness"] = result.InputCompleteness.ToString(),
                        ["AggregationComplete"] = result.AggregationComplete,
                        ["Diagnostic"] = result.Diagnostic,
                        ["ExecutionMode"] = result.ExecutionMode.ToString(),
                        ["InputRows"] = result.InputRows
                    }
                }
            };
        }
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
