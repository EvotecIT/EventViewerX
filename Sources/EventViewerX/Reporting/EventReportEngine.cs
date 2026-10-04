using System.Globalization;

namespace EventViewerX.Reporting;

/// <summary>Runs optimized event queries for streaming rows and reusable report snapshots.</summary>
public static partial class EventReportEngine {
    /// <summary>Queries and materializes an event report.</summary>
    public static async Task<EventReport> QueryAsync(EventReportRequest request, CancellationToken cancellationToken = default) {
        var stopwatch = Stopwatch.StartNew();
        var projections = new List<EventReportProjection>();
        EventReportSummary summary = await RunQueryAsync(request, (projection, _) => {
            projections.Add(projection);
            return Task.CompletedTask;
        }, cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();
        string title = string.IsNullOrWhiteSpace(request.Title)
            ? request.Types != null && request.Types.Count > 0
                ? string.Join(", ", request.Types.Select(static type => EventTypeCatalog.GetDefinition(type).DisplayName))
                : request.Definition != null
                    ? string.IsNullOrWhiteSpace(request.Definition.DisplayName) ? request.Definition.Name : request.Definition.DisplayName
                : request.Paths != null && request.Paths.Count > 0
                    ? $"{request.Paths.Count} offline event log{(request.Paths.Count == 1 ? string.Empty : "s")}"
                    : $"{request.LogName} events"
            : request.Title!.Trim();
        EventReportRow[] rows = projections.Select(static projection => projection.Row).ToArray();
        IReadOnlyList<EventReportSectionDefinition>? emptyDefinitions = projections.Count == 0
            ? EventReportProjectionFactory.CreateDefinitions(request)
            : null;
        return new EventReport(title, DateTime.UtcNow, stopwatch.Elapsed, rows,
            EventReportSectionBuilder.Build(projections, emptyDefinitions), summary.Coverage,
            summary.EventsScanned, summary.ScanLimitReached, summary.CompletenessDiagnostic);
    }

    /// <summary>Creates a report snapshot from previously queried EventViewerX objects without reading logs again.</summary>
    public static EventReport Create(IEnumerable<object> input, string? title = null) =>
        Create(input, title, coverage: null, completenessDiagnostic: null);

    /// <summary>Creates a report snapshot with caller-supplied source coverage without reading logs again.</summary>
    public static EventReport Create(
        IEnumerable<object> input,
        string? title,
        IEnumerable<EventReportCoverage>? coverage,
        string? completenessDiagnostic = null) {

        if (input == null) {
            throw new ArgumentNullException(nameof(input));
        }
        var projections = new List<EventReportProjection>();
        foreach (object item in input) {
            projections.Add(CreateProjection(item));
        }
        EventReportRow[] rows = projections.Select(static projection => projection.Row).ToArray();
        EventReportCoverage[] coverageSnapshot = coverage?.Select(CloneCoverage).ToArray() ?? rows
            .GroupBy(static row => row.CollectorComputer + "\0" + row.SourceLog, StringComparer.OrdinalIgnoreCase)
            .Select(static group => {
                EventReportRow first = group.First();
                return new EventReportCoverage {
                    MachineName = first.CollectorComputer,
                    LogName = first.SourceLog,
                    Succeeded = true,
                    Status = "Supplied",
                    Detail = string.Empty
                };
            }).ToArray();
        return new EventReport(string.IsNullOrWhiteSpace(title) ? "EventViewerX events" : title!.Trim(),
            DateTime.UtcNow, TimeSpan.Zero, rows, EventReportSectionBuilder.Build(projections),
            coverageSnapshot, rows.Length, scanLimitReached: false,
            completenessDiagnostic);
    }

    /// <summary>Rehydrates persisted normalized rows without querying Windows Event Log again.</summary>
    public static EventReport CreateStored(
        IEnumerable<EventReportRow> rows,
        IEnumerable<EventReportSectionSchema> schemas,
        string? title = null,
        IEnumerable<EventReportCoverage>? coverage = null,
        DateTime? generatedAt = null,
        long? eventsScanned = null,
        bool scanLimitReached = false,
        string? completenessDiagnostic = null) {

        if (rows == null) {
            throw new ArgumentNullException(nameof(rows));
        }
        if (schemas == null) {
            throw new ArgumentNullException(nameof(schemas));
        }
        EventReportRow[] rowSnapshot = rows.Select(CloneRow).ToArray();
        EventReportSectionSchema[] schemaSnapshot = schemas.Select(CloneSchema).ToArray();
        if (schemaSnapshot.Any(static schema =>
                !Enum.IsDefined(typeof(EventReportSectionKind), schema.Kind))) {
            throw new ArgumentException(
                "Stored schemas contain an undefined EventReportSectionKind value.",
                nameof(schemas));
        }
        if (schemaSnapshot.Length == 0 && rowSnapshot.Length > 0) {
            throw new ArgumentException("At least one stored section schema is required when rows are present.", nameof(schemas));
        }
        string[] duplicateSchemas = schemaSnapshot
            .GroupBy(static schema => schema.Name, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();
        if (duplicateSchemas.Length > 0) {
            throw new ArgumentException(
                "Stored schemas contain duplicate case-insensitive names: " +
                string.Join(", ", duplicateSchemas) + ".",
                nameof(schemas));
        }
        if (schemaSnapshot.Any(static schema =>
                schema.Kind == EventReportSectionKind.Generic &&
                !string.Equals(schema.Name, "Generic", StringComparison.OrdinalIgnoreCase))) {
            throw new ArgumentException("The generic stored schema must use the stable name 'Generic'.", nameof(schemas));
        }
        foreach (EventReportRow row in rowSnapshot) {
            EventReportSectionSchema[] matchingSchemas = schemaSnapshot.Where(schema => string.Equals(
                schema.Kind == EventReportSectionKind.Generic ? "Generic" : schema.Name,
                row.Type,
                StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matchingSchemas.Length != 1) {
                throw new ArgumentException(
                    $"Stored row type '{row.Type}' must match exactly one homogeneous schema.",
                    nameof(rows));
            }
            NormalizeStoredRow(row, matchingSchemas[0]);
        }
        var sections = new List<EventReportSection>();
        foreach (EventReportSectionSchema schema in schemaSnapshot) {
            EventReportRow[] sectionRows = rowSnapshot
                .Where(row => string.Equals(
                    schema.Kind == EventReportSectionKind.Generic ? "Generic" : schema.Name,
                    row.Type,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            EventReportColumn[] columns = schema.Kind == EventReportSectionKind.Generic
                ? sectionRows.Length == 0
                    ? schema.Columns.Select(static column => new EventReportColumn(
                        column.Name,
                        column.DisplayName,
                        EventReportColumnSchema.ResolveValueTypeName(column.ValueTypeName),
                        column.Aliases)).ToArray()
                    : EventReportTableProjection.BuildGenericColumns(sectionRows).ToArray()
                : schema.Columns.Select(static column => new EventReportColumn(
                    column.Name,
                    column.DisplayName,
                    EventReportColumnSchema.ResolveValueTypeName(column.ValueTypeName),
                    column.Aliases)).ToArray();
            sections.Add(new EventReportSection(
                schema.Name,
                schema.DisplayName,
                schema.Description,
                schema.Kind,
                columns,
                sectionRows));
        }
        EventReportCoverage[] coverageSnapshot = coverage?.Select(static item => new EventReportCoverage {
            MachineName = item.MachineName,
            LogName = item.LogName,
            Succeeded = item.Succeeded,
            Status = item.Status,
            Detail = item.Detail
        }).ToArray() ?? Array.Empty<EventReportCoverage>();
        return new EventReport(
            string.IsNullOrWhiteSpace(title) ? "Stored EventViewerX events" : title!.Trim(),
            generatedAt ?? DateTime.UtcNow,
            TimeSpan.Zero,
            rowSnapshot,
            sections,
            coverageSnapshot,
            eventsScanned ?? rowSnapshot.LongLength,
            scanLimitReached,
            completenessDiagnostic);
    }

    /// <summary>Normalizes one generic, built-in typed, or custom event without querying the event log.</summary>
    public static EventReportRow CreateRow(object input) {
        return CreateProjection(input).Row;
    }

    private static EventReportCoverage CloneCoverage(EventReportCoverage item) {
        if (item == null) {
            throw new ArgumentException("Coverage cannot contain null values.", nameof(item));
        }
        return new EventReportCoverage {
            MachineName = item.MachineName,
            LogName = item.LogName,
            Succeeded = item.Succeeded,
            Status = item.Status,
            Detail = item.Detail
        };
    }

    private static EventReportProjection CreateProjection(object input) {
        return input switch {
            EventTypeRecord typed => EventReportProjectionFactory.Create(typed),
            EventObject source => EventReportProjectionFactory.Create(source),
            CustomEventRecord custom => EventReportProjectionFactory.Create(custom),
            GroupPolicyAuditRecord groupPolicy => EventReportProjectionFactory.Create(groupPolicy),
            EventDetectionFinding finding => EventReportProjectionFactory.Create(finding),
            EventTimelineEntry timeline => EventReportProjectionFactory.Create(timeline),
            EventDecisionMetric metric => EventReportProjectionFactory.Create(metric),
            null => throw new ArgumentNullException(nameof(input)),
            _ => throw new ArgumentException(
                $"Unsupported report input type '{input.GetType().FullName}'. Expected an EventViewerX event, finding, or timeline entry.",
                nameof(input))
        };
    }

    private static EventReportRow CloneRow(EventReportRow row) {
        if (row == null) {
            throw new ArgumentException("Stored rows cannot contain null values.", nameof(row));
        }
        return new EventReportRow {
            TimeCreated = row.TimeCreated,
            ObservationIdentity = row.ObservationIdentity,
            ReceivedTimeUtc = row.ReceivedTimeUtc,
            ProcessedTimeUtc = row.ProcessedTimeUtc,
            StoredTimeUtc = row.StoredTimeUtc,
            Type = row.Type,
            EventId = row.EventId,
            RecordId = row.RecordId,
            Provider = row.Provider,
            SourceLog = row.SourceLog,
            ContainerLog = row.ContainerLog,
            SourceKind = row.SourceKind,
            SourceComputer = row.SourceComputer,
            CollectorComputer = row.CollectorComputer,
            Level = row.Level,
            LevelValue = row.LevelValue,
            ActivityId = row.ActivityId,
            RelatedActivityId = row.RelatedActivityId,
            ProcessId = row.ProcessId,
            ThreadId = row.ThreadId,
            Message = row.Message,
            Values = (row.Values ?? throw new ArgumentException(
                "Stored rows must provide a values collection.", nameof(row))).ToDictionary(
                static item => item.Key,
                static item => item.Value,
                StringComparer.OrdinalIgnoreCase)
        };
    }

    /// <summary>Rehydrates one detached stored row using its persisted schema and canonical value normalization.</summary>
    public static void NormalizeStoredRow(EventReportRow row, EventReportSectionSchema schema) {
        if (row == null) {
            throw new ArgumentNullException(nameof(row));
        }
        if (schema == null || !string.Equals(row.Type, schema.Name, StringComparison.OrdinalIgnoreCase)) {
            throw new ArgumentException("Stored row type must match its schema.", nameof(schema));
        }
        NormalizeStoredValues(row, schema);
        EventValueNormalizationEngine.Populate(row);
    }

    private static void NormalizeStoredValues(EventReportRow row, EventReportSectionSchema schema) {
        if (schema.Kind == EventReportSectionKind.Generic) {
            return;
        }
        var columns = schema.Columns.ToDictionary(
            static column => column.Name,
            StringComparer.OrdinalIgnoreCase);
        var normalized = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, object?> value in row.Values) {
            if (!columns.TryGetValue(value.Key, out EventReportColumnSchema? column)) {
                throw new ArgumentException(
                    $"Stored row '{row.Type}' contains field '{value.Key}' that is not declared by its schema.",
                    nameof(row));
            }
            Type targetType = EventReportColumnSchema.ResolveValueTypeName(column.ValueTypeName);
            string normalizedTypeName = EventReportColumnSchema.NormalizeValueTypeName(column.ValueTypeName);
            if (targetType == typeof(object) &&
                !string.Equals(
                    normalizedTypeName,
                    EventReportColumnSchema.GetStableTypeName(typeof(object)),
                    StringComparison.Ordinal)) {
                throw new ArgumentException(
                    $"Stored schema field '{schema.Name}.{column.Name}' declares unknown type '{column.ValueTypeName}'.",
                    nameof(schema));
            }
            normalized[value.Key] = ConvertStoredValue(value.Value, targetType, schema.Name, column.Name);
        }
        row.Values = normalized;
    }

    private static object? ConvertStoredValue(object? value, Type targetType, string schemaName, string fieldName) {
        if (value == null || targetType == typeof(object)) {
            return value;
        }
        Type effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (effectiveType.IsInstanceOfType(value)) {
            return value;
        }
        string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        try {
            if (effectiveType == typeof(string)) {
                return text;
            }
            if (effectiveType == typeof(Guid)) {
                return Guid.Parse(text);
            }
            if (effectiveType == typeof(System.Net.IPAddress)) {
                return System.Net.IPAddress.Parse(text);
            }
            if (effectiveType == typeof(DateTime)) {
                return DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            }
            if (effectiveType == typeof(DateTimeOffset)) {
                return DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            }
            if (effectiveType.IsEnum) {
                return Enum.Parse(effectiveType, text, ignoreCase: false);
            }
            return Convert.ChangeType(value, effectiveType, CultureInfo.InvariantCulture);
        } catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException or ArgumentException) {
            throw new ArgumentException(
                $"Stored field '{schemaName}.{fieldName}' value '{text}' cannot be converted to '{effectiveType.FullName}'.",
                nameof(value),
                exception);
        }
    }

    private static EventReportSectionSchema CloneSchema(EventReportSectionSchema schema) {
        if (schema == null || string.IsNullOrWhiteSpace(schema.Name)) {
            throw new ArgumentException("Stored schemas must have a non-empty name.", nameof(schema));
        }
        IReadOnlyList<EventReportColumnSchema> columns = schema.Columns ??
            throw new ArgumentException($"Stored schema '{schema.Name}' must declare Columns.", nameof(schema));
        if (columns.Any(static column => column == null || string.IsNullOrWhiteSpace(column.Name))) {
            throw new ArgumentException($"Stored schema '{schema.Name}' contains an invalid column.", nameof(schema));
        }
        string[] duplicateColumns = columns
            .GroupBy(static column => column.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();
        if (duplicateColumns.Length > 0) {
            throw new ArgumentException(
                $"Stored schema '{schema.Name}' contains duplicate case-insensitive columns: " +
                string.Join(", ", duplicateColumns) + ".",
                nameof(schema));
        }
        var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (EventReportColumnSchema column in columns) {
            identities.Add(column.Name.Trim());
        }
        foreach (EventReportColumnSchema column in columns) {
            foreach (string alias in column.Aliases ?? Array.Empty<string>()) {
                if (string.IsNullOrWhiteSpace(alias)) {
                    throw new ArgumentException(
                        $"Stored schema '{schema.Name}' column '{column.Name}' contains an empty alias.",
                        nameof(schema));
                }
                string normalizedAlias = alias.Trim();
                if (!identities.Add(normalizedAlias)) {
                    throw new ArgumentException(
                        $"Stored schema '{schema.Name}' contains duplicate case-insensitive field or alias identity '{normalizedAlias}'.",
                        nameof(schema));
                }
            }
        }
        return new EventReportSectionSchema {
            Name = schema.Name.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(schema.DisplayName) ? schema.Name.Trim() : schema.DisplayName.Trim(),
            Description = schema.Description?.Trim() ?? string.Empty,
            Kind = schema.Kind,
            Columns = columns.Select(static column => new EventReportColumnSchema {
                Name = column.Name.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(column.DisplayName) ? column.Name.Trim() : column.DisplayName.Trim(),
                ValueTypeName = string.IsNullOrWhiteSpace(column.ValueTypeName)
                    ? EventReportColumnSchema.GetStableTypeName(typeof(object))
                    : column.ValueTypeName,
                Aliases = (column.Aliases ?? Array.Empty<string>())
                    .Where(static alias => !string.IsNullOrWhiteSpace(alias))
                    .Select(static alias => alias.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            }).ToArray()
        };
    }

}
