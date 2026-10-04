using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace EventViewerX.Reporting;

public static partial class EventEvidenceBundle {
    /// <summary>Verifies the exact supported inventory, uncompressed bounds and every content checksum without extracting members.</summary>
    /// <remarks>Returns the verified inventory. A mismatch, extra member, unsupported schema or exceeded limit throws InvalidDataException.</remarks>
    public static EventEvidenceBundleManifest Verify(Stream source, long maximumUncompressedBytes = 256L * 1024 * 1024,
        CancellationToken cancellationToken = default) {
        if (source == null) { throw new ArgumentNullException(nameof(source)); }
        if (!source.CanRead || !source.CanSeek) { throw new ArgumentException("Verification requires a readable seekable stream.", nameof(source)); }
        ValidateMaximum(maximumUncompressedBytes);
        cancellationToken.ThrowIfCancellationRequested();
        PreflightArchive(source, maximumUncompressedBytes, cancellationToken);
        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);
        if (archive.Entries.Count < 3 || archive.Entries.Count > ContentNames.Count + 1 ||
            archive.Entries.Select(static entry => entry.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != archive.Entries.Count ||
            archive.Entries.Any(static entry => entry.FullName != "manifest.json" && !ContentNames.Contains(entry.FullName))) {
            throw new InvalidDataException("Evidence bundle inventory contains duplicate, unsupported or missing members.");
        }
        ZipArchiveEntry manifestEntry = archive.GetEntry("manifest.json") ?? throw new InvalidDataException("The evidence manifest is missing.");
        if (manifestEntry.Length > MaximumManifestBytes) { throw new InvalidDataException("The evidence manifest exceeds 1 MiB."); }
        using var manifestBytes = new MemoryStream();
        using (Stream input = manifestEntry.Open()) { ReadBounded(input, manifestBytes, MaximumManifestBytes, cancellationToken); }
        EventEvidenceBundleManifest manifest;
        try {
            using JsonDocument json = JsonDocument.Parse(manifestBytes.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
            if (json.RootElement.GetProperty("SchemaVersion").GetInt32() != 1) {
                throw new InvalidDataException("Unsupported evidence schema or invalid completion evidence.");
            }
            ValidateSummary(json.RootElement.GetProperty("Summary"));
            manifest = JsonSerializer.Deserialize<EventEvidenceBundleManifest>(json.RootElement.GetRawText())
                ?? throw new InvalidDataException("The evidence manifest is empty.");
        } catch (Exception exception) when (exception is JsonException || exception is KeyNotFoundException || exception is InvalidOperationException || exception is FormatException) {
            throw new InvalidDataException("The evidence manifest is invalid.", exception);
        }
        if (manifest.Entries == null || manifest.Entries.Count != archive.Entries.Count - 1 ||
            !manifest.Entries.Any(static entry => entry != null && entry.Name == "rows.jsonl") ||
            !manifest.Entries.Any(static entry => entry != null && entry.Name == "schemas.json") ||
            manifest.Entries.Any(static entry => entry == null || !ContentNames.Contains(entry.Name) || entry.Length < 0 ||
                entry.Sha256 == null || entry.Sha256.Length != 64 || entry.Sha256.Any(static digit => !Uri.IsHexDigit(digit))) ||
            manifest.Entries.Select(static entry => entry.Name).Distinct(StringComparer.Ordinal).Count() != manifest.Entries.Count) {
            throw new InvalidDataException("The manifest inventory is invalid.");
        }
        long total = manifestBytes.Length;
        foreach (EventEvidenceBundleEntry declared in manifest.Entries) {
            cancellationToken.ThrowIfCancellationRequested();
            ZipArchiveEntry member = archive.GetEntry(declared.Name) ?? throw new InvalidDataException("A declared evidence member is missing.");
            if (member.Length != declared.Length || declared.Length > maximumUncompressedBytes - total) {
                throw new InvalidDataException($"Evidence member '{declared.Name}' exceeds its declared length or byte bound.");
            }
            using Stream input = member.Open();
            using SHA256 hash = SHA256.Create();
            byte[] buffer = new byte[64 * 1024];
            long actual = 0;
            long rowCount = 0;
            byte lastByte = 0;
            int count;
            while ((count = input.Read(buffer, 0, buffer.Length)) > 0) {
                cancellationToken.ThrowIfCancellationRequested();
                if (count > declared.Length - actual) { throw new InvalidDataException("Evidence content exceeds its declared length."); }
                hash.TransformBlock(buffer, 0, count, null, 0);
                if (declared.Name == "rows.jsonl") {
                    for (int index = 0; index < count; index++) { if (buffer[index] == '\n') { rowCount++; } }
                    lastByte = buffer[count - 1];
                }
                actual += count;
            }
            if (declared.Name == "rows.jsonl" && (rowCount != manifest.Summary.GetProperty("EventCount").GetInt64() ||
                (actual > 0 && lastByte != '\n'))) {
                throw new InvalidDataException("Evidence row count does not match completion evidence.");
            }
            hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            if (actual != declared.Length || !string.Equals(HashText(hash.Hash!), declared.Sha256, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidDataException($"Evidence checksum mismatch for '{declared.Name}'.");
            }
            total += actual;
        }
        return manifest;
    }

    private static void ValidateSummary(JsonElement summary) {
        if (summary.GetProperty("SchemaVersion").GetInt32() != 1 ||
            summary.GetProperty("EventCount").GetInt64() < 0 || summary.GetProperty("EventsScanned").GetInt64() < 0) {
            throw new InvalidDataException("The completion evidence schema or counts are invalid.");
        }
        bool limited = summary.GetProperty("ScanLimitReached").GetBoolean();
        string? diagnostic = summary.GetProperty("CompletenessDiagnostic").GetString();
        JsonElement coverage = summary.GetProperty("Coverage");
        bool succeeded = true;
        foreach (JsonElement source in coverage.EnumerateArray()) {
            succeeded &= source.GetProperty("Succeeded").GetBoolean();
            foreach (string name in new[] { "MachineName", "LogName", "Status", "Detail" }) { source.GetProperty(name).GetString(); }
        }
        if (summary.GetProperty("HasDeclaredCoverage").GetBoolean() != (coverage.GetArrayLength() > 0) ||
            summary.GetProperty("IsComplete").GetBoolean() != (!limited && string.IsNullOrWhiteSpace(diagnostic) && succeeded)) {
            throw new InvalidDataException("The completion flags contradict their coverage or diagnostics.");
        }
    }

    private static void PreflightArchive(Stream source, long maximum, CancellationToken token) {
        // Bundle v1 has a small fixed inventory and at most 2 GiB of content.
        // Bound ZIP metadata before ZipArchive materializes its entry collection.
        if (source.Length < 22 || source.Length > maximum + MaximumManifestBytes) {
            throw new InvalidDataException("Evidence ZIP exceeds its compressed bound or is incomplete.");
        }
        int tailLength = (int)Math.Min(source.Length, 22 + ushort.MaxValue);
        long tailStart = source.Length - tailLength;
        source.Position = tailStart;
        byte[] tail = new byte[tailLength];
        int offset = 0;
        while (offset < tail.Length) {
            token.ThrowIfCancellationRequested();
            int read = source.Read(tail, offset, tail.Length - offset);
            if (read == 0) { throw new InvalidDataException("The ZIP trailer is incomplete."); }
            offset += read;
        }
        for (int index = tail.Length - 22; index >= 0; index--) {
            if (BitConverter.ToUInt32(tail, index) != 0x06054b50 ||
                index + 22 + BitConverter.ToUInt16(tail, index + 20) != tail.Length) { continue; }
            ushort count = BitConverter.ToUInt16(tail, index + 10);
            uint directoryBytes = BitConverter.ToUInt32(tail, index + 12);
            uint directoryOffset = BitConverter.ToUInt32(tail, index + 16);
            if (BitConverter.ToUInt16(tail, index + 4) != 0 || BitConverter.ToUInt16(tail, index + 6) != 0 ||
                count < 3 || count > ContentNames.Count + 1 || BitConverter.ToUInt16(tail, index + 8) != count ||
                directoryBytes > 64 * 1024 || (long)directoryOffset + directoryBytes != tailStart + index) {
                throw new InvalidDataException("Evidence ZIP metadata exceeds the bundle inventory bounds.");
            }
            source.Position = 0;
            return;
        }
        throw new InvalidDataException("The evidence ZIP trailer is missing.");
    }

    private static void ReadBounded(Stream source, Stream destination, long maximum, CancellationToken token) {
        byte[] buffer = new byte[16 * 1024];
        long total = 0;
        int count;
        while ((count = source.Read(buffer, 0, buffer.Length)) > 0) {
            token.ThrowIfCancellationRequested();
            if (count > maximum - total) { throw new InvalidDataException("Evidence content exceeds its byte bound."); }
            destination.Write(buffer, 0, count);
            total += count;
        }
    }
}
