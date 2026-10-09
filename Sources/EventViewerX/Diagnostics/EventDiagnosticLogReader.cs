using System.Globalization;
using System.Security.Cryptography;

namespace EventViewerX;

/// <summary>Reads UTF-8 or BOM-marked UTF-16 logs without materializing a whole file. Checkpoints never commit partial frames.</summary>
public static partial class EventDiagnosticLogReader {
    /// <summary>Reads a bounded batch. A stale physical identity or changed prefix/boundary resets the generation explicitly.</summary>
    public static EventDiagnosticReadResult Read(string path, EventDiagnosticReadOptions? options = null,
        EventDiagnosticCheckpoint? checkpoint = null, CancellationToken cancellationToken = default) {
        options ??= new EventDiagnosticReadOptions();
        if (options.MaximumRecordBytes < 128 || options.MaximumRecordBytes > 4 * 1024 * 1024 ||
            options.MaximumBatchBytes < options.MaximumRecordBytes || options.MaximumBatchBytes > 64 * 1024 * 1024 ||
            options.MaximumRecords <= 0 || options.MaximumRecords > 100_000 ||
            (options.UtcOffset.HasValue && (options.UtcOffset.Value.Ticks % TimeSpan.TicksPerMinute != 0 || options.UtcOffset.Value.Duration() > TimeSpan.FromHours(14)))) {
            throw new ArgumentOutOfRangeException(nameof(options), "Use finite bounds and a whole-minute offset within 14 hours.");
        }
        string fullPath = System.IO.Path.GetFullPath(path);
        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 8192);
        long length = stream.Length;
        string fileIdentity = PhysicalIdentity(stream, fullPath);
        (Encoding encoding, int bom, string encodingName) = DetectEncoding(stream);
        bool changed = checkpoint != null && (checkpoint.SchemaVersion != 1 || checkpoint.Path != fullPath ||
            checkpoint.Offset < bom || checkpoint.Offset > length || checkpoint.NextLine < 1 || checkpoint.PrefixLength < 0 || checkpoint.PrefixLength > 4096 ||
            checkpoint.FileIdentity != fileIdentity || checkpoint.Encoding != encodingName ||
            checkpoint.PrefixLength > length || Fingerprint(stream, 0, checkpoint.PrefixLength) != checkpoint.PrefixHash ||
            Fingerprint(stream, Math.Max(0, checkpoint.Offset - 4096), (int)Math.Min(4096, checkpoint.Offset)) != checkpoint.BoundaryHash);
        if (checkpoint != null && !changed && string.IsNullOrWhiteSpace(checkpoint.Generation)) { throw new ArgumentException("Missing checkpoint generation.", nameof(checkpoint)); }
        long offset = checkpoint == null || changed ? bom : checkpoint.Offset;
        long lineNumber = checkpoint == null || changed ? 1 : checkpoint.NextLine;
        string generation = checkpoint == null || changed ? Guid.NewGuid().ToString("N") : checkpoint.Generation;
        var records = new List<EventDiagnosticRecord>();
        var diagnostics = new List<string>();
        if (changed) { diagnostics.Add("The file generation changed; reading restarted at its beginning."); }
        diagnostics.Add("Resume validates physical identity, prefix, and the preceding 4 KiB. Changes elsewhere in previously consumed content are outside this bounded check.");
        long startOffset = offset;
        stream.Position = offset;
        var frame = new MemoryStream();
        long frameStart = offset, frameLine = lineNumber;
        bool oversized = false, cmTrace = false, decodingLoss = false;
        bool anyLoss = false;
        long consumed = offset;
        long nextLine = lineNumber;
        try {
            while (stream.Position < length && stream.Position - startOffset < options.MaximumBatchBytes && records.Count < options.MaximumRecords) {
                cancellationToken.ThrowIfCancellationRequested();
                using var line = new MemoryStream();
                bool lineOversized = false, newline = false;
                bool unexpectedEnd = false;
                bool lineHasFooter = false;
                int footerProgress = 0;
                const string footer = "]LOG]!>";
                int unit = encodingName.StartsWith("utf-16", StringComparison.Ordinal) ? 2 : 1;
                long limit = Math.Min(length, startOffset + options.MaximumBatchBytes);
                while (stream.Position + unit <= limit) {
                    cancellationToken.ThrowIfCancellationRequested();
                    int first = stream.ReadByte();
                    int second = unit == 2 ? stream.ReadByte() : -1;
                    if (first < 0 || unit == 2 && second < 0) { unexpectedEnd = true; break; }
                    int character = unit == 1 ? first : encodingName == "utf-16le" ? first | second << 8 : first << 8 | second;
                    footerProgress = character == footer[footerProgress] ? footerProgress + 1 : character == footer[0] ? 1 : 0;
                    if (footerProgress == footer.Length) { lineHasFooter = true; footerProgress = 0; }
                    if (line.Length + unit <= options.MaximumRecordBytes) { line.WriteByte((byte)first); if (unit == 2) { line.WriteByte((byte)second); } }
                    else { lineOversized = true; }
                    newline = unit == 1 ? first == 10 : encodingName == "utf-16le" ? first == 10 && second == 0 : first == 0 && second == 10;
                    if (newline) { break; }
                }
                if (unexpectedEnd) {
                    anyLoss = true;
                    diagnostics.Add("The file changed during this read; the unfinished frame was discarded. Resume to validate the generation.");
                    break;
                }
                bool eof = stream.Position == length;
                if (!newline && (!eof || !options.FinalFile)) { line.Dispose(); break; }
                if (!newline && unit == 2 && stream.Position < length) { line.Dispose(); break; }
                byte[] bytes = line.ToArray(); line.Dispose();
                string text;
                try { text = encoding.GetString(bytes); }
                catch (DecoderFallbackException) {
                    // A partial code point at EOF in a live file remains pending; invalid completed lines are diagnostic evidence.
                    if (!newline && !options.FinalFile) { break; }
                    text = Encoding.GetEncoding(encoding.CodePage).GetString(bytes); decodingLoss = true;
                }
                if (frame.Length == 0) { cmTrace = text.TrimStart().StartsWith("<![LOG[", StringComparison.Ordinal); }
                if (frame.Length + bytes.Length <= options.MaximumRecordBytes) { frame.Write(bytes, 0, bytes.Length); }
                else { oversized = true; }
                oversized |= lineOversized;
                bool closed = !cmTrace || lineHasFooter;
                if (!closed) { lineNumber++; continue; }
                string raw;
                try { raw = encoding.GetString(frame.ToArray()); }
                catch (DecoderFallbackException) { raw = Encoding.GetEncoding(encoding.CodePage).GetString(frame.ToArray()); decodingLoss = true; }
                EventDiagnosticRecord record = Parse(raw, options.UtcOffset);
                record.Source = options.Source ?? fullPath; record.Generation = generation;
                record.Device = options.Device;
                record.ByteStart = frameStart; record.ByteEnd = stream.Position;
                record.LineStart = frameLine; record.LineEnd = lineNumber;
                record.Identity = HashText(record.Source + "\n" + generation + "\n" + frameStart.ToString(CultureInfo.InvariantCulture) + "\n" + record.ByteEnd.ToString(CultureInfo.InvariantCulture));
                if (oversized || decodingLoss) {
                    record.Format = oversized ? "Oversized" : "Malformed";
                    record.Diagnostic = oversized ? "Frame exceeded the configured byte bound; the retained text is only a prefix." : "Invalid encoded bytes; inspect the original artifact.";
                }
                anyLoss |= record.Diagnostic != null;
                records.Add(record);
                lineNumber++;
                consumed = stream.Position; nextLine = lineNumber;
                frame.SetLength(0); frameStart = consumed; frameLine = lineNumber;
                oversized = false; decodingLoss = false;
            }
        } finally { frame.Dispose(); }
        if (consumed < length) { diagnostics.Add("Unread bytes remain: continue with the checkpoint, increase a batch bound, or wait for the trailing frame to complete."); }
        int prefixLength = (int)Math.Min(4096, consumed);
        var result = new EventDiagnosticReadResult {
            Records = records.ToArray(), GenerationChanged = changed, HasMore = consumed < length,
            IsComplete = consumed == length && !anyLoss,
            Checkpoint = new EventDiagnosticCheckpoint {
                Path = fullPath, FileIdentity = fileIdentity, Generation = generation, Encoding = encodingName,
                Offset = consumed, NextLine = nextLine, PrefixLength = prefixLength,
                PrefixHash = Fingerprint(stream, 0, prefixLength),
                BoundaryHash = Fingerprint(stream, Math.Max(0, consumed - 4096), (int)Math.Min(4096, consumed))
            }, Diagnostics = diagnostics.ToArray()
        };
        return result;
    }

    private static (Encoding, int, string) DetectEncoding(FileStream stream) {
        stream.Position = 0;
        int a = stream.ReadByte(), b = stream.ReadByte(), c = stream.ReadByte();
        if (a == 255 && b == 254) { return (new UnicodeEncoding(false, false, true), 2, "utf-16le"); }
        if (a == 254 && b == 255) { return (new UnicodeEncoding(true, false, true), 2, "utf-16be"); }
        return (new UTF8Encoding(false, true), a == 239 && b == 187 && c == 191 ? 3 : 0, "utf-8");
    }
    private static string Fingerprint(FileStream stream, long offset, int count) {
        stream.Position = offset;
        var buffer = new byte[count];
        int read = 0, next;
        while (read < count && (next = stream.Read(buffer, read, count - read)) > 0) { read += next; }
        using SHA256 hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(buffer, 0, read)).Replace("-", string.Empty);
    }
    internal static string HashText(string value) {
        using SHA256 hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty);
    }
}