using System.Text.Json;

namespace EventViewerX.Reporting;

/// <summary>Versioned inventory and completion evidence for an EventViewerX ZIP bundle.</summary>
public sealed class EventEvidenceBundleManifest {
    /// <summary>Bundle contract version.</summary>
    public int SchemaVersion { get; set; }
    /// <summary>UTC timestamp of the source snapshot.</summary>
    public DateTime GeneratedAtUtc { get; set; }
    /// <summary>EventViewerX assembly informational version.</summary>
    public string ProducerVersion { get; set; } = string.Empty;
    /// <summary>Serialized EventReportSummary, including source coverage and incomplete-input evidence.</summary>
    public JsonElement Summary { get; set; }
    /// <summary>Every content entry, excluding the manifest itself.</summary>
    public IReadOnlyList<EventEvidenceBundleEntry> Entries { get; set; } = Array.Empty<EventEvidenceBundleEntry>();
}

/// <summary>Length and SHA-256 integrity evidence for one bundle member.</summary>
public sealed class EventEvidenceBundleEntry {
    /// <summary>Canonical entry name without directory traversal.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Uncompressed bytes.</summary>
    public long Length { get; set; }
    /// <summary>Lowercase hexadecimal SHA-256 checksum.</summary>
    public string Sha256 { get; set; } = string.Empty;
}
