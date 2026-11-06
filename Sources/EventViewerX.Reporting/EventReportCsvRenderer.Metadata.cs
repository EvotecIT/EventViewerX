using System.Security.Cryptography;
using System.Text.Json;

namespace EventViewerX.Reporting;

public static partial class EventReportCsvRenderer {
    private static void WriteSingleMetadata(EventReport report, string csvPath,
        string destination, string metadataPath) {
        using SHA256 hash = SHA256.Create();
        using FileStream csv = File.OpenRead(csvPath);
        string digest = BitConverter.ToString(hash.ComputeHash(csv)).Replace("-", string.Empty).ToLowerInvariant();
        File.WriteAllText(metadataPath, JsonSerializer.Serialize(new EventCsvMetadata {
            SchemaVersion = 1,
            Title = report.Title,
            GeneratedAt = report.GeneratedAt,
            QueryDurationMilliseconds = report.QueryDuration.TotalMilliseconds,
            FileName = Path.GetFileName(destination),
            Sha256 = digest,
            Summary = EventReportSummary.Create(report)
        }, EventReportingJsonContext.Default.EventCsvMetadata), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
