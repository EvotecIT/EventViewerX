using System.Text.Json.Serialization;

namespace EventViewerX.Reporting;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(EventNotificationBatchManifest))]
[JsonSerializable(typeof(EventNotificationDeliveryState))]
[JsonSerializable(typeof(EventCsvMetadata))]
[JsonSerializable(typeof(EventCsvManifest))]
internal partial class EventReportingJsonContext : JsonSerializerContext { }

internal sealed class EventCsvMetadata {
    public int SchemaVersion { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public double QueryDurationMilliseconds { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public EventReportSummary Summary { get; set; } = null!;
}

internal sealed class EventCsvManifest {
    public string Title { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public double QueryDurationMilliseconds { get; set; }
    public int EventCount { get; set; }
    public long EventsScanned { get; set; }
    public bool ScanLimitReached { get; set; }
    public string? CompletenessDiagnostic { get; set; }
    public EventReportSummary Summary { get; set; } = null!;
    public IEnumerable<EventCsvSectionManifest> Sections { get; set; } = Array.Empty<EventCsvSectionManifest>();
}

internal sealed class EventCsvSectionManifest {
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public int RowCount { get; set; }
    public string FileName { get; set; } = string.Empty;
}
