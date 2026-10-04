namespace EventViewerX.Reporting;

public static partial class EventReportEngine {
    /// <summary>
    /// Delivers normalized rows in source query order without retaining the result set.
    /// Each callback is awaited before delivering another row. The section supplies the row's field contract;
    /// its Rows collection is empty and does not accumulate streamed rows. Source cursors and existing bounded
    /// reader buffers remain owned by the query engine. Completion evidence is returned only after successful
    /// exhaustion or a declared limit; cancellation and callback failures propagate without a success summary.
    /// </summary>
    public static Task<EventReportSummary> StreamRowsAsync(
        EventReportRequest request,
        Func<EventReportRow, EventReportSection, CancellationToken, Task> onRow,
        CancellationToken cancellationToken = default) {

        if (onRow == null) {
            throw new ArgumentNullException(nameof(onRow));
        }
        var sections = new Dictionary<string, EventReportSection>(StringComparer.Ordinal);
        return RunQueryAsync(request, (projection, token) => {
            EventReportSectionDefinition definition = projection.Section;
            if (!sections.TryGetValue(definition.Key, out EventReportSection? section)) {
                section = new EventReportSection(definition.Name, definition.DisplayName, definition.Description,
                    definition.Kind, definition.Columns, Array.Empty<EventReportRow>());
                sections.Add(definition.Key, section);
            }
            return onRow(projection.Row, section, token);
        }, cancellationToken);
    }

    private static async Task<EventReportSummary> RunQueryAsync(
        EventReportRequest request,
        Func<EventReportProjection, CancellationToken, Task> onProjection,
        CancellationToken cancellationToken) {
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }
        request.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        long emitted = 0;
        List<EventReportCoverage> coverage;
        long scanned = 0;
        bool scanLimitReached = false;
        bool resultLimitReached = false;
        var savedEventDiagnostics = new SavedEventCompletenessDiagnostics();
        Action<SavedEventReadDiagnostic> savedEventDiagnosticHandler = diagnostic => {
            if (diagnostic.AffectsCompleteness) {
                savedEventDiagnostics.Add(diagnostic);
            }
            request.SavedEventDiagnosticHandler?.Invoke(diagnostic);
        };

