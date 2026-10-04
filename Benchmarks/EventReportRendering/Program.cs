using System.Diagnostics;
using System.Text;
using System.Text.Json;
using EventViewerX.Reporting;

int count = int.Parse(args[0], System.Globalization.CultureInfo.InvariantCulture);
string format = args[1];
string scenario = args.Length > 3 ? args[3] : "Complete";
if (count < 0 || format is not ("Html" or "Email" or "Aggregation") || scenario is not ("Complete" or "Capped" or "Failed")) {
    throw new ArgumentException("Specify a nonnegative row count, Html/Email/Aggregation, and a supported scenario.");
}
var rows = Enumerable.Range(0, count).Select(index => new EventReportRow {
    Type = "Generic", RecordId = index + 1, EventId = 4625,
    TimeCreated = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(index),
    SourceComputer = "host-" + index % 10, SourceLog = "Security", Provider = "fixture",
    Message = "message-" + index, Level = "Warning", LevelValue = 3,
    Values = new Dictionary<string, object?> { ["User"] = "user-" + index, ["Count"] = index % 7 }
}).ToArray();
var coverage = new[] { new EventReportCoverage {
    MachineName = "fixture-host", LogName = "Security", Succeeded = scenario != "Failed",
    Status = scenario == "Failed" ? "Failed" : "Succeeded", Detail = scenario == "Failed" ? "Fixture access denied" : "Fixture complete"
} };
EventReport report = EventReportEngine.CreateStored(rows, new[] { EventReportSectionSchema.CreateGeneric() }, "Rendering fixture",
    coverage: args.Length > 3 ? coverage : null, scanLimitReached: scenario == "Capped");

string Render() => format switch {
    "Html" => EventReportHtmlRenderer.Render(report),
    "Aggregation" => EventAggregationHtmlRenderer.Render(EventAggregationEngine.Aggregate(report, new EventAggregationDefinition { GroupBy = new[] { "SourceComputer" } })),
    _ => EventReportEmailRenderer.RenderAsync(report, maximumRows: 25).GetAwaiter().GetResult().Html
};
_ = Render(); // Warm dependencies and renderer caches before the measured operation.
GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
long before = GC.GetTotalAllocatedBytes(precise: true);
var watch = Stopwatch.StartNew();
string html = Render();
watch.Stop();
long allocated = GC.GetTotalAllocatedBytes(precise: true) - before;
if (args.Length > 2) { File.WriteAllText(args[2], html, new UTF8Encoding(false)); }
Console.WriteLine(JsonSerializer.Serialize(new {
    Rows = report.Rows.Count, Format = format, RenderMs = watch.Elapsed.TotalMilliseconds,
    AllocatedBytes = allocated, OutputBytes = Encoding.UTF8.GetByteCount(html),
    HasFirst = html.Contains("message-0", StringComparison.Ordinal),
    HasLast = html.Contains("message-" + (count - 1), StringComparison.Ordinal),
    HasLimitRow = html.Contains("message-25", StringComparison.Ordinal)
}));
