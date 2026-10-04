namespace EventViewerX.Reporting;

/// <summary>Optional provenance and rendered artifacts for a portable report evidence bundle.</summary>
public sealed class EventEvidenceBundleOptions {
    /// <summary>JSON describing the executed query, when it may be disclosed.</summary>
    public string? QueryJson { get; set; }
    /// <summary>JSON describing the custom or built-in definition used by the query.</summary>
    public string? DefinitionJson { get; set; }
    /// <summary>JSON containing enabled detection pack versions and hashes, when applicable.</summary>
    public string? PackProvenanceJson { get; set; }
    /// <summary>Identifies the policy already applied to the report. The writer records these choices without applying the policy or embedding its key.</summary>
    public EventReportPrivacyOptions? PrivacyPolicy { get; set; }
    /// <summary>Optional report.html, report.xlsx, report.csv, report-csv.zip, report.csv.metadata.json, email.html or email.txt artifacts. Streams remain caller-owned.</summary>
    public IReadOnlyDictionary<string, Stream> Reports { get; set; } = new Dictionary<string, Stream>();
    /// <summary>Maximum total uncompressed content, including the manifest. Default 256 MiB; raise explicitly for larger exports.</summary>
    public long MaximumUncompressedBytes { get; set; } = 256L * 1024 * 1024;
}