        if (request.Types != null && request.Types.Count > 0) {
            var info = new EventTypeQueryExecutionInfo();
            EventTypeQuery query = CreateTypedQuery(request, savedEventDiagnosticHandler);
            await foreach (EventTypeRecord record in EventTypeEngine.ReadAsync(query, info, cancellationToken)) {
                cancellationToken.ThrowIfCancellationRequested();
                await onProjection(EventReportProjectionFactory.Create(record), cancellationToken).ConfigureAwait(false);
                emitted++;
            }
            scanned = info.EventsScanned;
            scanLimitReached = info.ScanLimitReached;
            resultLimitReached = info.ResultLimitReached;
            coverage = BuildTypedCoverage(request, info);
        } else if (request.Definition != null) {
            var info = new EventDefinitionQueryExecutionInfo();
            var query = new EventDefinitionQuery(request.Definition) {
                Paths = request.Paths,
                SavedEventReader = request.SavedEventReader,
                SavedEventDiagnosticHandler = savedEventDiagnosticHandler,
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
                ReadMode = request.ReadMode,
                Credential = request.Credential,
                Authentication = request.Authentication,
                ContinueOnRemoteFailure = request.ContinueOnRemoteFailure,
                Predicate = request.Predicate?.Clone(),
                MinimumRecordIdExclusiveResolver =
                    request.MinimumRecordIdExclusiveResolver,
                BookmarkXmlResolver = request.BookmarkXmlResolver
            };
            await foreach (CustomEventRecord record in EventDefinitionEngine.ReadAsync(query, info, cancellationToken)) {
                cancellationToken.ThrowIfCancellationRequested();
                await onProjection(EventReportProjectionFactory.Create(record), cancellationToken).ConfigureAwait(false);
                emitted++;
            }
            scanned = info.EventsScanned;
            scanLimitReached = info.ScanLimitReached;
            resultLimitReached = info.ResultLimitReached;
            coverage = BuildCustomCoverage(request, info);
        } else {
            (scanned, coverage, resultLimitReached) = await QueryGenericAsync(
                request,
                savedEventDiagnosticHandler,
                async (projection, token) => {
                    await onProjection(projection, token).ConfigureAwait(false);
                    emitted++;
                },
                cancellationToken).ConfigureAwait(false);
        }
        cancellationToken.ThrowIfCancellationRequested();
        string? savedEventDiagnostic = savedEventDiagnostics.Describe();
        ApplySavedEventCoverage(coverage, savedEventDiagnostic);
        return new EventReportSummary(emitted, scanned,
            scanLimitReached || resultLimitReached || savedEventDiagnostic != null,
            CreateCompletenessDiagnostic(request.MaxEvents, scanLimitReached, resultLimitReached, savedEventDiagnostic),
            coverage);
    }

    private static EventTypeQuery CreateTypedQuery(
        EventReportRequest request,
        Action<SavedEventReadDiagnostic> savedEventDiagnosticHandler) {

        bool collectors = request.Collectors != null && request.Collectors.Count > 0;
        return new EventTypeQuery(request.Types!) {
            Paths = request.Paths,
            SavedEventReader = request.SavedEventReader,
            SavedEventDiagnosticHandler = savedEventDiagnosticHandler,
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
            ReadMode = request.ReadMode,
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

    private static async Task<(long Scanned, List<EventReportCoverage> Coverage, bool ResultLimitReached)> QueryGenericAsync(
        EventReportRequest request,
        Action<SavedEventReadDiagnostic> savedEventDiagnosticHandler,
        Func<EventReportProjection, CancellationToken, Task> onProjection,
        CancellationToken cancellationToken) {
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
                    SavedEventDiagnosticHandler = savedEventDiagnosticHandler,
                    Oldest = request.Oldest,
                    ReadMode = request.ReadMode,
                    BookmarkXml = request.BookmarkXmlResolver?.Invoke(
                        fullPath,
                        fullPath)
                };
            }).ToArray();
            EventLogBatchQuery fileBatch = EventLogBatchQuery.ForFiles(files);
            fileBatch.MaxEvents = GetProbeLimit(request.MaxEvents);
            fileBatch.MaxConcurrency = request.MaxConcurrency;
            long fileScanned = 0;
            await foreach (EventObject record in EventLogEngine.ReadBatchAsync(fileBatch, cancellationToken)) {
                cancellationToken.ThrowIfCancellationRequested();
                fileScanned++;
                if (request.MaxEvents == 0 || fileScanned <= request.MaxEvents) {
                    await onProjection(EventReportProjectionFactory.Create(record), cancellationToken).ConfigureAwait(false);
                }
            }
            List<EventReportCoverage> fileCoverage = files.Select(static file => new EventReportCoverage {
                MachineName = "Offline",
                LogName = file.Path,
                Succeeded = true,
                Status = "Succeeded",
                Detail = string.Empty
            }).ToList();
            bool fileResultLimitReached = request.MaxEvents > 0 && fileScanned > request.MaxEvents;
            return (fileScanned, fileCoverage, fileResultLimitReached);
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
                ReadMode = request.ReadMode,
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
        long scanned = 0;
        await foreach (EventObject record in EventLogEngine.ReadBatchAsync(batch, cancellationToken)) {
            cancellationToken.ThrowIfCancellationRequested();
            scanned++;
            if (request.MaxEvents == 0 || scanned <= request.MaxEvents) {
                await onProjection(EventReportProjectionFactory.Create(record), cancellationToken).ConfigureAwait(false);
            }
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
        bool channelResultLimitReached = request.MaxEvents > 0 && scanned > request.MaxEvents;
        return (scanned, coverage, channelResultLimitReached);
    }

    private static long GetProbeLimit(long maximum) => maximum > 0 && maximum < long.MaxValue
        ? maximum + 1
        : maximum;

    private static string? CreateCompletenessDiagnostic(
        long maximum,
        bool scanLimitReached,
        bool resultLimitReached,
        string? savedEventDiagnostic) => EventCompletenessDiagnostic.Compose(
            scanLimitReached ? "The source candidate scan limit was reached" : null,
            resultLimitReached
                ? $"The result limit MaxEvents {maximum:N0} was reached; additional matching events exist"
                : null,
            savedEventDiagnostic);

    private static void ApplySavedEventCoverage(
        IEnumerable<EventReportCoverage> coverage,
        string? savedEventDiagnostic) {

        if (savedEventDiagnostic == null) {
            return;
        }
        foreach (EventReportCoverage source in coverage.Where(static source =>
                     string.Equals(source.MachineName, "Offline", StringComparison.OrdinalIgnoreCase))) {
            source.Succeeded = false;
            source.Status = "Incomplete";
            source.Detail = EventCompletenessDiagnostic.Compose(source.Detail, savedEventDiagnostic) ?? string.Empty;
        }
    }

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
