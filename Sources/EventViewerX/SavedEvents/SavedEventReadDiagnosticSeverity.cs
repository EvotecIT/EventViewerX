namespace EventViewerX;

/// <summary>Severity of a saved-event parser or recovery diagnostic.</summary>
public enum SavedEventReadDiagnosticSeverity {
    /// <summary>Additional parser or fidelity information.</summary>
    Information,
    /// <summary>A record or region was recovered with a disclosed limitation.</summary>
    Warning,
    /// <summary>A record or region could not be parsed without loss.</summary>
    Error
}
