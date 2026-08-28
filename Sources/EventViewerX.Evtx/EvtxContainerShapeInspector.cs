namespace EventViewerX.Evtx;

internal static class EvtxContainerShapeInspector {
    internal static void Report(
        string path,
        Action<SavedEventReadDiagnostic>? diagnosticHandler) {

        const long firstChunkOffset = 4096;
        const long chunkSize = 65536;
        long length = new FileInfo(Path.GetFullPath(path)).Length;
        long payloadLength = Math.Max(0, length - firstChunkOffset);
        if (length < firstChunkOffset) {
            diagnosticHandler?.Invoke(new SavedEventReadDiagnostic {
                Code = "EVXEVTX002",
                Severity = SavedEventReadDiagnosticSeverity.Error,
                Message = "The EVTX container is shorter than its file header.",
                FileOffset = length
            });
        } else if (payloadLength % chunkSize != 0) {
            diagnosticHandler?.Invoke(new SavedEventReadDiagnostic {
                Code = "EVXEVTX002",
                Severity = SavedEventReadDiagnosticSeverity.Warning,
                Message = $"The EVTX container ends inside a chunk ({payloadLength % chunkSize} of {chunkSize} bytes in the final region). Parseable records may still be retained.",
                FileOffset = length,
                Recovered = true
            });
        }
    }
}
