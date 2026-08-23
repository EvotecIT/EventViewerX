namespace EventViewerX.Reporting;

/// <summary>Adapts non-destructive occurrence groups to the normal report/export contract.</summary>
public static class EventOccurrenceReportFactory {
    /// <summary>Creates a report with one row per logical occurrence while the source result retains all observations.</summary>
    public static EventReport Create(EventOccurrenceResult result, string? title = null) {
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
            eventsScanned: result.Groups.Sum(static group => (long)group.ObservationCount),
            scanLimitReached: !result.IsComplete);
    }

    private static EventReportColumnSchema Column(string name, string displayName, Type type) => new() {
        Name = name,
        DisplayName = displayName,
        ValueTypeName = EventReportColumnSchema.GetStableTypeName(type)
    };
}
