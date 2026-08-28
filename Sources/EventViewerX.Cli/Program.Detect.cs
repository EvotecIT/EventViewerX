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
        foreach (string sigmaPath in sigmaPaths) {
            SigmaCompilationResult result = SigmaRuleCompiler.Load(sigmaPath);
            foreach (SigmaDiagnostic diagnostic in result.Diagnostics) {
                Console.Error.WriteLine($"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message}");
            }
            if (!result.IsSupported) {
                throw new InvalidDataException($"Sigma input '{sigmaPath}' contains unsupported or invalid behavior.");
            }
            rules.AddRange(result.Rules);
        }
        EventDetectionPlan plan = EventDetectionPlan.Compile(rules, tuning);
        if (options.Has("explain")) {
            return WriteJson(plan.Explain());
        }

        EventType[] types = ParseTypes(options.GetMany("type"));
        string? logName = options.Get("log");
        string[] paths = options.GetMany("path");
        bool portableEvtx = options.Has("portable-evtx") || options.Get("portable-evtx-executable") != null;
        bool typedSource = types.Length > 0;
        if (typedSource && logName != null || !typedSource && (logName != null ? 1 : 0) + (paths.Length > 0 ? 1 : 0) != 1) {
            throw new ArgumentException(
                "detect requires --type (optionally with --path), --log, or standalone --path as one source mode.");
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
        EventTypeProjectionPlan? detectionProjection = plan.RequiredEventTypes.Count == 0
            ? null
            : EventTypeCatalog.CompileProjectionPlan(plan.RequiredEventTypes);
        if (types.Length > 0) {
            var query = new EventTypeQuery(types) {
                Paths = paths.Length == 0 ? null : paths,
                SavedEventReader = savedEventReader,
                SavedEventDiagnosticHandler = portableEvtx ? WriteSavedEventDiagnostic : null,
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
            await foreach (EventTypeRecord record in EventTypeEngine.ReadAsync(query)) {
                EventTypeRecord? projected = detectionProjection == null
                    ? null
                    : EventTypeCatalog.CreateEventRule(record.SourceEvent, detectionProjection);
                observations.Add(EventObservation.Create(record.SourceEvent, projected));
            }
        } else {
            int[] requestedEventIds = ParseInts(options.GetMany("event-id")) ?? Array.Empty<int>();
            int[] explicitEventIds = requestedEventIds.Length > 0
                ? requestedEventIds
                : plan.Rules
                    .SelectMany(static rule => rule.EventIds.Concat(rule.Steps.SelectMany(static step => step.EventIds)))
                    .Distinct()
                    .ToArray();
            string[] providers = options.GetMany("provider");
            var filter = new EventFilter {
                EventIds = explicitEventIds.Length == 0 ? null : explicitEventIds,
                ProviderNames = providers.Length == 0 ? null : providers,
                StartTime = start,
                EndTime = end
            };
            string xpath = EventFilterCompiler.BuildXPath(filter);
            if (logName != null) {
                string[] targets = options.GetMany("collector").Length > 0
                    ? options.GetMany("collector")
                    : options.GetMany("machine");
                if (targets.Length == 0) {
                    targets = new[] { string.Empty };
                }
                foreach (string target in targets) {
                    var query = new EventLogChannelQuery(logName) {
                        MachineName = target,
                        XPath = xpath,
                        Oldest = true,
                        ReadMode = EventReadMode.StructuredDataAndMessage,
                        MaxEvents = Remaining(max, observations.Count)
                    };
                    await foreach (EventObject source in EventLogEngine.ReadChannelAsync(query)) {
                        EventTypeRecord? projected = detectionProjection == null
                            ? null
                            : EventTypeCatalog.CreateEventRule(source, detectionProjection);
                        observations.Add(EventObservation.Create(source, projected));
                    }
                    if (max > 0 && observations.Count >= max) {
                        break;
                    }
                }
            } else {
                foreach (string path in paths) {
                    var query = new EventLogFileQuery(path) {
                        XPath = xpath,
                        SavedEventReader = savedEventReader,
                        SavedEventDiagnosticHandler = portableEvtx ? WriteSavedEventDiagnostic : null,
                        Oldest = true,
                        ReadMode = EventReadMode.StructuredDataAndMessage,
                        MaxEvents = Remaining(max, observations.Count)
                    };
                    await foreach (EventObject source in EventLogEngine.ReadFileAsync(query)) {
                        EventTypeRecord? projected = detectionProjection == null
                            ? null
                            : EventTypeCatalog.CreateEventRule(source, detectionProjection);
                        observations.Add(EventObservation.Create(source, projected));
                    }
                    if (max > 0 && observations.Count >= max) {
                        break;
                    }
                }
            }
        }

        var engineOptions = new EventDetectionEngineOptions {
            MaximumObservations = options.GetLong("maximum-observations", 1000000),
            MaximumGroups = options.GetInt("maximum-groups", 25000),
            MaximumStateObservations = options.GetInt("maximum-state-observations", 250000),
            MaximumStateBytes = options.GetLong("maximum-state-bytes", 256L * 1024L * 1024L)
        };
        EventDetectionExecutionResult execution = EventDetectionEngine.Evaluate(observations, plan, engineOptions);
        if (options.Get("write-findings-store") is string findingStore) {
            await new EventStore(findingStore).WriteFindingsAsync(execution.Findings).ConfigureAwait(false);
        }
        using StreamWriter? jsonLines = CreateJsonLinesWriter(options.Get("jsonl"));
        foreach (EventDetectionFinding finding in execution.Findings) {
            string json = JsonSerializer.Serialize(finding, JsonOptions);
            if (jsonLines != null) {
                await jsonLines.WriteLineAsync(json).ConfigureAwait(false);
            } else {
                Console.WriteLine(json);
            }
        }
        EventDetectionReportSnapshot snapshot = EventDetectionReportEngine.Create(
            observations,
            execution.Findings,
            packs,
            new EventDetectionReportOptions {
                Title = options.Get("title") ?? "EventViewerX detection report",
                QueryOwner = types.Length > 0 ? "Typed EventViewerX query" : "Native Event Log query",
                Limits = new[] { $"Maximum source observations: {(max == 0 ? "unlimited" : max.ToString(CultureInfo.InvariantCulture))}" }
            });
        if (options.Get("report-html") is string html) {
            Console.WriteLine(EventReportHtmlRenderer.Save(snapshot.PresentationReport, html));
        }
        if (options.Get("report-csv") is string csv) {
            Console.WriteLine(EventReportCsvRenderer.Save(snapshot.PresentationReport, csv));
        }
        if (options.Get("report-excel") is string excel) {
            Console.WriteLine(EventReportExcelRenderer.Save(snapshot.PresentationReport, excel));
        }
        return execution.IsComplete ? 0 : 2;
    }

    private static long Remaining(long maximum, int current) =>
        maximum == 0 ? 0 : Math.Max(0, maximum - current);
}
