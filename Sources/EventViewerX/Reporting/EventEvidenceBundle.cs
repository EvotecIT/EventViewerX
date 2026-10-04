using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace EventViewerX.Reporting;

/// <summary>Writes portable report evidence and verifies its bounded ZIP inventory without extracting files.</summary>
/// <remarks>Checksums establish internal byte integrity, not the authenticity of a producer. Sign or protect the bundle separately when authenticity is required.</remarks>
public static partial class EventEvidenceBundle {
    private const int MaximumManifestBytes = 1024 * 1024;
    private static readonly HashSet<string> ReportNames = new(StringComparer.Ordinal) {
        "report.html", "report.xlsx", "report.csv", "report-csv.zip", "report.csv.metadata.json", "email.html", "email.txt"
    };
    private static readonly HashSet<string> ContentNames = new(StringComparer.Ordinal) {
        "rows.jsonl", "schemas.json", "query.json", "definition.json", "packs.json", "privacy.json",
        "report.html", "report.xlsx", "report.csv", "report-csv.zip", "report.csv.metadata.json", "email.html", "email.txt"
    };

    /// <summary>Writes normalized rows, schemas, completion evidence and selected provenance to a caller-owned writable stream.</summary>
    /// <remarks>The caller owns atomic publication. A failed or canceled write leaves a partial stream that must not be published. Apply a privacy policy before calling, and select provenance separately.</remarks>
    public static EventEvidenceBundleManifest Write(EventReport report, Stream destination,
        EventEvidenceBundleOptions? options = null, CancellationToken cancellationToken = default) {
        if (report == null) { throw new ArgumentNullException(nameof(report)); }
        if (destination == null) { throw new ArgumentNullException(nameof(destination)); }
        if (!destination.CanWrite) { throw new ArgumentException("The destination must be writable.", nameof(destination)); }
        options ??= new EventEvidenceBundleOptions();
        ValidateMaximum(options.MaximumUncompressedBytes);
        if (options.Reports == null) { throw new ArgumentNullException(nameof(options.Reports)); }
        foreach (KeyValuePair<string, Stream> artifact in options.Reports) {
            if (!ReportNames.Contains(artifact.Key) || artifact.Value == null || !artifact.Value.CanRead || ReferenceEquals(artifact.Value, destination)) {
                throw new ArgumentException("Report streams must be readable, separate from the destination, and use a supported artifact name.", nameof(options));
            }
        }
        ValidateContext(options.QueryJson);
        ValidateContext(options.DefinitionJson);
        ValidateContext(options.PackProvenanceJson);
        string? privacyJson = options.PrivacyPolicy == null ? null : JsonSerializer.Serialize(new {
            SchemaVersion = 1, RowType = "Generic", options.PrivacyPolicy.RetainedValueFields,
            options.PrivacyPolicy.PseudonymizedValueFields, options.PrivacyPolicy.PseudonymizeSourceIdentities
        });
        ValidateContext(privacyJson);
        cancellationToken.ThrowIfCancellationRequested();
        var sectionsByRow = new Dictionary<EventReportRow, EventReportSection>();
        foreach (EventReportSection section in report.Sections) {
            foreach (EventReportRow row in section.Rows) {
                if (sectionsByRow.ContainsKey(row)) { throw new ArgumentException("A row occurs in more than one evidence section.", nameof(report)); }
                sectionsByRow.Add(row, section);
            }
        }
        if (sectionsByRow.Count != report.Rows.Count || report.Rows.Any(row => !sectionsByRow.ContainsKey(row))) {
            throw new ArgumentException("Evidence sections must cover each report row exactly once.", nameof(report));
        }
        var entries = new List<EventEvidenceBundleEntry>();
        long total = 0;
        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
        Add("rows.jsonl", output => {
            foreach (EventReportRow row in report.Rows) {
                cancellationToken.ThrowIfCancellationRequested();
                byte[] json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(EventReportJsonProjection.Project(row, sectionsByRow[row])) + "\n");
                output.Write(json, 0, json.Length);
            }
        });
        AddJson("schemas.json", JsonSerializer.Serialize(report.Sections.Select(EventReportSectionSchema.FromSection)));
        if (options.QueryJson != null) { AddJson("query.json", options.QueryJson); }
        if (options.DefinitionJson != null) { AddJson("definition.json", options.DefinitionJson); }
        if (options.PackProvenanceJson != null) { AddJson("packs.json", options.PackProvenanceJson); }
        if (privacyJson != null) { AddJson("privacy.json", privacyJson); }
        foreach (KeyValuePair<string, Stream> artifact in options.Reports.OrderBy(static item => item.Key, StringComparer.Ordinal)) {
            Add(artifact.Key, output => Copy(artifact.Value, output, cancellationToken));
        }
        using JsonDocument summary = JsonDocument.Parse(JsonSerializer.Serialize(EventReportSummary.Create(report)));
        var manifest = new EventEvidenceBundleManifest {
            SchemaVersion = 1, GeneratedAtUtc = report.GeneratedAt,
            ProducerVersion = typeof(EventEvidenceBundle).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown",
            Summary = summary.RootElement.Clone(), Entries = entries.ToArray()
        };
        byte[] manifestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest));
        if (manifestBytes.Length > MaximumManifestBytes || manifestBytes.Length > options.MaximumUncompressedBytes - total) {
            throw new InvalidDataException("The evidence manifest exceeds its byte bound.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        using (Stream output = archive.CreateEntry("manifest.json", CompressionLevel.Optimal).Open()) {
            output.Write(manifestBytes, 0, manifestBytes.Length);
        }
        return manifest;

        void AddJson(string name, string json) => Add(name, output => {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            output.Write(bytes, 0, bytes.Length);
        });
        void Add(string name, Action<Stream> write) {
            cancellationToken.ThrowIfCancellationRequested();
            using Stream member = archive.CreateEntry(name, CompressionLevel.Optimal).Open();
            using var bounded = new BundleWriteStream(member, options.MaximumUncompressedBytes - total, cancellationToken);
            write(bounded);
            entries.Add(new EventEvidenceBundleEntry { Name = name, Length = bounded.BytesWritten, Sha256 = bounded.FinishHash() });
            total += bounded.BytesWritten;
        }
    }

    private static void ValidateMaximum(long maximum) {
        if (maximum < MaximumManifestBytes || maximum > 2L * 1024 * 1024 * 1024) {
            throw new ArgumentOutOfRangeException(nameof(maximum), "The uncompressed bound must be between 1 MiB and 2 GiB.");
        }
    }

    private static void ValidateContext(string? json) {
        if (json == null) { return; }
        if (Encoding.UTF8.GetByteCount(json) > MaximumManifestBytes) { throw new ArgumentException("Provenance JSON exceeds 1 MiB."); }
        using JsonDocument parsed = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
    }

    private static void Copy(Stream source, Stream destination, CancellationToken token) {
        byte[] buffer = new byte[64 * 1024];
        int count;
        while ((count = source.Read(buffer, 0, buffer.Length)) > 0) {
            token.ThrowIfCancellationRequested();
            destination.Write(buffer, 0, count);
        }
    }

    private static string HashText(byte[] hash) => BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();

    private sealed class BundleWriteStream : Stream {
        private readonly Stream destination;
        private readonly long maximum;
        private readonly CancellationToken token;
        private readonly SHA256 hash = SHA256.Create();
        internal BundleWriteStream(Stream destination, long maximum, CancellationToken token) {
            this.destination = destination; this.maximum = maximum; this.token = token;
        }
        internal long BytesWritten { get; private set; }
        internal string FinishHash() { hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0); return HashText(hash.Hash!); }
        public override void Write(byte[] buffer, int offset, int count) {
            token.ThrowIfCancellationRequested();
            if (count > maximum - BytesWritten) { throw new InvalidDataException("Evidence content exceeds its uncompressed byte bound."); }
            destination.Write(buffer, offset, count);
            hash.TransformBlock(buffer, offset, count, null, 0);
            BytesWritten += count;
        }
        protected override void Dispose(bool disposing) { if (disposing) { hash.Dispose(); } base.Dispose(disposing); }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => BytesWritten;
        public override long Position { get => BytesWritten; set => throw new NotSupportedException(); }
        public override void Flush() => destination.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
