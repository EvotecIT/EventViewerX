namespace EventViewerX;

/// <summary>Structured corruption, recovery, or fidelity diagnostic from a saved-event reader.</summary>
public sealed class SavedEventReadDiagnostic {
    /// <summary>Stable diagnostic code.</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>Diagnostic severity.</summary>
    public SavedEventReadDiagnosticSeverity Severity { get; set; }
    /// <summary>Actionable description.</summary>
    public string Message { get; set; } = string.Empty;
    /// <summary>Optional byte offset in the saved container.</summary>
    public long? FileOffset { get; set; }
    /// <summary>Whether parsing continued after this condition.</summary>
    public bool Recovered { get; set; }
}
