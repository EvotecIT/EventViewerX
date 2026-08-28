using System.Globalization;

namespace EventViewerX.Reporting;

/// <summary>Runs one optimized query and produces a reusable report snapshot.</summary>
public static class EventReportEngine {
    /// <summary>Queries and materializes an event report.</summary>
    public static async Task<EventReport> QueryAsync(EventReportRequest request, CancellationToken cancellationToken = default) {
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }
        request.Validate();
        var stopwatch = Stopwatch.StartNew();
        List<EventReportProjection> projections;
        List<EventReportCoverage> coverage;
        long scanned = 0;
        bool scanLimitReached = false;
        bool resultLimitReached = false;

        if (request.Types != null && request.Types.Count > 0) {
            var info = new EventTypeQueryExecutionInfo();
            EventTypeQuery query = CreateTypedQuery(request);
            projections = new List<EventReportProjection>();
            await foreach (EventTypeRecord record in EventTypeEngine.ReadAsync(query, info, cancellationToken)) {
                projections.Add(EventReportProjectionFactory.Create(record));
            }
            scanned = info.EventsScanned;
            scanLimitReached = info.ScanLimitReached;
            resultLimitReached = info.ResultLimitReached;
            coverage = BuildTypedCoverage(request, info);
        } else if (request.Definition != null) {
            projections = new List<EventReportProjection>();
            var info = new EventDefinitionQueryExecutionInfo();
            var query = new EventDefinitionQuery(request.Definition) {
                Paths = request.Paths,
                SavedEventReader = request.SavedEventReader,
                SavedEventDiagnosticHandler = request.SavedEventDiagnosticHandler,
                MachineNames = request.Collectors != null && request.Collectors.Count > 0 ? request.Collectors : request.MachineNames,
                CollectorLogName = request.Collectors != null && request.Collectors.Count > 0 ? request.CollectorLogName : null,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                TimePeriod = request.TimePeriod,
                RecordIds = request.RecordIds,
                MaxEvents = request.MaxEvents,
                MaxCandidates = request.MaxCandidates,
                MaxConcurrency = request.MaxConcurrency,
                Oldest = request.Oldest,
                Credential = request.Credential,
                Authentication = request.Authentication,
                ContinueOnRemoteFailure = request.ContinueOnRemoteFailure,
                Predicate = request.Predicate?.Clone(),
                MinimumRecordIdExclusiveResolver =
                    request.MinimumRecordIdExclusiveResolver,
                BookmarkXmlResolver = request.BookmarkXmlResolver
            };
            await foreach (CustomEventRecord record in EventDefinitionEngine.ReadAsync(query, info, cancellationToken)) {
                projections.Add(EventReportProjectionFactory.Create(record));
            }
            scanned = info.EventsScanned;
            scanLimitReached = info.ScanLimitReached;
            resultLimitReached = info.ResultLimitReached;
            coverage = BuildCustomCoverage(request, info);
        } else {
            (projections, coverage, resultLimitReached) = await QueryGenericAsync(request, cancellationToken);
            scanned = projections.Count + (resultLimitReached ? 1L : 0L);
        }
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
            EventReportSectionBuilder.Build(projections, emptyDefinitions),
            coverage,
            scanned,
            scanLimitReached || resultLimitReached,
            CreateResultLimitDiagnostic(request.MaxEvents, scanLimitReached, resultLimitReached));
    }

    /// <summary>Creates a report snapshot from previously queried EventViewerX objects without reading logs again.</summary>
    public static EventReport Create(IEnumerable<object> input, string? title = null) {
        if (input == null) {
            throw new ArgumentNullException(nameof(input));
        }
        var projections = new List<EventReportProjection>();
        foreach (object item in input) {
            projections.Add(CreateProjection(item));
        }
        EventReportRow[] rows = projections.Select(static projection => projection.Row).ToArray();
        List<EventReportCoverage> coverage = rows
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
            }).ToList();
        return new EventReport(string.IsNullOrWhiteSpace(title) ? "EventViewerX events" : title!.Trim(),
            DateTime.UtcNow, TimeSpan.Zero, rows, EventReportSectionBuilder.Build(projections),
            coverage, rows.Length, scanLimitReached: false);
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
            NormalizeStoredValues(row, matchingSchemas[0]);
            EventValueNormalizationEngine.Populate(row);
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

    private static EventReportProjection CreateProjection(object input) {
        return input switch {
            EventTypeRecord typed => EventReportProjectionFactory.Create(typed),
            EventObject source => EventReportProjectionFactory.Create(source),
            CustomEventRecord custom => EventReportProjectionFactory.Create(custom),
            GroupPolicyAuditRecord groupPolicy => EventReportProjectionFactory.Create(groupPolicy),
            EventDetectionFinding finding => EventReportProjectionFactory.Create(finding),
            EventTimelineEntry timeline => EventReportProjectionFactory.Create(timeline),
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
            Message = row.Message,
            Values = (row.Values ?? throw new ArgumentException(
                "Stored rows must provide a values collection.", nameof(row))).ToDictionary(
                static item => item.Key,
                static item => item.Value,
                StringComparer.OrdinalIgnoreCase)
        };
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

    private static EventTypeQuery CreateTypedQuery(EventReportRequest request) {
        bool collectors = request.Collectors != null && request.Collectors.Count > 0;
        return new EventTypeQuery(request.Types!) {
            Paths = request.Paths,
            SavedEventReader = request.SavedEventReader,
            SavedEventDiagnosticHandler = request.SavedEventDiagnosticHandler,
            MachineNames = collectors ? request.Collectors : request.MachineNames,
            CollectorLogName = collectors ? request.CollectorLogName : null,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            TimePeriod = request.TimePeriod,
            SourceRecordIds = request.RecordIds,
            MaxEvents = request.MaxEvents,
            MaxCandidates = request.MaxCandidates,
            MaxConcurrency = request.MaxConcurrency,
            Oldest = request.Oldest,
            ReadMode = EventReadMode.StructuredDataAndMessage,
            Credential = request.Credential,
            Authentication = request.Authentication,
            ContinueOnRemoteFailure = request.ContinueOnRemoteFailure,
            Enrichment = request.ResolveDns ? new EventEnrichmentOptions { ResolveDns = true } : null,
            Predicate = request.Predicate?.Clone(),
            MinimumRecordIdExclusiveResolver =
                request.MinimumRecordIdExclusiveResolver,
            BookmarkXmlResolver = request.BookmarkXmlResolver
        };
    }

    private static async Task<(List<EventReportProjection> Rows, List<EventReportCoverage> Coverage, bool ResultLimitReached)> QueryGenericAsync(
        EventReportRequest request, CancellationToken cancellationToken) {
        (DateTime? startTime, DateTime? endTime) = EventTimeRange.Resolve(request.StartTime, request.EndTime, request.TimePeriod);
        EventFilter filter = new() {
            EventIds = request.EventIds?.ToArray(),
            RecordIds = request.RecordIds?.ToArray(),
            StartTime = startTime,
            EndTime = endTime
        };
        if (request.Paths != null && request.Paths.Count > 0) {
            EventLogFileQuery[] files = request.Paths.Select(path => {
                string fullPath = Path.GetFullPath(path);
                EventFilter pathFilter = filter.WithMinimumRecordIdExclusive(
                    request.MinimumRecordIdExclusiveResolver?.Invoke(
                        fullPath,
                        fullPath));
                return new EventLogFileQuery(fullPath) {
                    XPath = EventFilterCompiler.BuildXPath(pathFilter),
                    SavedEventReader = request.SavedEventReader,
                    SavedEventDiagnosticHandler = request.SavedEventDiagnosticHandler,
                    Oldest = request.Oldest,
                    ReadMode = EventReadMode.StructuredDataAndMessage,
                    BookmarkXml = request.BookmarkXmlResolver?.Invoke(
                        fullPath,
                        fullPath)
                };
            }).ToArray();
            EventLogBatchQuery fileBatch = EventLogBatchQuery.ForFiles(files);
            fileBatch.MaxEvents = GetProbeLimit(request.MaxEvents);
            fileBatch.MaxConcurrency = request.MaxConcurrency;
            var fileRows = new List<EventReportProjection>();
            await foreach (EventObject record in EventLogEngine.ReadBatchAsync(fileBatch, cancellationToken)) {
                fileRows.Add(EventReportProjectionFactory.Create(record));
            }
            List<EventReportCoverage> fileCoverage = files.Select(static file => new EventReportCoverage {
                MachineName = "Offline",
                LogName = file.Path,
                Succeeded = true,
                Status = "Succeeded",
                Detail = string.Empty
            }).ToList();
            bool fileResultLimitReached = request.MaxEvents > 0 && fileRows.Count > request.MaxEvents;
            return (TrimToResultLimit(fileRows, request.MaxEvents), fileCoverage, fileResultLimitReached);
        }
        string?[] targets = request.MachineNames == null || request.MachineNames.Count == 0
            ? new string?[] { null }
            : request.MachineNames.ToArray();
        var failures = new List<EventLogQueryFailure>();
        EventLogChannelQuery[] channels = targets.Select(target => {
            EventFilter targetFilter = filter.WithMinimumRecordIdExclusive(
                request.MinimumRecordIdExclusiveResolver?.Invoke(
                    target,
                    request.LogName!));
            return new EventLogChannelQuery(request.LogName!) {
                MachineName = target,
                Credential = string.IsNullOrWhiteSpace(target) ? null : request.Credential,
                Authentication = request.Authentication,
                XPath = EventFilterCompiler.BuildXPath(targetFilter),
                Oldest = request.Oldest,
                ReadMode = EventReadMode.StructuredDataAndMessage,
                BookmarkXml = request.BookmarkXmlResolver?.Invoke(
                    target,
                    request.LogName!)
            };
        }).ToArray();
        EventLogBatchQuery batch = EventLogBatchQuery.ForChannels(channels);
        batch.MaxEvents = GetProbeLimit(request.MaxEvents);
        batch.MaxConcurrency = request.MaxConcurrency;
        batch.ContinueOnError = request.ContinueOnRemoteFailure;
        batch.FailureHandler = failure => {
            if (EventLogRemoteQueryFailureClassifier.TryClassify(failure.MachineName, failure.Exception,
                    out EventLogRemoteQueryFailureKind kind)) {
                failures.Add(failure);
                return;
            }
            throw failure.Exception;
        };
        var rows = new List<EventReportProjection>();
        await foreach (EventObject record in EventLogEngine.ReadBatchAsync(batch, cancellationToken)) {
            rows.Add(EventReportProjectionFactory.Create(record));
        }
        var coverage = targets.Select(target => {
            string machine = string.IsNullOrWhiteSpace(target) ? Environment.MachineName : target!;
            EventLogQueryFailure? failure = failures.FirstOrDefault(item =>
                string.Equals(item.MachineName, machine, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Source, request.LogName, StringComparison.OrdinalIgnoreCase));
            EventLogRemoteQueryFailureKind failureKind = EventLogRemoteQueryFailureKind.None;
            if (failure != null) {
                EventLogRemoteQueryFailureClassifier.TryClassify(
                    failure.MachineName, failure.Exception, out failureKind);
            }
            return new EventReportCoverage {
                MachineName = machine,
                LogName = request.LogName!,
                Succeeded = failure == null,
                Status = failure == null ? "Succeeded" : failureKind.ToString(),
                Detail = failure?.Exception.Message ?? string.Empty
            };
        }).ToList();
        bool channelResultLimitReached = request.MaxEvents > 0 && rows.Count > request.MaxEvents;
        return (TrimToResultLimit(rows, request.MaxEvents), coverage, channelResultLimitReached);
    }

    private static long GetProbeLimit(long maximum) => maximum > 0 && maximum < long.MaxValue
        ? maximum + 1
        : maximum;

    private static List<EventReportProjection> TrimToResultLimit(
        List<EventReportProjection> rows,
        long maximum) {

        if (maximum > 0 && rows.Count > maximum) {
            rows.RemoveRange(checked((int)maximum), rows.Count - checked((int)maximum));
        }
        return rows;
    }

    private static string? CreateResultLimitDiagnostic(
        long maximum,
        bool scanLimitReached,
        bool resultLimitReached) => EventCompletenessDiagnostic.Compose(
            scanLimitReached ? "The source candidate scan limit was reached" : null,
            resultLimitReached
                ? $"The result limit MaxEvents {maximum:N0} was reached; additional matching events exist"
                : null);

    private static List<EventReportCoverage> BuildCustomCoverage(
        EventReportRequest request,
        EventDefinitionQueryExecutionInfo info) {
        if (request.Paths != null && request.Paths.Count > 0) {
            return request.Paths.Select(static path => new EventReportCoverage {
                MachineName = "Offline",
                LogName = Path.GetFullPath(path),
                Succeeded = true,
                Status = "Succeeded",
                Detail = string.Empty
            }).ToList();
        }
        IReadOnlyList<string?> targets = request.Collectors ?? request.MachineNames ?? new string?[] { null };
        return (from target in targets
                from source in request.Definition!.Sources
                let machine = string.IsNullOrWhiteSpace(target) ? Environment.MachineName : target!
                let queriedLog = request.Collectors != null ? request.CollectorLogName : source.LogName
                let failure = info.TargetFailures.FirstOrDefault(item =>
                    string.Equals(item.MachineName, machine, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(item.LogName, queriedLog, StringComparison.OrdinalIgnoreCase))
                select new EventReportCoverage {
                    MachineName = machine,
                    LogName = source.LogName,
                    Succeeded = failure == null,
                    Status = failure?.Kind.ToString() ?? "Succeeded",
                    Detail = failure?.Message ?? string.Empty
                }).ToList();
    }

    private static List<EventReportCoverage> BuildTypedCoverage(EventReportRequest request, EventTypeQueryExecutionInfo info) {
        if (request.Paths != null && request.Paths.Count > 0) {
            return request.Paths.Select(static path => new EventReportCoverage {
                MachineName = "Offline",
                LogName = Path.GetFullPath(path),
                Succeeded = true,
                Status = "Succeeded",
                Detail = string.Empty
            }).ToList();
        }
        IReadOnlyList<string?> targets = request.Collectors ?? request.MachineNames ?? new string?[] { null };
        IReadOnlyList<EventSourceDefinition> sources = EventTypeCatalog.GetSources(request.Types!);
        var failures = info.TargetFailures;
        var result = new List<EventReportCoverage>();
        foreach (string? target in targets) {
            string machine = string.IsNullOrWhiteSpace(target) ? Environment.MachineName : target!;
            foreach (EventSourceDefinition source in sources) {
                EventLogQueryTargetFailure? failure = failures.FirstOrDefault(item =>
                    string.Equals(item.MachineName, machine, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(item.LogName, request.Collectors != null ? request.CollectorLogName : source.LogName, StringComparison.OrdinalIgnoreCase));
                result.Add(new EventReportCoverage {
                    MachineName = machine,
                    LogName = source.LogName,
                    Succeeded = failure == null,
                    Status = failure?.Kind.ToString() ?? "Succeeded",
                    Detail = failure?.Message ?? string.Empty
                });
            }
        }
        return result;
    }
}
