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
        var usedColumnNames = new HashSet<string>(
            columns.Select(static column => column.Name),
            StringComparer.OrdinalIgnoreCase);
        KeyValuePair<string, string>[] groupColumns = result.Definition.GroupBy
            .Select(field => new KeyValuePair<string, string>(
                field,
                CreateUniqueDataColumnName(field, "Group", usedColumnNames)))
            .ToArray();
        (EventAggregationMeasure Measure, string Name)[] measureColumns = result.Definition.Measures
            .Select(measure => (
                measure,
                CreateUniqueDataColumnName(measure.OutputName!, "Measure", usedColumnNames)))
            .ToArray();
        columns.AddRange(groupColumns.Select(binding => Column(
            binding.Value,
            binding.Key,
            typeof(object))));
        columns.AddRange(measureColumns.Select(binding => Column(
            binding.Name,
            binding.Measure.OutputName!,
            binding.Measure.Operation switch {
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
            foreach (KeyValuePair<string, string> binding in groupColumns) {
                values[binding.Value] = row.Group[binding.Key];
            }
            foreach ((EventAggregationMeasure measure, string name) in measureColumns) {
                values[name] = row.Measures[measure.OutputName!];
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

    private static string CreateUniqueDataColumnName(
        string requested,
        string prefix,
        ISet<string> usedNames) {

        if (usedNames.Add(requested)) {
            return requested;
        }
        string root = prefix + "." + requested;
        string candidate = root;
        int suffix = 2;
        while (!usedNames.Add(candidate)) {
            candidate = root + "." + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
            suffix++;
        }
        return candidate;
    }
}
