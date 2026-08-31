namespace EventViewerX;

/// <summary>Parser-neutral reader for saved event containers such as EVTX files.</summary>
public interface ISavedEventReader {
    /// <summary>
    /// Streams normalized saved records in the order requested by the query. Implementations must apply
    /// the query XPath or reject unsupported expressions explicitly; they must not silently weaken it.
    /// </summary>
    IEnumerable<SavedEventRecord> Read(
        EventLogFileQuery query,
        Action<SavedEventReadDiagnostic>? diagnosticHandler = null,
        CancellationToken cancellationToken = default);
}
