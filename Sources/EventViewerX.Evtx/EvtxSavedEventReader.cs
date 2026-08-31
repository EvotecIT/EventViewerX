using ThirdPartyEventLog = evtx.EventLog;
using ThirdPartyEventRecord = evtx.EventRecord;

namespace EventViewerX.Evtx;

/// <summary>
/// Cross-platform EVTX reader backed by the dependency-isolated <c>evtx</c> parser package.
/// Provider-formatted messages are unavailable without Windows provider resources.
/// </summary>
public sealed class EvtxSavedEventReader : ISavedEventReader {
    /// <inheritdoc />
    public IEnumerable<SavedEventRecord> Read(
        EventLogFileQuery query,
        Action<SavedEventReadDiagnostic>? diagnosticHandler = null,
        CancellationToken cancellationToken = default) {

        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        var matcher = new EvtxXPathMatcher(query.XPath);
        using FileStream stream = File.Open(
            Path.GetFullPath(query.Path),
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        var eventLog = new ThirdPartyEventLog(stream);
        ReportHeaderIntegrity(eventLog, diagnosticHandler);
        ReportChunkHeaders(stream, eventLog.ChunkCount, diagnosticHandler);
        bool literalBinXml = EvtxLiteralRecordReader.IsLiteralBinXml(stream);
        if (literalBinXml) {
            diagnosticHandler?.Invoke(new SavedEventReadDiagnostic {
                Code = "EVXEVTX005",
                Severity = SavedEventReadDiagnosticSeverity.Information,
                Message = "The file stores literal BinXML records (commonly produced by rendered WEC subscriptions); the spec-aligned literal reader is active."
            });
        }

        if (query.Oldest) {
            IEnumerable<SavedEventRecord> forward = literalBinXml
                ? ReadLiteral(stream, matcher, cancellationToken)
                : ReadForward(eventLog, matcher, diagnosticHandler, cancellationToken);
            foreach (SavedEventRecord record in forward) {
                yield return record;
            }
            if (!literalBinXml) {
                ReportParserErrors(eventLog, diagnosticHandler);
            }
            yield break;
        }

        diagnosticHandler?.Invoke(new SavedEventReadDiagnostic {
            Code = "EVXEVTX101",
            Severity = SavedEventReadDiagnosticSeverity.Information,
            Message = query.MaxEvents > 0
                ? $"Newest-first EVTX enumeration retains at most the newest {query.MaxEvents} matching record(s) because the parser streams chunks forward."
                : "Newest-first EVTX enumeration buffers all matching records because the parser streams chunks forward. Use Oldest=true or set MaxEvents for bounded-memory streaming."
        });
        IEnumerable<SavedEventRecord> newestSource = literalBinXml
            ? ReadLiteral(stream, matcher, cancellationToken)
            : ReadForward(eventLog, matcher, diagnosticHandler, cancellationToken);
        foreach (SavedEventRecord record in NewestFirstSavedEventBuffer.Read(
                     newestSource,
                     query.MaxEvents,
                     cancellationToken)) {
            yield return record;
        }
        if (!literalBinXml) {
            ReportParserErrors(eventLog, diagnosticHandler);
        }
    }

    private static IEnumerable<SavedEventRecord> ReadLiteral(
        Stream stream,
        EvtxXPathMatcher matcher,
        CancellationToken cancellationToken) {

        foreach (EvtxLiteralRecordReader.LiteralEvtxRecord source in
                 EvtxLiteralRecordReader.Read(stream, cancellationToken)) {
            if (!matcher.IsMatch(source.Xml)) {
                continue;
            }
            SavedEventRecord record = SavedEventXmlProjector.Create(
                source.Xml,
                source.RecordNumber,
                source.TimestampUtc);
            record.FileOffset = source.FileOffset;
            yield return record;
        }
    }

    private static IEnumerable<SavedEventRecord> ReadForward(
        ThirdPartyEventLog eventLog,
        EvtxXPathMatcher matcher,
        Action<SavedEventReadDiagnostic>? diagnosticHandler,
        CancellationToken cancellationToken) {

        foreach (ThirdPartyEventRecord source in eventLog.GetEventRecords()) {
            cancellationToken.ThrowIfCancellationRequested();
            string xml;
            try {
                xml = source.ConvertPayloadToXml();
            } catch (Exception exception) {
                diagnosticHandler?.Invoke(new SavedEventReadDiagnostic {
                    Code = "EVXEVTX201",
                    Severity = SavedEventReadDiagnosticSeverity.Error,
                    Message = $"Record {source.RecordNumber} could not be rendered as XML: {exception.Message}",
                    FileOffset = GetFileOffset(source)
                });
                throw new InvalidDataException(
                    $"EVTX record {source.RecordNumber} could not be rendered without weakening fidelity.",
                    exception);
            }
            if (!matcher.IsMatch(xml)) {
                continue;
            }
            SavedEventRecord record = SavedEventXmlProjector.Create(
                xml,
                source.RecordNumber,
                source.Timestamp.UtcDateTime);
            record.FileOffset = GetFileOffset(source);
            yield return record;
        }
    }

    private static long GetFileOffset(ThirdPartyEventRecord source) =>
        4096L + source.ChunkNumber * 65536L + source.RecordPosition;

    private static void ReportHeaderIntegrity(
        ThirdPartyEventLog eventLog,
        Action<SavedEventReadDiagnostic>? diagnosticHandler) {

        if (eventLog.Crc == eventLog.CalculatedCrc) {
            return;
        }
        diagnosticHandler?.Invoke(new SavedEventReadDiagnostic {
            Code = "EVXEVTX001",
            Severity = SavedEventReadDiagnosticSeverity.Warning,
            Message = $"The EVTX file-header checksum is invalid (stored 0x{eventLog.Crc:X8}, calculated 0x{eventLog.CalculatedCrc:X8}).",
            FileOffset = 0,
            Recovered = true
        });
    }

    private static void ReportChunkHeaders(
        Stream stream,
        int chunkCount,
        Action<SavedEventReadDiagnostic>? diagnosticHandler) {

        const long firstChunkOffset = 4096;
        const long chunkSize = 65536;
        const long chunkSignature = 0x006B6E6843666C45;
        long payloadLength = Math.Max(0, stream.Length - firstChunkOffset);
        if (payloadLength % chunkSize != 0) {
            diagnosticHandler?.Invoke(new SavedEventReadDiagnostic {
                Code = "EVXEVTX002",
                Severity = SavedEventReadDiagnosticSeverity.Warning,
                Message = $"The EVTX container ends inside a chunk ({payloadLength % chunkSize} of {chunkSize} bytes in the final region). Parseable records were retained.",
                FileOffset = stream.Length,
                Recovered = true
            });
        }
        var signature = new byte[8];
        int emptyChunks = 0;
        long? firstEmptyOffset = null;
        for (int index = 0; index < chunkCount; index++) {
            long offset = firstChunkOffset + index * chunkSize;
            if (offset + signature.Length > stream.Length) {
                diagnosticHandler?.Invoke(new SavedEventReadDiagnostic {
                    Code = "EVXEVTX002",
                    Severity = SavedEventReadDiagnosticSeverity.Error,
                    Message = $"EVTX chunk {index} is truncated before its header signature.",
                    FileOffset = offset
                });
                break;
            }
            stream.Position = offset;
            int read = stream.Read(signature, 0, signature.Length);
            if (read == signature.Length && signature.All(static value => value == 0)) {
                emptyChunks++;
                firstEmptyOffset ??= offset;
                continue;
            }
            if (read != signature.Length || BitConverter.ToInt64(signature, 0) != chunkSignature) {
                diagnosticHandler?.Invoke(new SavedEventReadDiagnostic {
                    Code = "EVXEVTX003",
                    Severity = SavedEventReadDiagnosticSeverity.Warning,
                    Message = $"EVTX chunk {index} has an invalid header signature and cannot be parsed normally.",
                    FileOffset = offset,
                    Recovered = true
                });
            }
        }
        if (emptyChunks > 0) {
            diagnosticHandler?.Invoke(new SavedEventReadDiagnostic {
                Code = "EVXEVTX004",
                Severity = SavedEventReadDiagnosticSeverity.Warning,
                Message = $"The file declares {chunkCount} chunks but {emptyChunks} chunk regions have no header. Valid chunks were parsed and empty regions were skipped.",
                FileOffset = firstEmptyOffset,
                Recovered = true
            });
        }
        stream.Position = firstChunkOffset;
    }

    private static void ReportParserErrors(
        ThirdPartyEventLog eventLog,
        Action<SavedEventReadDiagnostic>? diagnosticHandler) {

        foreach (IGrouping<string, KeyValuePair<long, string>> group in eventLog.ErrorRecords
                     .OrderBy(static item => item.Key)
                     .GroupBy(static item => item.Value, StringComparer.Ordinal)) {
            KeyValuePair<long, string> first = group.First();
            diagnosticHandler?.Invoke(new SavedEventReadDiagnostic {
                Code = "EVXEVTX202",
                Severity = SavedEventReadDiagnosticSeverity.Warning,
                Message = $"The parser skipped {group.Count()} record(s); first record number {first.Key}: {first.Value}",
                Recovered = true
            });
        }
    }
}
