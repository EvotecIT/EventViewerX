using System.Globalization;
using EventViewerX.Reporting;
using EventViewerX.Storage;

namespace EventViewerX.Cli;

internal static partial class Program {
    private static async Task<int> MeasureAsync(CliArguments options) {
        ValidateQuerySource(options, allowSummary: false);
        ValidateOccurrenceOptions(options);
        EventAggregationDefinition definition = CreateAggregationDefinition(options);
        EventAggregationResult result;
        if (options.Get("store") is string storePath) {
            EventStoreQuery query = CreateStoreQuery(options);
            EventDuplicateMode duplicateMode = ParseEnum(
                options.Get("duplicates"),
                EventDuplicateMode.None,
                "--duplicates");
            if (duplicateMode != EventDuplicateMode.None) {
                if (options.Has("explain")) {
                    _ = EventAggregationEngine.CreateAccumulator(definition);
                    return WriteJson(new {
                        ExecutionMode = EventAggregationExecutionMode.Managed,
                        Reason = "Occurrence grouping must retain source observations before aggregation, so stored pushdown is disabled."
                    });
                }
                EventReport stored = await new EventStore(storePath)
                    .ReadReportAsync(query, options.Get("title"))
                    .ConfigureAwait(false);
                result = AggregateOccurrences(stored, definition, options);
            } else if (options.Has("explain")) {
                EventStoreAggregationPlan plan = await new EventStore(storePath)
                    .PlanAggregationAsync(query, definition)
                    .ConfigureAwait(false);
                return WriteJson(plan);
            } else {
                result = await new EventStore(storePath)
                    .AggregateAsync(query, definition)
                    .ConfigureAwait(false);
            }
        } else {
            if (options.Has("explain")) {
                throw new ArgumentException("measure --explain is available for --store execution planning.");
            }
            EventReport report = options.Get("context-store") != null
                ? await QueryGroupPolicyReportAsync(options).ConfigureAwait(false)
                : await EventReportEngine.QueryAsync(CreateRequest(options)).ConfigureAwait(false);
            result = ParseEnum(
                options.Get("duplicates"),
                EventDuplicateMode.None,
                "--duplicates") == EventDuplicateMode.None
                ? EventAggregationEngine.Aggregate(report, definition)
                : AggregateOccurrences(report, definition, options);
        }
        bool written = false;
        if (options.Get("html") is string html) {
            Console.WriteLine(EventAggregationHtmlRenderer.Save(result, html, options.Get("title")));
            written = true;
        }
        if (options.Get("excel") is string excel) {
            Console.WriteLine(EventAggregationExcelRenderer.Save(result, excel, options.Get("title")));
            written = true;
        }
        if (options.Get("csv") is string csv) {
            EventReport aggregationReport = EventAggregationReportFactory.Create(result, options.Get("title"));
            Console.WriteLine(EventReportCsvRenderer.Save(aggregationReport, csv));
            written = true;
        }
        return written ? 0 : WriteJson(result);
    }

    private static Task<EventReport> QueryGroupPolicyReportAsync(CliArguments options) {
        var query = new GroupPolicyAuditQuery {
            ContextStore = new SqliteEventContextStore(options.Require("context-store")),
            AuthorizationContext = options.Get("context-authorization"),
            Paths = NullWhenEmpty(options.GetMany("path")),
            MachineNames = NullWhenEmpty(options.GetMany("collector")) ?? NullWhenEmpty(options.GetMany("machine")),
            CollectorLogName = options.GetMany("collector").Length == 0 ? null : "ForwardedEvents",
            StartTime = ParseDate(options.Get("start")),
            EndTime = ParseDate(options.Get("end")),
            MaxEvents = options.GetLong("max"),
            MaxCandidates = options.GetLong("max-candidates"),
            MaxConcurrency = options.GetInt("concurrency", 8),
            Oldest = true
        };
        if (options.Get("since") is string since) {
            query.StartTime = DateTime.Now.Subtract(TimeSpan.Parse(since, CultureInfo.InvariantCulture));
        }
        return GroupPolicyAuditReportEngine.QueryAsync(query, options.Get("title"));
    }

