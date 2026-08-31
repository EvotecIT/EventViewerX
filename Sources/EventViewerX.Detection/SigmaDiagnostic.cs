namespace EventViewerX.Sigma;

/// <summary>Severity of a Sigma import diagnostic.</summary>
public enum SigmaDiagnosticSeverity {
    /// <summary>Informational mapping detail.</summary>
    Information,
    /// <summary>Supported import with a caveat that does not weaken matching.</summary>
    Warning,
    /// <summary>Unsupported or invalid behavior that prevents rule compilation.</summary>
    Error
}

/// <summary>Structured Sigma validation or compilation diagnostic.</summary>
public sealed class SigmaDiagnostic {
    internal SigmaDiagnostic(string code, SigmaDiagnosticSeverity severity, string message, int documentIndex) {
        Code = code;
        Severity = severity;
        Message = message;
        DocumentIndex = documentIndex;
    }

    /// <summary>Stable diagnostic code.</summary>
    public string Code { get; }
    /// <summary>Diagnostic severity.</summary>
    public SigmaDiagnosticSeverity Severity { get; }
    /// <summary>Actionable explanation.</summary>
    public string Message { get; }
    /// <summary>Zero-based YAML document index.</summary>
    public int DocumentIndex { get; }
}
