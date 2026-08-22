namespace EventViewerX;

internal sealed class EventReadinessConfigurationEvidence {
    internal EventReadinessConfigurationEvidence(
        EventReadinessStatus status,
        string evidence,
        string remediation,
        EventReadinessDiagnosticKind diagnosticKind = EventReadinessDiagnosticKind.None) {

        Status = status;
        Evidence = evidence;
        Remediation = remediation;
        DiagnosticKind = diagnosticKind;
    }

    internal EventReadinessStatus Status { get; }
    internal string Evidence { get; }
    internal string Remediation { get; }
    internal EventReadinessDiagnosticKind DiagnosticKind { get; }
}
