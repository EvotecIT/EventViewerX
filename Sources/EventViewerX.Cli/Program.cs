using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EventViewerX.Providers;
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

    private static async Task<int> Main(string[] args) {
        try {
            var options = new CliArguments(args);
            ValidateOptions(options);
            return options.Command switch {
                "query" => await QueryAsync(options).ConfigureAwait(false),
                "report" => await ReportAsync(options).ConfigureAwait(false),
                "watch" => await WatchAsync(options).ConfigureAwait(false),
                "store" => await StoreAsync(options).ConfigureAwait(false),
                "collector" => Collector(options),
                "provider" => Provider(options),
                "types" => ListTypes(options),
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
            return WriteRows(stored);
        }
        EventReportRequest request = CreateRequest(options);
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
        await WriteStoreIfRequestedAsync(report, options).ConfigureAwait(false);
        return WriteRows(report);
    }

    private static async Task<int> ReportAsync(CliArguments options) {
        ValidateQuerySource(options, allowSummary: true);
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
            report = await EventReportEngine.QueryAsync(CreateRequest(options)).ConfigureAwait(false);
            await WriteStoreIfRequestedAsync(report, options).ConfigureAwait(false);
        }
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
        if (options.Subcommand != "prune") {
            throw new ArgumentException("store supports prune. Use query/report --store for reading and --write-store for ingestion.");
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
        EventDefinition? definition = options.Get("definition") is string definitionPath
            ? EventDefinition.Load(definitionPath)
            : null;
        EventPredicate? predicate = ParsePredicate(options.Get("where"));
        if (predicate != null) {
            predicate = definition != null
                ? EventPredicateBuilder.ForDefinition(definition).Normalize(predicate)
                : types.Length > 0
                    ? EventPredicateBuilder.ForTypes(types).Normalize(predicate)
                    : predicate;
        }
        return new EventStoreQuery {
            Types = types.Length == 0 ? null : types,
            DefinitionNames = definition == null
                ? NullWhenEmpty(options.GetMany("definition-name"))
                : new[] { definition.Name },
            DefinitionSchemas = definition == null
                ? null
                : new[] { EventReportSectionSchema.FromDefinition(definition) },
            StartTime = start,
            EndTime = ParseDate(options.Get("end")),
            EventIds = ParseInts(options.GetMany("event-id")),
            RecordIds = ParseLongs(options.GetMany("record-id")),
            SourceComputers = NullWhenEmpty(options.GetMany("source")),
            SourceLogs = NullWhenEmpty(options.GetMany("log")),
            Providers = NullWhenEmpty(options.GetMany("provider")),
            Predicate = predicate,
            MaxEvents = options.GetLong("max"),
            MaxCandidates = options.GetLong("max-candidates", 100000),
            Oldest = options.Has("oldest")
        };
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
        if (stored && options.Get("write-store") != null) {
            throw new ArgumentException("--write-store is only valid for live or offline event-log ingestion.");
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
        bool typedSource = options.Get("definition") != null || options.GetMany("type").Length > 0;
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
    }

    private static EventReportRequest CreateRequest(CliArguments options) {
        bool hasDefinition = options.Get("definition") != null;
        bool hasTypes = options.GetMany("type").Length > 0;
        bool hasPaths = options.GetMany("path").Length > 0;
        bool hasLog = options.Get("log") != null;
        int logicalDefinitions = new[] { hasDefinition, hasTypes, hasLog }.Count(static value => value);
        if (logicalDefinitions > 1 || logicalDefinitions == 0 && !hasPaths || hasLog && hasPaths) {
            throw new ArgumentException("query and report require one of --type, --definition, --log, or standalone --path; --path may accompany --type or --definition.");
        }
        EventReportRequest request;
        if (options.Get("definition") is string path) {
            request = EventReportRequest.ForDefinition(EventDefinition.Load(path));
        } else if (options.GetMany("type").Length > 0) {
            request = EventReportRequest.ForTypes(ParseTypes(options.GetMany("type")));
        } else if (options.GetMany("path").Length > 0) {
            request = EventReportRequest.ForFiles(options.GetMany("path"));
        } else {
            request = EventReportRequest.ForLog(options.Require("log"));
        }
        if (hasPaths && (hasTypes || hasDefinition)) {
            request.Paths = options.GetMany("path");
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
        request.Predicate = ParsePredicate(options.Get("where"));
        return request;
    }

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
            IReadOnlyDictionary<string, object?> output = sectionsByRow.TryGetValue(
                row,
                out EventReportSection? section)
                ? row.ToDictionary(section)
                : row.ToDictionary();
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

    private static void ValidateOptions(CliArguments options) {
        if (options.Subcommand.Length > 0 && options.Command is not ("collector" or "provider" or "store")) {
            throw new ArgumentException(
                $"Unexpected argument '{options.Subcommand}'. The {options.Command} command does not accept a subcommand.");
        }

        switch (options.Command) {
            case "query":
                options.ValidateAllowed(
                    "type", "definition", "definition-name", "log", "path", "event-id", "record-id",
                    "machine", "collector", "source", "provider", "start", "end", "since", "max",
                    "max-candidates", "concurrency", "oldest", "resolve-dns", "title", "where", "explain",
                    "store", "write-store");
                break;
            case "report":
                options.ValidateAllowed(
                    "type", "definition", "definition-name", "log", "path", "event-id", "record-id",
                    "machine", "collector", "source", "provider", "start", "end", "since", "max",
                    "max-candidates", "concurrency", "oldest", "resolve-dns", "title",
                    "html", "excel", "csv", "email-html", "mail-profile", "email-rows", "drawer-placement", "where",
                    "store", "write-store", "summary");
                break;
            case "watch":
                options.ValidateAllowed(
                    "type", "definition", "machine", "collector", "jsonl", "outbox",
                    "mail-profile", "interval", "stop-after", "timeout", "ready-file",
                    "summary-file", "title");
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
            case "types":
                options.ValidateAllowed("type", "definition");
                break;
            case "help":
            case "--help":
            case "-h":
                options.ValidateAllowed();
                break;
        }
    }

    private static int Help() {
        Console.WriteLine("EventViewerX 4.0\n\n" +
            "  evx types [--type TYPE[,TYPE] | --definition FILE]\n" +
            "  evx query  (--type TYPE[,TYPE] | --definition FILE | --log LOG | --path FILE[,FILE] | --store FILE.db [--type TYPE[,TYPE] | --definition FILE | --definition-name NAME]) [--where JSON_OR_FILE (typed/store)] [--write-store FILE.db] [--explain] [--since 01:00:00] [--max N]\n" +
            "  evx report (--type TYPE[,TYPE] | --definition FILE | --log LOG | --path FILE[,FILE] | --store FILE.db [--type TYPE[,TYPE] | --definition FILE | --definition-name NAME]) [--summary Hour|Day|Week|Month] [--where JSON_OR_FILE (typed/store)] [--write-store FILE.db] (--html FILE | --excel FILE | --csv FILE.csv|BUNDLE.zip | --email-html FILE | --mail-profile FILE) [--drawer-placement Auto|Top|Right]\n" +
            "  evx watch  (--type TYPE[,TYPE] | --definition FILE) [--machine HOST | --collector WEC] [--jsonl FILE] [--outbox DIR | --mail-profile FILE] [--interval 00:05:00] [--stop-after N] [--timeout 01:00:00] [--ready-file FILE] [--summary-file FILE]\n" +
            "  evx collector create --name NAME --type TYPE[,TYPE] (--source HOST[,HOST] | --source-initiated --collector-host WEC) [--allowed-source-sddl SDDL] [--output FILE] [--apply]\n" +
            "  evx collector readiness\n" +
            "  evx collector runtime --name NAME\n" +
            "  evx collector initialize [--skip-winrm]\n" +
            "  evx collector remove --name NAME\n" +
            "  evx store prune --path FILE.db --before TIMESTAMP [--definition-name NAME]\n" +
            "  evx provider build --definition FILE --output FILE.evxprovider\n" +
            "  evx provider install --package FILE.evxprovider\n" +
            "  evx provider uninstall --name PROVIDER [--remove-files]");
        return 0;
    }
}
