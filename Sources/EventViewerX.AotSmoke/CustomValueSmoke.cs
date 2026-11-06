using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using EventViewerX;
using EventViewerX.Reporting;
using EventViewerX.Rules.Windows;

internal static class CustomValueSmoke {
    internal static void Run(EventObject source) {
        var record = new ConsumerBugCheck(source);
        JsonTypeInfo[] contracts = {
            ConsumerJsonContext.Default.ConsumerState,
            ConsumerJsonContext.Default.ConsumerDetails
        };
        EventObservation observation = EventObservation.Create(source, record);
        using JsonDocument json = JsonDocument.Parse(EventAnalysisJson.Serialize(observation, false, contracts));
        JsonElement fields = json.RootElement.GetProperty("fields");
        if (fields.GetProperty("State").GetString() != "Verified" ||
            fields.GetProperty("Details").GetProperty("Code").GetInt32() != 42) {
            throw new InvalidOperationException("The generated caller-owned observation contracts lost their values.");
        }
        EventReport report = EventReportEngine.Create(new[] { record });
        EventOccurrenceResult occurrences = EventOccurrenceEngine.Group(report.Rows,
            new EventOccurrenceOptions { Mode = EventDuplicateMode.Transport });
        EventReport representatives = EventOccurrenceReportFactory.CreateRepresentatives(occurrences, report);
        if (representatives.Rows.Single().Values["State"] is not ConsumerState.Verified ||
            representatives.Sections.Single().Columns.Single(static column => column.Name == "Details").ValueType != typeof(ConsumerDetails)) {
            throw new InvalidOperationException("The native occurrence representatives lost caller-owned column types.");
        }
        using var evidence = new MemoryStream();
        EventEvidenceBundle.Write(report, evidence, null, CancellationToken.None, contracts);
        evidence.Position = 0;
        using var archive = new ZipArchive(evidence, ZipArchiveMode.Read);
        using var reader = new StreamReader(archive.GetEntry("rows.jsonl")!.Open());
        using JsonDocument row = JsonDocument.Parse(reader.ReadLine()!);
        if (row.RootElement.GetProperty("State").GetString() != "Verified" ||
            row.RootElement.GetProperty("Details").GetProperty("Code").GetInt32() != 42) {
            throw new InvalidOperationException("The generated caller-owned evidence contracts lost their values.");
        }
    }
}

internal sealed class ConsumerBugCheck : OSBugCheck {
    internal ConsumerBugCheck(EventObject source) : base(source) { }
    public ConsumerState State => ConsumerState.Verified;
    public ConsumerDetails Details => new() { Code = 42 };
}

internal enum ConsumerState { Verified }

internal sealed class ConsumerDetails {
    public int Code { get; set; }
}

[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
[JsonSerializable(typeof(ConsumerState))]
[JsonSerializable(typeof(ConsumerDetails))]
internal partial class ConsumerJsonContext : JsonSerializerContext { }
