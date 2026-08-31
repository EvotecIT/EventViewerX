using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using EventViewerX.Reporting;
using EventViewerX.Evtx;
using EventViewerX.Sigma;
using EventViewerX.Storage;

namespace EventViewerX.Cli;

internal static partial class Program {
    private static async Task<int> DetectAsync(CliArguments options) {
        if (options.Has("portable-evtx") && options.Get("portable-evtx-executable") != null) {
            throw new ArgumentException(
                "--portable-evtx and --portable-evtx-executable are mutually exclusive. Select one portable EVTX engine.");
        }
        EventDetectionTuning? tuning = options.Get("tuning") is string tuningPath
            ? JsonSerializer.Deserialize<EventDetectionTuning>(
                await File.ReadAllTextAsync(Path.GetFullPath(tuningPath)).ConfigureAwait(false),
                JsonOptions) ?? throw new InvalidDataException($"Detection tuning '{tuningPath}' is empty.")
            : null;
        var rules = new List<IEventDetectionRule>();
        var packs = new List<EventDetectionPack>();
        string[] packPaths = options.GetMany("pack");
        string[] sigmaPaths = options.GetMany("sigma");
        bool explicitContent = packPaths.Length > 0 || sigmaPaths.Length > 0;
        if (!explicitContent || options.Has("include-built-in")) {
            EventDetectionPack[] builtIn = EventDetectionCatalog.GetBuiltInPacks().ToArray();
            packs.AddRange(builtIn);
            rules.AddRange(builtIn.SelectMany(static pack => pack.GetRules()));
        }
        foreach (string packPath in packPaths) {
            EventDetectionPack pack = EventDetectionPack.Load(packPath);
            EventDetectionPackValidationResult validation = pack.Validate();
            if (!validation.IsValid) {
                throw new InvalidDataException(
                    $"Detection pack '{packPath}' failed validation: {string.Join(" ", validation.Diagnostics)}");
            }
            packs.Add(pack);
            rules.AddRange(pack.GetRules());
        }
        if (sigmaPaths.Length > 0) {
            SigmaCompilationResult result = SigmaRuleCompiler.Load(sigmaPaths);
            foreach (SigmaDiagnostic diagnostic in result.Diagnostics) {
                Console.Error.WriteLine($"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message}");
            }
            if (!result.IsSupported) {
                throw new InvalidDataException("The Sigma input set contains unsupported or invalid behavior.");
            }
            rules.AddRange(result.Rules);
        }
        EventDetectionPlan plan = EventDetectionPlan.Compile(rules, tuning);
        if (options.Has("test-fixtures")) {
            if (explicitContent || tuning != null) {
                throw new ArgumentException("--test-fixtures validates the immutable built-in content and cannot be combined with --pack, --sigma, or --tuning.");
            }
            IReadOnlyList<EventDetectionFixtureResult> fixtureResults = EventDetectionCatalog.TestBuiltInFixtures();
            bool fixturesValid = fixtureResults.All(static result => result.IsMatch);
            WriteJson(new {
                IsValid = fixturesValid,
                FixtureCount = fixtureResults.Count,
                RuleCount = EventDetectionCatalog.GetBuiltInRules().Count,
                Results = fixtureResults.Select(static result => new {
                    result.Name,
                    result.IsMatch,
                    result.ExpectedRuleIds,
                    result.ActualRuleIds
                })
            });
            return fixturesValid ? 0 : 2;
        }
        if (options.Has("pack-coverage")) {
            return WriteJson(packs.Select(static pack => new {
                pack.PackId,
                pack.Version,
                Coverage = pack.GetCoverage()
            }));
        }
        if (options.Has("explain") || options.Has("dry-run")) {
            return WriteJson(plan.Explain());
        }

        EventType[] types = ParseTypes(options.GetMany("type"));
        string? logName = options.Get("log");
        string[] paths = options.GetMany("path");
        string? storePath = options.Get("store");
        bool storedSource = storePath != null;
        bool portableEvtx = options.Has("portable-evtx") || options.Get("portable-evtx-executable") != null;
        bool typedSource = types.Length > 0;
        if (storedSource && (logName != null || paths.Length > 0 || portableEvtx ||
                options.GetMany("machine").Length > 0 || options.GetMany("collector").Length > 0)) {
            throw new ArgumentException(
                "--store cannot be combined with live channels, saved EVTX paths, portable EVTX, machines, or collectors.");
        }
        if (!storedSource &&
            (typedSource && logName != null ||
             !typedSource && (logName != null ? 1 : 0) + (paths.Length > 0 ? 1 : 0) != 1)) {
            throw new ArgumentException(
                "detect requires --store, --type (optionally with --path), --log, or standalone --path as one source mode.");
        }
        if (!storedSource && typedSource &&
            (options.GetMany("event-id").Length > 0 || options.GetMany("provider").Length > 0)) {
            throw new ArgumentException(
                "--event-id and --provider are available only for generic --log or standalone --path detection sources because typed sources own their native selectors.");
        }
        if (options.GetMany("machine").Length > 0 && options.GetMany("collector").Length > 0) {
            throw new ArgumentException("--machine and --collector are mutually exclusive.");
        }
        if (portableEvtx && paths.Length == 0) {
            throw new ArgumentException("--portable-evtx requires at least one --path source.");
        }
        ISavedEventReader? savedEventReader = options.Get("portable-evtx-executable") is string executable
            ? new EvtxDumpSavedEventReader(executable)
            : portableEvtx
                ? new EvtxSavedEventReader()
                : null;
        DateTime? start = ParseDate(options.Get("start"));
        DateTime? end = ParseDate(options.Get("end"));
        if (options.Get("since") is string since) {
            start = DateTime.Now.Subtract(TimeSpan.Parse(since, CultureInfo.InvariantCulture));
        }
        long max = options.GetLong("max", 100000);
        if (max < 0) {
            throw new ArgumentOutOfRangeException("max");
        }
        var observations = new List<EventObservation>();
        var sourceFailures = new ConcurrentQueue<string>();
        Action<SavedEventReadDiagnostic>? savedEventDiagnosticHandler = portableEvtx
            ? diagnostic => {
                WriteSavedEventDiagnostic(diagnostic);
                if (diagnostic.AffectsCompleteness) {
                    sourceFailures.Enqueue(
                        $"Portable EVTX source reported {diagnostic.Code}: {diagnostic.Message}");
                }
            }
            : null;
        EventTypeQueryExecutionInfo? typedExecution = null;
        EventTypeProjectionPlan? detectionProjection = plan.RequiredEventTypes.Count == 0
            ? null
            : EventTypeCatalog.CompileProjectionPlan(plan.RequiredEventTypes);
        if (storedSource) {
            // Historical rows are loaded below together with the plan's stateful lookback window.
        } else if (types.Length > 0) {
            typedExecution = new EventTypeQueryExecutionInfo();
            var query = new EventTypeQuery(types) {
                Paths = paths.Length == 0 ? null : paths,
                SavedEventReader = savedEventReader,
                SavedEventDiagnosticHandler = savedEventDiagnosticHandler,
                MachineNames = NullWhenEmpty(options.GetMany("machine")),
                CollectorLogName = options.GetMany("collector").Length > 0 ? "ForwardedEvents" : null,
                StartTime = start,
                EndTime = end,
                MaxEvents = max,
                MaxCandidates = max == 0 ? 0 : Math.Max(max, 100000),
                Oldest = true,
                ReadMode = EventReadMode.StructuredDataAndMessage
            };
            if (options.GetMany("collector").Length > 0) {
                query.MachineNames = options.GetMany("collector");
            }
            await foreach (EventTypeRecord record in EventTypeEngine.ReadAsync(query, typedExecution)) {
                EventTypeRecord? projected = detectionProjection == null
                    ? null
                    : EventTypeCatalog.CreateEventRule(record.SourceEvent, detectionProjection);
                observations.Add(EventObservation.Create(record.SourceEvent, projected));
            }
        } else {
            int[] requestedEventIds = ParseInts(options.GetMany("event-id")) ?? Array.Empty<int>();
            int[] explicitEventIds = requestedEventIds.Length > 0
                ? requestedEventIds
                : GetRequiredEventIds(plan);
            string[] providers = options.GetMany("provider");
            var filter = new EventFilter {
                EventIds = explicitEventIds.Length == 0 ? null : explicitEventIds,
                ProviderNames = providers.Length == 0 ? null : providers,
                StartTime = start,
                EndTime = end
            };
            string xpath = EventFilterCompiler.BuildXPath(filter);
            if (logName != null) {
                string[] collectors = options.GetMany("collector");
                bool useCollectorLog = collectors.Length > 0;
                string[] targets = useCollectorLog
                    ? collectors
                    : options.GetMany("machine");
                if (targets.Length == 0) {
                    targets = new[] { string.Empty };
                }
                for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++) {
                    string target = targets[targetIndex];
                    long remaining = Remaining(max, observations.Count);
                    if (max > 0 && remaining == 0) {
                        sourceFailures.Enqueue(
                            $"The generic source result limit of {max} observations was reached before target '{DisplayTarget(target)}' was evaluated.");
                        break;
                    }
                    EventLogChannelQuery query = useCollectorLog
                        ? EventLogChannelQuery.ForCollector(logName, target, xpath)
                        : new EventLogChannelQuery(logName) {
                            MachineName = target,
                            XPath = xpath
                        };
                    query.Oldest = true;
                    query.ReadMode = EventReadMode.StructuredDataAndMessage;
                    query.MaxEvents = ProbeLimit(max, remaining);
                    await foreach (EventObject source in EventLogEngine.ReadChannelAsync(query)) {
                        if (max > 0 && observations.Count >= max) {
                            sourceFailures.Enqueue(
                                $"The generic source result limit of {max} observations was reached while reading target '{DisplayTarget(target)}'; later matching events were not evaluated.");
                            break;
                        }
                        EventTypeRecord? projected = detectionProjection == null
                            ? null
                            : EventTypeCatalog.CreateEventRule(source, detectionProjection);
                        observations.Add(EventObservation.Create(source, projected));
                    }
                    if (max > 0 && observations.Count >= max && targetIndex + 1 < targets.Length) {
                        sourceFailures.Enqueue(
                            $"The generic source result limit of {max} observations was reached before {targets.Length - targetIndex - 1} later target(s) were evaluated.");
                        break;
                    }
                }
            } else {
                for (int pathIndex = 0; pathIndex < paths.Length; pathIndex++) {
                    string path = paths[pathIndex];
                    long remaining = Remaining(max, observations.Count);
                    if (max > 0 && remaining == 0) {
                        sourceFailures.Enqueue(
                            $"The generic source result limit of {max} observations was reached before file '{Path.GetFullPath(path)}' was evaluated.");
                        break;
                    }
                    var query = new EventLogFileQuery(path) {
                        XPath = xpath,
                        SavedEventReader = savedEventReader,
                        SavedEventDiagnosticHandler = savedEventDiagnosticHandler,
                        Oldest = true,
                        ReadMode = EventReadMode.StructuredDataAndMessage,
                        MaxEvents = ProbeLimit(max, remaining)
                    };
                    await foreach (EventObject source in EventLogEngine.ReadFileAsync(query)) {
                        if (max > 0 && observations.Count >= max) {
                            sourceFailures.Enqueue(
                                $"The generic source result limit of {max} observations was reached while reading file '{Path.GetFullPath(path)}'; later matching events were not evaluated.");
                            break;
                        }
                        EventTypeRecord? projected = detectionProjection == null
                            ? null
                            : EventTypeCatalog.CreateEventRule(source, detectionProjection);
                        observations.Add(EventObservation.Create(source, projected));
                    }
                    if (max > 0 && observations.Count >= max && pathIndex + 1 < paths.Length) {
                        sourceFailures.Enqueue(
                            $"The generic source result limit of {max} observations was reached before {paths.Length - pathIndex - 1} later file(s) were evaluated.");
                        break;
                    }
                }
            }
        }

