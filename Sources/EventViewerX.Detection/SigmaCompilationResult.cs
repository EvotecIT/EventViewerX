namespace EventViewerX.Sigma;

/// <summary>Compiled Sigma rules plus explicit unsupported-behavior diagnostics.</summary>
public sealed class SigmaCompilationResult {
    internal SigmaCompilationResult(
        IReadOnlyList<IEventDetectionRule> rules,
        IReadOnlyList<SigmaDiagnostic> diagnostics) {

        Rules = Array.AsReadOnly(rules.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    /// <summary>Successfully compiled native detection rules.</summary>
    public IReadOnlyList<IEventDetectionRule> Rules { get; }
    /// <summary>Validation, mapping, and unsupported-feature diagnostics.</summary>
    public IReadOnlyList<SigmaDiagnostic> Diagnostics { get; }
    /// <summary>Whether every supplied Sigma document compiled without errors.</summary>
    public bool IsSupported => Diagnostics.All(static item => item.Severity != SigmaDiagnosticSeverity.Error);

    /// <summary>Compiles all supported rules into the shared EventViewerX detection engine.</summary>
    public EventDetectionPlan CompilePlan(EventDetectionTuning? tuning = null) {
        if (!IsSupported) {
            throw new InvalidDataException("Sigma input contains unsupported or invalid behavior: " +
                                           string.Join(" ", Diagnostics
                                               .Where(static item => item.Severity == SigmaDiagnosticSeverity.Error)
                                               .Select(static item => item.Message)));
        }
        return EventDetectionPlan.Compile(Rules, tuning);
    }
}
