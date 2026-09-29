namespace EventViewerX.Reports.Evtx;

/// <summary>
/// Canonical error kind produced by EVTX query execution.
/// </summary>
public enum EvtxQueryFailureKind {
    /// <summary>Invalid request arguments.</summary>
    InvalidArgument,
    /// <summary>Target EVTX file was not found.</summary>
    NotFound,
    /// <summary>Access to target EVTX file was denied.</summary>
    AccessDenied,
    /// <summary>I/O error occurred while reading EVTX file.</summary>
    IoError,
    /// <summary>Unexpected failure.</summary>
    Exception
}

/// <summary>
/// Failure payload produced by EVTX query execution.
/// </summary>
public sealed class EvtxQueryFailure {
    /// <summary>
    /// Gets or sets failure kind.
    /// </summary>
    public EvtxQueryFailureKind Kind { get; set; }

    /// <summary>
    /// Gets or sets failure message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
