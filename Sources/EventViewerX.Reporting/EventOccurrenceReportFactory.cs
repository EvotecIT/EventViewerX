namespace EventViewerX.Reporting;

/// <summary>Adapts non-destructive occurrence groups to the normal report/export contract.</summary>
public static class EventOccurrenceReportFactory {
    /// <summary>Creates a report with one row per logical occurrence while the source result retains all observations.</summary>
    public static EventReport Create(EventOccurrenceResult result, string? title = null) {
        return Create(result, sourceReport: null, title);
    }

    /// <summary>Creates an occurrence report while retaining the source query completeness envelope.</summary>
    public static EventReport Create(
        EventOccurrenceResult result,
        EventReport? sourceReport,
        string? title = null) {

        if (result == null) {
            throw new ArgumentNullException(nameof(result));
        }
        var schema = new EventReportSectionSchema {
            Name = "EventOccurrence",
            DisplayName = "Event occurrences",
            Description = result.Diagnostic ?? "Non-destructive logical occurrence groups.",
            Kind = EventReportSectionKind.Custom,
            Columns = new[] {
                Column("OccurrenceId", "Occurrence ID", typeof(string)),
                Column("RepresentativeType", "Type", typeof(string)),
                Column("ObservationCount", "Observations", typeof(int)),
                Column("SourceComputers", "Source computers", typeof(string)),
                Column("RecordIds", "Record IDs", typeof(string)),
                Column("Policy", "Policy", typeof(string)),
                Column("MatchReason", "Match reason", typeof(string))
            }
        };
        EventReportRow[] rows = result.Groups.Select(group => new EventReportRow {
            TimeCreated = group.Representative.TimeCreated,
            Type = "EventOccurrence",
            EventId = group.Representative.EventId,
            RecordId = group.Representative.RecordId,
            Provider = group.Representative.Provider,
            SourceLog = group.Representative.SourceLog,
            ContainerLog = group.Representative.ContainerLog,
            SourceKind = group.Representative.SourceKind,
            SourceComputer = group.Representative.SourceComputer,
            CollectorComputer = group.Representative.CollectorComputer,
            Level = group.Representative.Level,
            LevelValue = group.Representative.LevelValue,
            Message = group.Representative.Message,
            Values = new Dictionary<string, object?> {
                ["OccurrenceId"] = group.Identity,
                ["RepresentativeType"] = group.Representative.Type,
                ["ObservationCount"] = group.ObservationCount,
                ["SourceComputers"] = string.Join(", ", group.SourceComputers),
                ["RecordIds"] = string.Join(", ", group.Observations.Select(static row => row.RecordId?.ToString() ?? "(none)")),
                ["Policy"] = group.PolicyName + " v" + group.PolicyVersion,
                ["MatchReason"] = group.MatchReason
            }
        }).ToArray();
        return EventReportEngine.CreateStored(
            rows,
            new[] { schema },
            string.IsNullOrWhiteSpace(title) ? "EventViewerX occurrences" : title,
            coverage: sourceReport?.Coverage,
            generatedAt: sourceReport?.GeneratedAt,
            eventsScanned: sourceReport?.EventsScanned ??
                result.Groups.Sum(static group => (long)group.ObservationCount),
            scanLimitReached: !result.IsComplete || sourceReport?.ScanLimitReached == true);
    }

    /// <summary>
    /// Creates a report containing one original representative row per occurrence while retaining
    /// the source schemas and completeness envelope. This is the aggregation input contract.
    /// </summary>
    public static EventReport CreateRepresentatives(
        EventOccurrenceResult result,
        EventReport sourceReport,
        string? title = null) {

        if (result == null) {
            throw new ArgumentNullException(nameof(result));
        }
        if (sourceReport == null) {
            throw new ArgumentNullException(nameof(sourceReport));
        }
        EventReportSectionSchema[] schemas = sourceReport.Sections.Select(static section =>
            new EventReportSectionSchema {
                Name = section.Name,
                DisplayName = section.DisplayName,
                Description = section.Description,
                Kind = section.Kind,
                Columns = section.Columns.Select(static column => new EventReportColumnSchema {
                    Name = column.Name,
                    DisplayName = column.DisplayName,
                    ValueTypeName = EventReportColumnSchema.GetStableTypeName(column.ValueType),
                    Aliases = column.Aliases.ToArray()
                }).ToArray()
            }).ToArray();
        return EventReportEngine.CreateStored(
            result.Groups.Select(static group => group.Representative),
            schemas,
            string.IsNullOrWhiteSpace(title) ? sourceReport.Title : title,
            sourceReport.Coverage,
            sourceReport.GeneratedAt,
            sourceReport.EventsScanned,
            !result.IsComplete || sourceReport.ScanLimitReached);
    }

    private static EventReportColumnSchema Column(string name, string displayName, Type type) => new() {
        Name = name,
        DisplayName = displayName,
        ValueTypeName = EventReportColumnSchema.GetStableTypeName(type)
    };
}