    private static EventReport ApplyOccurrenceGrouping(EventReport report, CliArguments options) {
        EventDuplicateMode mode = ParseEnum(
            options.Get("duplicates"),
            EventDuplicateMode.None,
            "--duplicates");
        if (mode == EventDuplicateMode.None) {
            return report;
        }
        EventOccurrenceResult result = GroupOccurrences(report, options);
        return EventOccurrenceReportFactory.Create(result, report, options.Get("title"));
    }

    private static EventAggregationResult AggregateOccurrences(
        EventReport report,
        EventAggregationDefinition definition,
        CliArguments options) {

        EventOccurrenceResult occurrences = GroupOccurrences(report, options);
        EventReport representatives = EventOccurrenceReportFactory.CreateRepresentatives(
            occurrences,
            report,
            options.Get("title"));
        return EventAggregationEngine.Aggregate(representatives, definition);
    }

    private static EventOccurrenceResult GroupOccurrences(EventReport report, CliArguments options) {
        return EventOccurrenceEngine.Group(report.Rows, CreateOccurrenceOptions(options));
    }

    private static void ValidateOccurrenceOptions(CliArguments options) =>
        EventOccurrenceEngine.ValidateOptions(CreateOccurrenceOptions(options));

    private static EventOccurrenceOptions CreateOccurrenceOptions(CliArguments options) =>
        new() {
            Mode = ParseEnum(
                options.Get("duplicates"),
                EventDuplicateMode.None,
                "--duplicates"),
            Window = options.Get("occurrence-window") is string window
                ? TimeSpan.Parse(window, CultureInfo.InvariantCulture)
                : TimeSpan.FromSeconds(10),
            MaximumObservations = options.GetInt("maximum-occurrence-observations", 100000),
            MaximumGroups = options.GetInt("maximum-occurrence-groups", 25000)
        };

    private static EventAggregationDefinition CreateAggregationDefinition(CliArguments options) {
        EventAggregationMeasure[] measures = options.GetMany("measure")
            .Select(ParseAggregationMeasure)
            .ToArray();
        return new EventAggregationDefinition {
            GroupBy = options.GetMany("group-by"),
            Bucket = ParseEnum(options.Get("bucket"), EventAggregationBucket.None, "--bucket"),
            TimeZoneId = options.Get("timezone") ?? "UTC",
            Measures = measures.Length == 0
                ? new[] { new EventAggregationMeasure { Operation = EventAggregationOperation.Count, OutputName = "Count" } }
                : measures,
            Top = options.GetInt("top"),
            TopScope = ParseEnum(options.Get("top-scope"), EventAggregationTopScope.GlobalGroup, "--top-scope"),
            RankingMeasure = options.Get("ranking-measure"),
            WindowStart = ParseDate(options.Get("window-start")),
            WindowEnd = ParseDate(options.Get("window-end")),
            MaximumGroups = options.GetInt("maximum-groups", 25000),
            MaximumDistinctValues = options.GetInt("maximum-distinct", 100000),
            MaximumStateBytes = options.GetLong("maximum-state-bytes", 64L * 1024L * 1024L)
        };
    }

    private static EventAggregationMeasure ParseAggregationMeasure(string value) {
        string[] parts = value.Split(new[] { ':' }, 4);
        if (!Enum.TryParse(parts[0], ignoreCase: true, out EventAggregationOperation operation) || !Enum.IsDefined(operation)) {
            throw new ArgumentException($"Unknown aggregation operation '{parts[0]}'.");
        }
        return new EventAggregationMeasure {
            Operation = operation,
            Field = parts.Length > 1 && parts[1].Length > 0 ? parts[1] : null,
            OutputName = parts.Length > 2 && parts[2].Length > 0 ? parts[2] : null,
            RateUnit = parts.Length > 3 && parts[3].Length > 0
                ? TimeSpan.Parse(parts[3], CultureInfo.InvariantCulture)
                : operation == EventAggregationOperation.Rate ? TimeSpan.FromHours(1) : null
        };
    }
}
