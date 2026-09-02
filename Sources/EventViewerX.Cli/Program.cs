using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EventViewerX.Providers;
using EventViewerX.Evtx;
using EventViewerX.Reporting;
using EventViewerX.Storage;
using HtmlForgeX;

namespace EventViewerX.Cli;

internal static partial class Program {
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions() {
        var options = new JsonSerializerOptions { WriteIndented = false };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static JsonElement ParseJsonElement(string json) {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static async Task<int> Main(string[] args) {
        try {
            var options = new CliArguments(args);
            ValidateOptions(options);
            return options.Command switch {
                "query" => await QueryAsync(options).ConfigureAwait(false),
                "report" => await ReportAsync(options).ConfigureAwait(false),
                "measure" => await MeasureAsync(options).ConfigureAwait(false),
                "detect" => await DetectAsync(options).ConfigureAwait(false),
                "watch" => await WatchAsync(options).ConfigureAwait(false),
                "store" => await StoreAsync(options).ConfigureAwait(false),
                "collector" => Collector(options),
                "provider" => Provider(options),
                "types" => ListTypes(options),
                "schemas" => WriteJson(EventAnalysisContractCatalog.GetContracts().Select(static contract => new {
                    contract.Kind,
                    contract.SchemaVersion,
                    Schema = ParseJsonElement(contract.JsonSchema)
                })),
                "version" or "--version" or "-v" => Version(),
                "help" or "--help" or "-h" => Help(),
                _ => throw new ArgumentException($"Unknown command '{options.Command}'.")
            };
        } catch (OperationCanceledException) {
            Console.Error.WriteLine("Canceled.");
            return 130;
        } catch (Exception exception) {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static async Task<int> QueryAsync(CliArguments options) {
        ValidateQuerySource(options, allowSummary: false);
        ValidateOccurrenceOptions(options);
        if (options.Get("store") is string storePath) {
            EventStoreQuery storedQuery = CreateStoreQuery(options);
            if (options.Has("explain")) {
                if (storedQuery.Predicate == null) {
                    throw new ArgumentException("--explain requires --where.");
                }
                EventStoreQueryPlan plan = await new EventStore(storePath)
                    .PlanAsync(storedQuery)
                    .ConfigureAwait(false);
                return WriteJson(plan);
            }
            EventReport stored = await new EventStore(storePath)
                .ReadReportAsync(storedQuery, options.Get("title"))
                .ConfigureAwait(false);
            return WriteRows(ApplyOccurrenceGrouping(stored, options));
        }
        if (options.Get("context-store") != null) {
            EventReport contextual = await QueryGroupPolicyReportAsync(options).ConfigureAwait(false);
            await WriteStoreIfRequestedAsync(contextual, options).ConfigureAwait(false);
            return WriteRows(ApplyOccurrenceGrouping(contextual, options));
        }
        EventReportRequest request = CreateRequest(options);
        CollectionCheckpointContext? checkpoint =
            await PrepareCollectionCheckpointAsync(request, options)
                .ConfigureAwait(false);
        if (options.Has("explain")) {
            EventPredicate predicate = request.Predicate ??
                throw new ArgumentException("--explain requires --where.");
            if (request.Types != null && request.Types.Count > 0) {
                predicate = EventPredicateBuilder.ForTypes(request.Types).Normalize(predicate);
            }
            EventPredicatePlan plan = request.Definition != null
                ? EventDefinitionEngine.PlanPredicate(
                    request.Definition,
                    predicate,
                    request.Collectors != null && request.Collectors.Count > 0
                        ? "ForwardedEvents"
                        : null)
                : request.Collectors != null && request.Collectors.Count > 0
                    ? EventPredicatePlanner.PlanManaged(
                        predicate,
                        "ForwardedEvents uses the Windows Server 2025 safe '*' reader, so typed filtering is bounded and managed.")
                    : EventPredicatePlanner.Plan(predicate);
            return WriteJson(plan);
        }
        EventReport report = await EventReportEngine.QueryAsync(request).ConfigureAwait(false);
        if (checkpoint != null) {
            await WriteCheckpointedStoreAsync(report, checkpoint)
                .ConfigureAwait(false);
        } else {
            await WriteStoreIfRequestedAsync(report, options).ConfigureAwait(false);
        }
        return WriteRows(ApplyOccurrenceGrouping(report, options));
    }

    private static async Task<int> ReportAsync(CliArguments options) {
        ValidateQuerySource(options, allowSummary: true);
        ValidateOccurrenceOptions(options);
        EventReport report;
        if (options.Get("store") is string storePath) {
            var store = new EventStore(storePath);
            EventStoreQuery query = CreateStoreQuery(options);
            report = options.Get("summary") is string summary
                ? await store.CreateSummaryReportAsync(
                    query,
                    ParseSummaryPeriod(summary),
                    options.Get("title")).ConfigureAwait(false)
                : await store.ReadReportAsync(query, options.Get("title")).ConfigureAwait(false);
        } else {
            report = options.Get("context-store") != null
                ? await QueryGroupPolicyReportAsync(options).ConfigureAwait(false)
                : await EventReportEngine.QueryAsync(CreateRequest(options)).ConfigureAwait(false);
            await WriteStoreIfRequestedAsync(report, options).ConfigureAwait(false);
        }
        report = ApplyOccurrenceGrouping(report, options);
        bool written = false;
        EventEmailPackage? emailPackage = null;
        if (options.Get("html") is string html) {
            var htmlOptions = new EventReportHtmlOptions {
                RecordDrawerPlacement = ParseDrawerPlacement(options.Get("drawer-placement"))
            };
            Console.WriteLine(EventReportHtmlRenderer.Save(report, html, htmlOptions));
            written = true;
        }
        if (options.Get("excel") is string excel) {
            Console.WriteLine(EventReportExcelRenderer.Save(report, excel));
            written = true;
        }
        if (options.Get("csv") is string csv) {
            Console.WriteLine(EventReportCsvRenderer.Save(report, csv));
            written = true;
        }
        if (options.Get("email-html") is string emailHtml) {
            emailPackage = await EventReportEmailRenderer.RenderAsync(report, options.GetInt("email-rows", 25)).ConfigureAwait(false);
            string fullPath = Path.GetFullPath(emailHtml);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllTextAsync(fullPath, emailPackage.Html, new UTF8Encoding(false)).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.ChangeExtension(fullPath, ".txt"), emailPackage.PlainText, new UTF8Encoding(false)).ConfigureAwait(false);
            Console.WriteLine(fullPath);
            written = true;
        }
        if (options.Get("mail-profile") is string mailProfile) {
            emailPackage ??= await EventReportEmailRenderer.RenderAsync(report, options.GetInt("email-rows", 25)).ConfigureAwait(false);
            SmtpNotificationProfile profile = SmtpNotificationProfile.Load(mailProfile);
            Mailozaurr.SmtpResult result = await profile.SendAsync(emailPackage, report.Title).ConfigureAwait(false);
            Console.WriteLine(JsonSerializer.Serialize(new {
                Delivered = result.Status,
                profile.DryRun,
                result.Server,
                result.Port,
                result.MessageId,
                result.TimeToExecute
            }, JsonOptions));
            written = true;
        }
        if (!written) {
            throw new ArgumentException("report requires --html, --excel, --csv, --email-html, or --mail-profile.");
        }
        return 0;
    }

    private static async Task<int> StoreAsync(CliArguments options) {
        if (options.Subcommand == "integrity") {
            EventStoreIntegrityResult result = await new EventStore(options.Require("path"))
                .CheckIntegrityAsync().ConfigureAwait(false);
            WriteJson(result);
            return result.IsHealthy ? 0 : 2;
        }
        if (options.Subcommand == "backup") {
            EventStoreBackupResult result = await new EventStore(options.Require("path"))
                .BackupAsync(options.Require("output"), options.Has("force"))
                .ConfigureAwait(false);
            return WriteJson(result);
        }
        if (options.Subcommand == "restore") {
            EventStoreIntegrityResult result = await new EventStore(options.Require("path"))
                .RestoreAsync(options.Require("backup"))
                .ConfigureAwait(false);
            return WriteJson(result);
        }
        if (options.Subcommand == "retention") {
            TimeSpan? eventRetention = options.Get("events-for") is string eventsFor
                ? TimeSpan.Parse(eventsFor, CultureInfo.InvariantCulture)
                : null;
            TimeSpan? findingRetention = options.Get("findings-for") is string findingsFor
                ? TimeSpan.Parse(findingsFor, CultureInfo.InvariantCulture)
                : null;
            EventStoreRetentionResult result = await new EventStore(options.Require("path"))
                .ApplyRetentionAsync(new EventStoreRetentionPolicy {
                    EventRetention = eventRetention,
                    FindingRetention = findingRetention,
                    VacuumAfterPrune = options.Has("vacuum")
                }).ConfigureAwait(false);
            return WriteJson(result);
        }
        if (options.Subcommand == "reset-checkpoint") {
            bool removed = await new EventStore(options.Require("path"))
                .DeleteCheckpointAsync(
                    options.Require("consumer"),
                    NormalizeCheckpointComputer(options.Require("computer")),
                    options.Require("container"))
                .ConfigureAwait(false);
            return WriteJson(new { Removed = removed });
        }
        if (options.Subcommand != "prune") {
            throw new ArgumentException(
                "store supports integrity, backup, restore, retention, prune, and reset-checkpoint. " +
                "Use query/report --store for reading and --write-store for ingestion.");
        }
        DateTime before = ParseDate(options.Require("before"))!.Value;
        int deleted = await new EventStore(options.Require("path"))
            .PruneBeforeAsync(before, NullWhenEmpty(options.GetMany("definition-name")))
            .ConfigureAwait(false);
        return WriteJson(new { Deleted = deleted, Before = before.ToUniversalTime() });
    }

    private static EventStoreQuery CreateStoreQuery(CliArguments options) {
        DateTime? start = ParseDate(options.Get("start"));
        if (options.Get("since") is string since) {
            start = DateTime.Now.Subtract(TimeSpan.Parse(since, CultureInfo.InvariantCulture));
        }
        EventType[] types = ParseTypes(options.GetMany("type"));
        EventMonitoringPresetDefinition? preset = ParsePreset(options.Get("preset"));
        if (preset != null) {
            if (types.Length > 0 || options.Get("definition") != null || options.GetMany("definition-name").Length > 0) {
                throw new ArgumentException("--preset is mutually exclusive with --type, --definition, and --definition-name.");
            }
            types = preset.Types.ToArray();
        }
        EventDefinition? definition = options.Get("definition") is string definitionPath
            ? EventDefinition.Load(definitionPath)
            : null;
        EventStoreQuery query = preset == null
            ? new EventStoreQuery()
            : EventStoreQuery.ForPreset(preset.Preset);
        EventPredicate? predicate = CombinePredicates(query.Predicate, ParsePredicate(options.Get("where")));
        if (predicate != null) {
            predicate = definition != null
                ? EventPredicateBuilder.ForDefinition(definition).Normalize(predicate)
                : types.Length > 0
                    ? EventStoreQuery.IsEnrichedGroupPolicySelection(types)
                        ? EventReportSectionSchema.FromGroupPolicyAudit().CreatePredicateBuilder().Normalize(predicate)
                        : EventPredicateBuilder.ForTypes(types).Normalize(predicate)
                    : predicate;
        }
        query.Types = types.Length == 0 ? null : types;
        query.DefinitionNames = definition == null
            ? NullWhenEmpty(options.GetMany("definition-name"))
            : new[] { definition.Name };
        query.DefinitionSchemas = definition == null
            ? null
            : new[] { EventReportSectionSchema.FromDefinition(definition) };
        query.StartTime = start;
        query.EndTime = ParseDate(options.Get("end"));
        query.EventIds = ParseInts(options.GetMany("event-id"));
        query.RecordIds = ParseLongs(options.GetMany("record-id"));
        query.SourceComputers = NullWhenEmpty(options.GetMany("source"));
        query.SourceLogs = NullWhenEmpty(options.GetMany("log"));
        query.Providers = NullWhenEmpty(options.GetMany("provider"));
        query.Predicate = predicate;
        query.MaxEvents = options.GetLong("max");
        query.MaxCandidates = options.GetLong("max-candidates", 100000);
        query.Oldest = options.Has("oldest");
        return query;
    }

    private static async Task WriteStoreIfRequestedAsync(EventReport report, CliArguments options) {
        if (options.Get("write-store") is not string path) {
            return;
        }
        EventStoreWriteResult result = await new EventStore(path).WriteAsync(report).ConfigureAwait(false);
        Console.Error.WriteLine(
            $"Stored {result.Inserted} new rows; skipped {result.Duplicates} duplicates in {Path.GetFullPath(path)}.");
    }

    private static EventStoreSummaryPeriod ParseSummaryPeriod(string value) =>
        Enum.TryParse(value, ignoreCase: true, out EventStoreSummaryPeriod parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new ArgumentException("--summary must be Hour, Day, Week, or Month.");

    private static void ValidateQuerySource(CliArguments options, bool allowSummary) {
        bool stored = options.Get("store") != null;
        if (options.GetMany("machine").Length > 0 && options.GetMany("collector").Length > 0) {
            throw new ArgumentException("--machine and --collector are mutually exclusive target modes.");
        }
        if (options.Has("explain") && options.Get("write-store") != null) {
            throw new ArgumentException(
                "--explain cannot be combined with --write-store because explanation does not read or persist events.");
        }
        if (stored && (options.GetMany("path").Length > 0 ||
                       options.GetMany("machine").Length > 0 || options.GetMany("collector").Length > 0)) {
            throw new ArgumentException(
                "--store cannot be combined with --path, --machine, or --collector. " +
                "Use --type, --definition, --definition-name, --log, --source, and --provider to filter stored rows.");
        }
        if (options.Get("context-store") != null) {
            if (options.Has("explain")) {
                throw new ArgumentException(
                    "--explain cannot be combined with --context-store because contextual Group Policy queries update persistent context while reading the complete timeline.");
            }
            EventType[] contextTypes = ParseTypes(options.GetMany("type"));
            if (stored || contextTypes.Length != 1 || contextTypes[0] != EventType.GroupPolicyDirectoryAudit) {
                throw new ArgumentException("--context-store requires exactly --type GroupPolicyDirectoryAudit and a live or offline source.");
            }
            if (options.Get("preset") != null || options.Get("definition") != null || options.Get("log") != null ||
                options.Get("where") != null || options.GetMany("event-id").Length > 0 ||
                options.GetMany("record-id").Length > 0 || options.Has("resolve-dns") || options.Get("checkpoint") != null) {
                throw new ArgumentException(
                    "--context-store cannot be combined with --preset, --definition, --log, --where, --event-id, --record-id, --resolve-dns, or --checkpoint.");
            }
        } else if (options.Get("context-authorization") != null) {
            throw new ArgumentException("--context-authorization requires --context-store.");
        }
        if (stored && options.Get("definition") != null && options.GetMany("type").Length > 0) {
            throw new ArgumentException("--store accepts either --type or --definition metadata, not both.");
        }
        if (stored && options.Get("definition") != null && options.GetMany("definition-name").Length > 0) {
            throw new ArgumentException(
                "--definition selects its own stored definition and cannot be combined with --definition-name.");
        }
        if (stored && options.GetMany("type").Length > 0 &&
            options.GetMany("definition-name").Length > 0) {
            throw new ArgumentException(
                "--type and --definition-name are mutually exclusive stored definition selectors.");
        }
        if (options.Get("preset") != null &&
            (options.GetMany("type").Length > 0 || options.Get("definition") != null ||
             options.GetMany("definition-name").Length > 0 || options.Get("log") != null)) {
            throw new ArgumentException("--preset is mutually exclusive with --type, --definition, --definition-name, and --log.");
        }
        if (stored && options.Get("write-store") != null) {
            throw new ArgumentException("--write-store is only valid for live or offline event-log ingestion.");
        }
        if (stored && options.Get("checkpoint") != null) {
            throw new ArgumentException(
                "--checkpoint is only valid for live collector ingestion with --write-store.");
        }
        if (stored && (options.Has("resolve-dns") || options.Has("concurrency"))) {
            throw new ArgumentException(
                "--resolve-dns and --concurrency are live event-source options and cannot be combined with --store.");
        }
        if (!stored && (options.Get("definition-name") != null || options.Get("source") != null ||
                        options.Get("provider") != null || options.Get("summary") != null)) {
            throw new ArgumentException(
                "--definition-name, --source, --provider, and --summary require --store.");
        }
        bool typedSource = options.Get("definition") != null || options.GetMany("type").Length > 0 || options.Get("preset") != null;
        if (!stored && !typedSource && options.Get("where") != null) {
            throw new ArgumentException(
                "--where requires --type or --definition for live and offline event-log queries. " +
                "Use --event-id and --record-id for generic --log or standalone --path queries.");
        }
        if (!stored && typedSource && options.GetMany("event-id").Length > 0) {
            throw new ArgumentException(
                "--event-id is available only for generic --log or standalone --path queries because typed sources own event IDs. " +
                "Use a typed EventId --where predicate to further restrict typed events.");
        }
        if (!allowSummary && options.Get("summary") != null) {
            throw new ArgumentException("--summary is available through the report command.");
        }
        if (options.Get("summary") != null &&
            ParseEnum(options.Get("duplicates"), EventDuplicateMode.None, "--duplicates") != EventDuplicateMode.None) {
            throw new ArgumentException(
                "--summary cannot be combined with --duplicates because stored summary rows are already derived data.");
        }
    }

    private static EventReportRequest CreateRequest(CliArguments options) {
        if (options.Has("portable-evtx") && options.Get("portable-evtx-executable") != null) {
            throw new ArgumentException(
                "--portable-evtx and --portable-evtx-executable are mutually exclusive. Select one portable EVTX engine.");
        }
        bool hasDefinition = options.Get("definition") != null;
        bool hasTypes = options.GetMany("type").Length > 0;
        bool hasPreset = options.Get("preset") != null;
        bool hasPaths = options.GetMany("path").Length > 0;
        bool hasLog = options.Get("log") != null;
        int logicalDefinitions = new[] { hasDefinition, hasTypes, hasPreset, hasLog }.Count(static value => value);
        if (logicalDefinitions > 1 || logicalDefinitions == 0 && !hasPaths || hasLog && hasPaths) {
            throw new ArgumentException("query, report, and measure require one of --preset, --type, --definition, --log, or standalone --path; --path may accompany a typed selection.");
        }
        EventReportRequest request;
        EventMonitoringPresetDefinition? preset = ParsePreset(options.Get("preset"));
        if (options.Get("definition") is string path) {
            request = EventReportRequest.ForDefinition(EventDefinition.Load(path));
        } else if (preset != null) {
            request = EventReportRequest.ForTypes(preset.Types.ToArray());
        } else if (options.GetMany("type").Length > 0) {
            request = EventReportRequest.ForTypes(ParseTypes(options.GetMany("type")));
        } else if (options.GetMany("path").Length > 0) {
            request = EventReportRequest.ForFiles(options.GetMany("path"));
        } else {
            request = EventReportRequest.ForLog(options.Require("log"));
        }
        if (hasPaths && (hasTypes || hasDefinition || hasPreset)) {
            request.Paths = options.GetMany("path");
        }
        if (options.Has("portable-evtx") || options.Get("portable-evtx-executable") != null) {
            if (!hasPaths) {
                throw new ArgumentException("--portable-evtx requires at least one --path source.");
            }
            request.SavedEventReader = options.Get("portable-evtx-executable") is string executable
                ? new EvtxDumpSavedEventReader(executable)
                : new EvtxSavedEventReader();
            request.SavedEventDiagnosticHandler = WriteSavedEventDiagnostic;
        }
        request.EventIds = ParseInts(options.GetMany("event-id"));
        request.RecordIds = ParseLongs(options.GetMany("record-id"));
        request.MachineNames = NullWhenEmpty(options.GetMany("machine"));
        request.Collectors = NullWhenEmpty(options.GetMany("collector"));
        request.StartTime = ParseDate(options.Get("start"));
        request.EndTime = ParseDate(options.Get("end"));
        if (options.Get("since") is string since) {
            request.StartTime = DateTime.Now.Subtract(TimeSpan.Parse(since, CultureInfo.InvariantCulture));
        }
        request.MaxEvents = options.GetLong("max");
        request.MaxCandidates = options.GetLong("max-candidates");
        request.MaxConcurrency = options.GetInt("concurrency", 8);
        request.Oldest = options.Has("oldest");
        request.ResolveDns = options.Has("resolve-dns");
        request.Title = options.Get("title");
        request.Predicate = CombinePredicates(preset?.Predicate, ParsePredicate(options.Get("where")));
        return request;
    }

    private static T ParseEnum<T>(string? value, T fallback, string option) where T : struct, Enum =>
        string.IsNullOrWhiteSpace(value)
            ? fallback
            : Enum.TryParse(value, ignoreCase: true, out T parsed) && Enum.IsDefined(parsed)
                ? parsed
                : throw new ArgumentException($"{option} has an unsupported value '{value}'.");

    private static EventMonitoringPresetDefinition? ParsePreset(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }
        if (!Enum.TryParse(value, ignoreCase: true, out EventMonitoringPreset preset) || !Enum.IsDefined(preset)) {
            throw new ArgumentException($"Unknown monitoring preset '{value}'.");
        }
        return EventMonitoringPresetCatalog.Get(preset);
    }

    private static EventPredicate? CombinePredicates(EventPredicate? first, EventPredicate? second) =>
        first == null ? second : second == null ? first.Clone() : EventPredicate.AllOf(first.Clone(), second);

    private static EventPredicate? ParsePredicate(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }
        EventPredicate predicate = File.Exists(value)
            ? EventPredicate.Load(value)
            : EventPredicate.ParseJson(value);
        predicate.Validate();
        return predicate;
    }

    private static MonitoringRecordDrawerPlacement ParseDrawerPlacement(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return MonitoringRecordDrawerPlacement.Auto;
        }
        return Enum.TryParse(value, ignoreCase: true, out MonitoringRecordDrawerPlacement placement) &&
               Enum.IsDefined(typeof(MonitoringRecordDrawerPlacement), placement)
            ? placement
            : throw new ArgumentException("--drawer-placement must be Auto, Top, or Right.");
    }

    private static int Collector(CliArguments options) {
        if (options.Subcommand == "remove") {
            return WriteJson(CollectorSubscriptionManager.RemoveCollectorSubscription(options.Require("name")));
        }
        if (options.Subcommand == "readiness") {
            return WriteJson(CollectorSubscriptionManager.GetCollectorReadiness());
        }
        if (options.Subcommand == "runtime") {
            return WriteJson(CollectorSubscriptionManager.GetCollectorSubscriptionRuntimeStatus(options.Require("name")));
        }
        if (options.Subcommand == "initialize") {
            return WriteJson(CollectorSubscriptionManager.InitializeCollector(!options.Has("skip-winrm")));
        }
        if (options.Subcommand != "create") {
            throw new ArgumentException("collector supports create, remove, readiness, runtime, and initialize.");
        }
        EventType[] types = ParseTypes(options.GetMany("type"));
        if (types.Length == 0) {
            throw new ArgumentException("--type is required.");
        }
        string[] computers = options.GetMany("source");
        bool sourceInitiated = options.Has("source-initiated");
        if (!sourceInitiated && computers.Length == 0) {
            throw new ArgumentException("--source is required.");
        }
        if (sourceInitiated && computers.Length > 0) {
            throw new ArgumentException("--source cannot be used with --source-initiated; authorize source SIDs with --allowed-source-sddl.");
        }
        CollectorSubscriptionDeliveryMode deliveryMode = options.Get("delivery") is string delivery
            ? Enum.TryParse(delivery, true, out CollectorSubscriptionDeliveryMode parsedDelivery)
                ? parsedDelivery
                : throw new ArgumentException("--delivery must be Pull or Push.")
            : sourceInitiated ? CollectorSubscriptionDeliveryMode.Push : CollectorSubscriptionDeliveryMode.Pull;
        var definition = new CollectorSubscriptionDefinition {
            SubscriptionId = options.Require("name"),
            Description = options.Get("description") ?? $"EventViewerX {string.Join(", ", types.Select(static type => type.ToString()))}",
            Enabled = !options.Has("disabled"),
            SubscriptionType = sourceInitiated
                ? CollectorSubscriptionType.SourceInitiated
                : CollectorSubscriptionType.CollectorInitiated,
            QueryXml = EventDefinitionCompiler.BuildQueryXml(types),
            Sources = computers.Select(static computer => new CollectorSubscriptionSource(computer)).ToArray(),
            ReadExistingEvents = options.Has("read-existing"),
            DeliveryMode = deliveryMode,
            CollectorHostName = options.Get("collector-host"),
            AllowedSourceDomainComputersSddl = options.Get("allowed-source-sddl") ?? "O:NSG:NSD:(A;;GA;;;DC)(A;;GA;;;NS)",
            SourceRefreshIntervalSeconds = options.GetInt("source-refresh", 60)
        };
        definition.Validate();
        if (options.Get("output") is string output) {
            Console.WriteLine(CollectorSubscriptionManager.WriteCollectorSubscriptionDefinition(definition, output, options.Has("force")).FullName);
        }
        if (options.Has("apply")) {
            Console.WriteLine(JsonSerializer.Serialize(CollectorSubscriptionManager.ApplyCollectorSubscription(definition), JsonOptions));
        }
        if (!options.Has("apply") && options.Get("output") == null) {
            Console.WriteLine(definition.ToXml());
        }
        return 0;
    }

    private static int Provider(CliArguments options) {
        return options.Subcommand switch {
            "build" => ProviderBuild(options),
            "install" => WriteJson(EventProviderPackageManager.Install(options.Require("package"))),
            "uninstall" => WriteJson(EventProviderPackageManager.Uninstall(options.Require("name"), options.Has("remove-files"))),
            _ => throw new ArgumentException("provider supports build, install, and uninstall.")
        };
    }

    private static int ProviderBuild(CliArguments options) {
        EventProviderDefinition definition = EventProviderDefinitionJson.Load(options.Require("definition"));
        EventProviderPackageBuildResult result = EventProviderPackageBuilder.Build(definition, options.Require("output"),
            new EventProviderPackageBuildOptions { Overwrite = options.Has("force"), BaselinePath = options.Get("baseline") ?? string.Empty });
        return WriteJson(result);
    }

    private static int ListTypes(CliArguments options) {
        if (options.Get("definition") is string definitionPath) {
            if (options.Has("type")) {
                throw new ArgumentException("types accepts either --type or --definition, not both.");
            }
            EventDefinition custom = EventDefinition.Load(definitionPath);
            EventPredicateBuilder customBuilder = EventPredicateBuilder.ForDefinition(custom);
            return WriteJson(new {
                custom.Name,
                custom.DisplayName,
                custom.Description,
                custom.Category,
                IsComposite = false,
                Sources = custom.Sources.Select(static source => new {
                    source.LogName,
                    source.EventIds,
                    source.ProviderNames
                }),
                Fields = DescribeFields(customBuilder.Fields)
            });
        }
        EventType[] selected = ParseTypes(options.GetMany("type"));
        IEnumerable<EventTypeDefinition> definitions = selected.Length == 0
            ? EventTypeCatalog.GetDefinitions()
            : selected.Select(EventTypeCatalog.GetDefinition);
        foreach (EventTypeDefinition definition in definitions) {
            EventPredicateBuilder builder = EventPredicateBuilder.ForType(definition.Type);
            Console.WriteLine(JsonSerializer.Serialize(new {
                definition.Name,
                definition.DisplayName,
                definition.Description,
                definition.Category,
                definition.IsComposite,
                Sources = definition.Sources.Select(static source => new { source.LogName, source.EventIds }),
                Fields = DescribeFields(builder.Fields)
            }, JsonOptions));
        }
        return 0;
    }

    private static object[] DescribeFields(IReadOnlyList<EventPredicateField> fields) => fields
        .Select(static field => (object)new {
            field.Name,
            field.DisplayName,
            field.Definition.Description,
            ValueType = field.Definition.ValueType.FullName,
            field.Definition.IsCommon,
            field.Definition.IsFilterable,
            field.Definition.Aliases,
            field.Definition.FilterStage,
            field.Definition.SupportedOperators
        })
        .ToArray();

    private static int WriteJson<T>(T value) {
        Console.WriteLine(JsonSerializer.Serialize(value, JsonOptions));
        return 0;
    }

    private static int WriteRows(EventReport report) {
        var sectionsByRow = new Dictionary<EventReportRow, EventReportSection>();
        foreach (EventReportSection section in report.Sections) {
            foreach (EventReportRow row in section.Rows) {
                sectionsByRow[row] = section;
            }
        }
        foreach (EventReportRow row in report.Rows) {
            sectionsByRow.TryGetValue(
                row,
                out EventReportSection? section);
            IReadOnlyDictionary<string, object?> output =
                EventReportJsonProjection.Project(row, section);
            Console.WriteLine(JsonSerializer.Serialize(output, JsonOptions));
        }
        return 0;
    }

    private static EventType[] ParseTypes(IEnumerable<string> values) => values.Select(value =>
        Enum.TryParse(value, ignoreCase: true, out EventType parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new ArgumentException($"Unknown event type '{value}'. Use 'evx types' to list definitions."))
        .Distinct().ToArray();
    private static int[]? ParseInts(string[] values) => values.Length == 0 ? null : values.Select(value => int.Parse(value, CultureInfo.InvariantCulture)).ToArray();
    private static long[]? ParseLongs(string[] values) => values.Length == 0 ? null : values.Select(value => long.Parse(value, CultureInfo.InvariantCulture)).ToArray();
    private static string[]? NullWhenEmpty(string[] values) => values.Length == 0 ? null : values;
    private static DateTime? ParseDate(string? value) => value == null ? null : DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);

    private static void WriteSavedEventDiagnostic(SavedEventReadDiagnostic diagnostic) {
        string offset = diagnostic.FileOffset.HasValue
            ? $" offset=0x{diagnostic.FileOffset.Value:X}"
            : string.Empty;
        Console.Error.WriteLine($"{diagnostic.Severity} {diagnostic.Code}{offset}: {diagnostic.Message}");
    }

    private static void ValidateOptions(CliArguments options) {
        if (options.Subcommand.Length > 0 && options.Command is not ("collector" or "provider" or "store")) {
            throw new ArgumentException(
                $"Unexpected argument '{options.Subcommand}'. The {options.Command} command does not accept a subcommand.");
        }

        switch (options.Command) {
            case "query":
                options.ValidateAllowed(
                    "preset", "type", "definition", "definition-name", "log", "path", "event-id", "record-id",
                    "machine", "collector", "source", "provider", "start", "end", "since", "max",
                    "max-candidates", "concurrency", "oldest", "portable-evtx", "portable-evtx-executable", "resolve-dns", "title", "where", "explain",
                    "store", "write-store", "checkpoint", "context-store", "context-authorization",
                    "duplicates", "occurrence-window", "maximum-occurrence-observations", "maximum-occurrence-groups");
                break;
            case "report":
                options.ValidateAllowed(
                    "preset", "type", "definition", "definition-name", "log", "path", "event-id", "record-id",
                    "machine", "collector", "source", "provider", "start", "end", "since", "max",
                    "max-candidates", "concurrency", "oldest", "portable-evtx", "portable-evtx-executable", "resolve-dns", "title",
                    "html", "excel", "csv", "email-html", "mail-profile", "email-rows", "drawer-placement", "where",
                    "store", "write-store", "summary", "context-store", "context-authorization",
                    "duplicates", "occurrence-window", "maximum-occurrence-observations", "maximum-occurrence-groups");
                break;
            case "measure":
                // occurrence options are applied before aggregation and retain one deterministic representative per group.
                options.ValidateAllowed(
                    "preset", "type", "definition", "definition-name", "log", "path", "event-id", "record-id",
                    "machine", "collector", "source", "provider", "start", "end", "since", "max",
                    "max-candidates", "concurrency", "oldest", "portable-evtx", "portable-evtx-executable", "resolve-dns", "title", "where", "store", "explain",
                    "group-by", "bucket", "timezone", "measure", "top", "top-scope", "ranking-measure",
                    "window-start", "window-end", "maximum-groups", "maximum-distinct", "maximum-state-bytes",
                    "html", "excel", "csv", "context-store", "context-authorization",
                    "duplicates", "occurrence-window", "maximum-occurrence-observations", "maximum-occurrence-groups");
                break;
            case "watch":
                options.ValidateAllowed(
                    "type", "definition", "machine", "collector", "jsonl", "outbox",
                    "mail-profile", "interval", "stop-after", "timeout", "ready-file",
                    "summary-file", "title", "notification-buffer-capacity", "delivery-queue-capacity",
                    "dead-letter-after", "retry-delay", "maximum-retry-delay", "checkpoint-store",
                    "checkpoint-consumer", "ignore-stale-bookmark", "outbox-maximum-batch-bytes",
                    "outbox-maximum-bytes", "outbox-maximum-pending-batches");
                break;
            case "detect":
                options.ValidateAllowed(
                    "type", "log", "path", "machine", "collector", "start", "end", "since", "max",
                    "event-id", "provider", "portable-evtx", "portable-evtx-executable", "sigma", "pack", "include-built-in", "tuning", "explain", "dry-run",
                    "test-fixtures", "pack-coverage",
                    "maximum-observations", "maximum-groups", "maximum-state-observations", "maximum-state-bytes",
                    "write-findings-store", "jsonl", "report-html", "report-csv", "report-excel", "report-kind", "title",
                    "store", "coverage", "trace-jsonl");
                break;
            case "collector" when options.Subcommand == "create":
                options.ValidateAllowed(
                    "name", "source", "type", "description", "disabled", "read-existing",
                    "output", "force", "apply", "source-initiated", "allowed-source-sddl",
                    "delivery", "collector-host", "source-refresh");
                break;
            case "collector" when options.Subcommand == "remove":
            case "collector" when options.Subcommand == "runtime":
                options.ValidateAllowed("name");
                break;
            case "collector" when options.Subcommand == "readiness":
                options.ValidateAllowed();
                break;
            case "collector" when options.Subcommand == "initialize":
                options.ValidateAllowed("skip-winrm");
                break;
            case "provider" when options.Subcommand == "build":
                options.ValidateAllowed("definition", "output", "force", "baseline");
                break;
            case "provider" when options.Subcommand == "install":
                options.ValidateAllowed("package");
                break;
            case "provider" when options.Subcommand == "uninstall":
                options.ValidateAllowed("name", "remove-files");
                break;
            case "store" when options.Subcommand == "prune":
                options.ValidateAllowed("path", "before", "definition-name");
                break;
            case "store" when options.Subcommand == "integrity":
                options.ValidateAllowed("path");
                break;
            case "store" when options.Subcommand == "backup":
                options.ValidateAllowed("path", "output", "force");
                break;
            case "store" when options.Subcommand == "restore":
                options.ValidateAllowed("path", "backup");
                break;
            case "store" when options.Subcommand == "retention":
                options.ValidateAllowed("path", "events-for", "findings-for", "vacuum");
                break;
            case "store" when options.Subcommand == "reset-checkpoint":
                options.ValidateAllowed("path", "consumer", "computer", "container");
                break;
            case "types":
                options.ValidateAllowed("type", "definition");
                break;
            case "schemas":
                options.ValidateAllowed();
                break;
            case "version":
            case "--version":
            case "-v":
            case "help":
            case "--help":
            case "-h":
                options.ValidateAllowed();
                break;
        }
    }

    private static int Help() {
        Console.WriteLine($"EventViewerX {GetVersion()}\n\n" +
            "  evx --version\n" +
            "  evx types [--type TYPE[,TYPE] | --definition FILE]\n" +
            "  evx schemas\n" +
            "  evx query  (--type TYPE[,TYPE] | --definition FILE | --log LOG | --path FILE[,FILE] | --store FILE.db [--type TYPE[,TYPE] | --definition FILE | --definition-name NAME]) [--portable-evtx | --portable-evtx-executable FILE with --path] [--context-store CONTEXT.db with --type GroupPolicyDirectoryAudit] [--where JSON_OR_FILE (typed/store)] [--write-store FILE.db [--checkpoint NAME]] [--explain] [--since 01:00:00] [--max N]\n" +
            "  evx report (--type TYPE[,TYPE] | --definition FILE | --log LOG | --path FILE[,FILE] | --store FILE.db [--type TYPE[,TYPE] | --definition FILE | --definition-name NAME]) [--portable-evtx | --portable-evtx-executable FILE with --path] [--summary Hour|Day|Week|Month] [--where JSON_OR_FILE (typed/store)] [--write-store FILE.db] (--html FILE | --excel FILE | --csv FILE.csv|BUNDLE.zip | --email-html FILE | --mail-profile FILE) [--drawer-placement Auto|Top|Right]\n" +
            "  evx measure (--preset PRESET | --type TYPE[,TYPE] | --definition FILE | --log LOG | --path FILE[,FILE] | --store FILE.db) [--portable-evtx | --portable-evtx-executable FILE with --path] [--group-by FIELD[,FIELD]] [--bucket Hour|Day|Week|Month] [--measure OPERATION:FIELD:NAME:RATE_UNIT] [--top N] [--html FILE | --excel FILE | --csv FILE] [--explain]\n" +
            "  evx detect (--store FILE.db | --type TYPE[,TYPE] | --log LOG | --path FILE[,FILE]) [--coverage FILE with --store] [--portable-evtx | --portable-evtx-executable FILE with --path] [--sigma FILE[,FILE] | --pack FILE[,FILE]] [--include-built-in] [--tuning FILE] [--write-findings-store FILE.db] [--jsonl FILE] [--trace-jsonl FILE] [--report-kind KIND] [--report-html FILE | --report-csv FILE | --report-excel FILE] [--explain | --dry-run]\n" +
            "  evx detect --test-fixtures\n" +
            "  evx detect --pack-coverage [--pack FILE[,FILE]] [--include-built-in]\n" +
            "  evx watch  (--type TYPE[,TYPE] | --definition FILE) [--machine HOST | --collector WEC] [--checkpoint-store FILE.db] [--checkpoint-consumer NAME] [--ignore-stale-bookmark] [--jsonl FILE] [--outbox DIR | --mail-profile FILE] [--interval 00:05:00] [--delivery-queue-capacity N] [--notification-buffer-capacity N] [--outbox-maximum-batch-bytes N] [--outbox-maximum-bytes N] [--outbox-maximum-pending-batches N] [--dead-letter-after N] [--retry-delay 00:01:00] [--maximum-retry-delay 01:00:00] [--stop-after N] [--timeout 01:00:00] [--ready-file FILE] [--summary-file FILE]\n" +
            "  evx collector create --name NAME --type TYPE[,TYPE] (--source HOST[,HOST] | --source-initiated --collector-host WEC) [--allowed-source-sddl SDDL] [--output FILE] [--apply]\n" +
            "  evx collector readiness\n" +
            "  evx collector runtime --name NAME\n" +
            "  evx collector initialize [--skip-winrm]\n" +
            "  evx collector remove --name NAME\n" +
            "  evx store prune --path FILE.db --before TIMESTAMP [--definition-name NAME]\n" +
            "  evx store integrity --path FILE.db\n" +
            "  evx store backup --path FILE.db --output BACKUP.db [--force]\n" +
            "  evx store restore --path FILE.db --backup BACKUP.db\n" +
            "  evx store retention --path FILE.db [--events-for 30.00:00:00] [--findings-for 90.00:00:00] [--vacuum]\n" +
            "  evx store reset-checkpoint --path FILE.db --consumer NAME --computer HOST --container LOG\n" +
            "  evx provider build --definition FILE --output FILE.evxprovider\n" +
            "  evx provider install --package FILE.evxprovider\n" +
            "  evx provider uninstall --name PROVIDER [--remove-files]");
        return 0;
    }
}