        foreach (string failure in CreateTypedSourceFailures(typedExecution)) {
            sourceFailures.Enqueue(failure);
        }
        string[] sourceFailureSnapshot = sourceFailures
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        EventDetectionCoverage coverage = options.Get("coverage") is string coveragePath
            ? EventDetectionCoverage.FromJson(
                    await File.ReadAllTextAsync(Path.GetFullPath(coveragePath)).ConfigureAwait(false))
                .WithFailures(sourceFailureSnapshot)
            : storedSource
                ? EventDetectionCoverage.Create(
                    expectedTargets: new[] { Path.GetFullPath(storePath!) },
                    observedTargets: new[] { Path.GetFullPath(storePath!) },
                    failures: new[] {
                        "Stored history ingestion coverage was not supplied. Pass --coverage with a versioned EventDetectionCoverage document before treating an empty historical result as clean."
                    })
                : CreateDetectionCoverage(types, logName, paths, observations, plan, options, sourceFailureSnapshot);
        var engineOptions = new EventDetectionEngineOptions(
            maximumObservations: options.GetLong("maximum-observations", 1000000),
            maximumGroups: options.GetInt("maximum-groups", 25000),
            maximumStateObservations: options.GetInt("maximum-state-observations", 250000),
            maximumStateBytes: options.GetLong("maximum-state-bytes", 256L * 1024L * 1024L),
            coverage: coverage);
        EventDetectionExecutionResult execution;
        if (storedSource) {
            var historicalQuery = new EventStoreQuery {
                Types = types.Length == 0 ? null : types,
                StartTime = start,
                EndTime = end,
                EventIds = ParseInts(options.GetMany("event-id")),
                Providers = NullWhenEmpty(options.GetMany("provider")),
                MaxEvents = max,
                MaxCandidates = engineOptions.MaximumObservations,
                Oldest = true
            };
            execution = await new EventStore(storePath!).EvaluateDetectionAsync(
                historicalQuery,
                plan,
                engineOptions).ConfigureAwait(false);
        } else {
            execution = EventDetectionEngine.Evaluate(observations, plan, engineOptions);
        }
        if (options.Get("write-findings-store") is string findingStore) {
            await new EventStore(findingStore).WriteFindingsAsync(execution.Findings).ConfigureAwait(false);
        }
        if (options.Get("trace-jsonl") is string tracePath) {
            string fullTracePath = Path.GetFullPath(tracePath);
            string? traceDirectory = Path.GetDirectoryName(fullTracePath);
            if (!string.IsNullOrWhiteSpace(traceDirectory)) {
                Directory.CreateDirectory(traceDirectory!);
            }
            using var traceWriter = new StreamWriter(fullTracePath, append: false, new UTF8Encoding(false));
            foreach (EventObservation observation in execution.Observations) {
                foreach (EventDetectionRuleTrace trace in EventDetectionEngine.Explain(
                             observation,
                             plan,
                             execution.Coverage)) {
                    await traceWriter.WriteLineAsync(EventAnalysisJson.Serialize(trace)).ConfigureAwait(false);
                }
            }
        }
        using StreamWriter? jsonLines = CreateJsonLinesWriter(options.Get("jsonl"));
        foreach (EventDetectionFinding finding in execution.Findings) {
            string json = EventAnalysisJson.Serialize(finding);
            if (jsonLines != null) {
                await jsonLines.WriteLineAsync(json).ConfigureAwait(false);
            } else {
                Console.WriteLine(json);
            }
        }
        string? html = options.Get("report-html");
        string? csv = options.Get("report-csv");
        string? excel = options.Get("report-excel");
        if (html != null || csv != null || excel != null) {
            var reportOptions = new EventDetectionReportOptions(
                options.Get("title") ?? "EventViewerX detection report",
                storedSource
                    ? "EventStore historical query"
                    : types.Length > 0 ? "Typed EventViewerX query" : "Native Event Log query",
                storedSource,
                new[] { $"Maximum source observations: {(max == 0 ? "unlimited" : max.ToString(CultureInfo.InvariantCulture))}" },
                coverage: execution.Coverage);
            EventDetectionReportSnapshot snapshot = options.Get("report-kind") is string reportKindText
                ? EventDecisionReportEngine.Create(
                    Enum.Parse<EventDecisionReportKind>(reportKindText, ignoreCase: true),
                    execution.Observations,
                    execution.Findings,
                    packs,
                    reportOptions).Analysis
                : EventDetectionReportEngine.Create(
                    execution.Observations,
                    execution.Findings,
                    packs,
                    reportOptions);
            if (html != null) {
                Console.WriteLine(EventReportHtmlRenderer.Save(snapshot.PresentationReport, html));
            }
            if (csv != null) {
                Console.WriteLine(EventReportCsvRenderer.Save(snapshot.PresentationReport, csv));
            }
            if (excel != null) {
                Console.WriteLine(EventReportExcelRenderer.Save(snapshot.PresentationReport, excel));
            }
        }
        return execution.IsComplete ? 0 : 2;
    }

    private static long Remaining(long maximum, int current) =>
        maximum == 0 ? 0 : Math.Max(0, maximum - current);

    private static long ProbeLimit(long maximum, long remaining) =>
        maximum == 0 ? 0 : remaining == long.MaxValue ? long.MaxValue : remaining + 1;

    private static string DisplayTarget(string target) =>
        string.IsNullOrWhiteSpace(target) ? Environment.MachineName : target;

    private static EventDetectionCoverage CreateDetectionCoverage(
        IReadOnlyList<EventType> selectedTypes,
        string? logName,
        IReadOnlyList<string> paths,
        IReadOnlyList<EventObservation> observations,
        EventDetectionPlan plan,
        CliArguments options,
        IReadOnlyList<string> sourceFailures) {

        string[] requestedTargets = options.GetMany("collector").Concat(options.GetMany("machine"))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        if (requestedTargets.Length == 0) {
            requestedTargets = paths.Count > 0 ? paths.ToArray() : new[] { Environment.MachineName };
        }
        EventType[] expectedTypes = plan.RequiredEventTypes.ToArray();
        EventType[] successfulTypes = selectedTypes.Count > 0
            ? EventTypeCatalog.Expand(selectedTypes).Where(expectedTypes.Contains).ToArray()
            : observations.Select(static observation => observation.TypeName)
                .Select(static name => Enum.TryParse(name, ignoreCase: true, out EventType type) ? (EventType?)type : null)
                .Where(static type => type.HasValue)
                .Select(static type => type!.Value)
                .Distinct()
                .ToArray();
        EventSourceDefinition[] selectedSources = selectedTypes.Count > 0
            ? EventTypeCatalog.GetSources(selectedTypes).ToArray()
            : Array.Empty<EventSourceDefinition>();
        string[] expectedChannels = GetRequiredChannels(plan);
        string[] successfulChannels = selectedTypes.Count > 0
            ? selectedSources.Select(static source => source.LogName).ToArray()
            : logName != null
                ? new[] { logName }
                : observations.Select(static observation => observation.SourceLog)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        string[] expectedProviders = plan.Rules.SelectMany(static rule => rule.Providers
                .Concat(rule.Steps.SelectMany(static step => step.Providers)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] requestedProviders = options.GetMany("provider");
        string[] successfulProviders = expectedProviders.Length == 0
            ? observations.Select(static observation => observation.ProviderName)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : requestedProviders.Length == 0
                ? expectedProviders
                : expectedProviders.Where(provider => requestedProviders.Contains(
                    provider,
                    StringComparer.OrdinalIgnoreCase)).ToArray();
        int[] expectedEventIds = GetRequiredEventIds(plan);
        int[] requestedEventIds = ParseInts(options.GetMany("event-id")) ?? Array.Empty<int>();
        int[] successfulEventIds = selectedTypes.Count > 0
            ? selectedSources.SelectMany(static source => source.EventIds).Distinct().ToArray()
            : requestedEventIds.Length == 0
                ? expectedEventIds
                : expectedEventIds.Where(requestedEventIds.Contains).ToArray();
        return EventDetectionCoverage.Create(
            expectedTargets: requestedTargets,
            observedTargets: requestedTargets,
            expectedChannels: expectedChannels,
            observedChannels: successfulChannels,
            expectedProviders: expectedProviders,
            observedProviders: successfulProviders,
            expectedEventIds: expectedEventIds,
            observedEventIds: successfulEventIds,
            expectedEventTypes: expectedTypes,
            observedEventTypes: successfulTypes,
            failures: sourceFailures);
    }

    private static int[] GetRequiredEventIds(EventDetectionPlan plan) {
        EventType[] eventTypes = plan.RequiredEventTypes.ToArray();
        return plan.Rules.SelectMany(static rule => rule.EventIds
                .Concat(rule.Steps.SelectMany(static step => step.EventIds)))
            .Concat(EventTypeCatalog.GetSources(eventTypes).SelectMany(static source => source.EventIds))
            .Distinct()
            .ToArray();
    }

    private static string[] GetRequiredChannels(EventDetectionPlan plan) =>
        plan.Rules.SelectMany(static rule => rule.Channels
                .Concat(rule.Steps.SelectMany(static step => step.Channels)))
            .Concat(EventTypeCatalog.GetSources(plan.RequiredEventTypes)
                .Select(static source => source.LogName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string[] CreateTypedSourceFailures(EventTypeQueryExecutionInfo? execution) {
        if (execution == null) {
            return Array.Empty<string>();
        }
        var failures = new List<string>();
        if (execution.ResultLimitReached) {
            failures.Add(
                $"The typed source result limit was reached after {execution.EventsEmitted} observations; later matching events were not evaluated.");
        }
        if (execution.ScanLimitReached) {
            failures.Add(
                $"The typed source candidate scan limit of {execution.MaxEventsScanned} was reached after {execution.EventsScanned} candidates; detection coverage is incomplete.");
        }
        failures.AddRange(execution.TargetFailures.Select(static failure =>
            $"Typed source '{failure.LogName}' on '{failure.MachineName}' failed with {failure.Kind}: {failure.Message}"));
        return failures.ToArray();
    }
}
