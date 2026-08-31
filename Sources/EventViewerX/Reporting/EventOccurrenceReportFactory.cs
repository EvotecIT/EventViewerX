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
        bool sourceComplete = sourceReport == null ||
            !sourceReport.ScanLimitReached &&
            sourceReport.Coverage.All(static coverage => coverage.Succeeded);
        bool isComplete = result.IsComplete && sourceComplete;
        string? diagnostic = EventCompletenessDiagnostic.Compose(
            result.Diagnostic,
            sourceReport?.CompletenessDiagnostic,
            sourceComplete
                ? null
                : "The source query was incomplete; occurrence output cannot be treated as exhaustive.");
        var schema = new EventReportSectionSchema {
            Name = "EventOccurrence",
            DisplayName = "Event occurrences",
            Description = result.Diagnostic ?? "Non-destructive logical occurrence groups.",
            Kind = EventReportSectionKind.Custom,
            Columns = new[] {
                Column("ResultKind", "Result kind", typeof(string)),
                Column("IsComplete", "Complete", typeof(bool)),
                Column("Diagnostic", "Diagnostic", typeof(string)),
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
            ActivityId = group.Representative.ActivityId,
            RelatedActivityId = group.Representative.RelatedActivityId,
            ProcessId = group.Representative.ProcessId,
            ThreadId = group.Representative.ThreadId,
            SourceKind = group.Representative.SourceKind,
            SourceComputer = group.Representative.SourceComputer,
            CollectorComputer = group.Representative.CollectorComputer,
            Level = group.Representative.Level,
            LevelValue = group.Representative.LevelValue,
            Message = group.Representative.Message,
            Values = new Dictionary<string, object?> {
                ["ResultKind"] = "Occurrence",
                ["IsComplete"] = isComplete,
                ["Diagnostic"] = diagnostic,
                ["OccurrenceId"] = group.Identity,
                ["RepresentativeType"] = group.Representative.Type,
                ["ObservationCount"] = group.ObservationCount,
                ["SourceComputers"] = string.Join(", ", group.SourceComputers),
                ["RecordIds"] = string.Join(", ", group.Observations.Select(static row => row.RecordId?.ToString() ?? "(none)")),
                ["Policy"] = group.PolicyName + " v" + group.PolicyVersion,
                ["MatchReason"] = group.MatchReason
            }
        }).ToArray();
        if (rows.Length == 0 && !isComplete) {
            rows = new[] {
                new EventReportRow {
                    Type = "EventOccurrence",
                    Values = new Dictionary<string, object?> {
                        ["ResultKind"] = "ResultMetadata",
                        ["IsComplete"] = false,
                        ["Diagnostic"] = diagnostic
                    }
                }
            };
        }
        return EventReportEngine.CreateStored(
            rows,
            new[] { schema },
            string.IsNullOrWhiteSpace(title) ? "EventViewerX occurrences" : title,
            coverage: sourceReport?.Coverage,
            generatedAt: sourceReport?.GeneratedAt,
            eventsScanned: sourceReport?.EventsScanned ?? result.ObservationsEvaluated,
            scanLimitReached: !isComplete,
            completenessDiagnostic: diagnostic);
    }

    /// <summary>
    /// Applies a source report's coverage and query-limit evidence to an occurrence result returned
    /// to programmatic callers.
    /// </summary>
    public static EventOccurrenceResult ComposeSourceCompleteness(
        EventOccurrenceResult result,
        EventReport sourceReport) {

        if (result == null) {
            throw new ArgumentNullException(nameof(result));
        }
        if (sourceReport == null) {
            throw new ArgumentNullException(nameof(sourceReport));
        }
        bool sourceComplete = !sourceReport.ScanLimitReached &&
            sourceReport.Coverage.All(static coverage => coverage.Succeeded);
        if (sourceComplete) {
            return result;
        }
        string? diagnostic = EventCompletenessDiagnostic.Compose(
            result.Diagnostic,
            sourceReport.CompletenessDiagnostic,
            "The source query was incomplete; occurrence output cannot be treated as exhaustive.");
        return new EventOccurrenceResult(
            result.Groups,
            isComplete: false,
            diagnostic,
            result.ObservationsEvaluated);
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
        bool sourceComplete = !sourceReport.ScanLimitReached &&
            sourceReport.Coverage.All(static coverage => coverage.Succeeded);
        string? diagnostic = EventCompletenessDiagnostic.Compose(
            result.Diagnostic,
            sourceReport.CompletenessDiagnostic,
            sourceComplete
                ? null
                : "The source query was incomplete; occurrence output cannot be treated as exhaustive.");
        return EventReportEngine.CreateStored(
            result.Groups.Select(static group => group.Representative),
            schemas,
            string.IsNullOrWhiteSpace(title) ? sourceReport.Title : title,
            sourceReport.Coverage,
            sourceReport.GeneratedAt,
            sourceReport.EventsScanned,
            !result.IsComplete || !sourceComplete,
            diagnostic);
    }

    private static EventReportColumnSchema Column(string name, string displayName, Type type) => new() {
        Name = name,
        DisplayName = displayName,
        ValueTypeName = EventReportColumnSchema.GetStableTypeName(type)
    };
}
