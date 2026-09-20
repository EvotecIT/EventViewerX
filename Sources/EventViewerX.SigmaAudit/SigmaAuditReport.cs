namespace EventViewerX.SigmaAudit;

internal enum AuditScope {
    Windows,
    All
}

internal enum AuditProfile {
    Strict,
    WindowsSysmonAndPowerShell
}

internal enum SigmaFileStatus {
    Supported,
    SupportedWithWarnings,
    Unsupported
}

internal sealed class SigmaAuditReport {
    public required string Repository { get; init; }
    public required string Commit { get; init; }
    public required AuditScope Scope { get; init; }
    public required string ProfileId { get; init; }
    public required string ProfileVersion { get; init; }
    public required DateTime GeneratedAtUtc { get; init; }
    public required SigmaAuditSummary Summary { get; init; }
    public required IReadOnlyList<SigmaCategorySummary> Categories { get; init; }
    public required IReadOnlyList<SigmaDiagnosticSummary> Diagnostics { get; init; }
    public required IReadOnlyList<SigmaFileResult> Files { get; init; }
}

internal sealed class SigmaAuditSummary {
    public required int TotalFiles { get; init; }
    public required int SupportedFiles { get; init; }
    public required int SupportedWithWarningsFiles { get; init; }
    public required int UnsupportedFiles { get; init; }
    public required int CompiledRules { get; init; }
    public required double SupportedPercent { get; init; }
}

internal sealed class SigmaCategorySummary {
    public required string Category { get; init; }
    public required int TotalFiles { get; init; }
    public required int SupportedFiles { get; init; }
    public required int SupportedWithWarningsFiles { get; init; }
    public required int UnsupportedFiles { get; init; }
    public required double SupportedPercent { get; init; }
}

internal sealed class SigmaDiagnosticSummary {
    public required string Code { get; init; }
    public required string Severity { get; init; }
    public required int FileCount { get; init; }
    public required int Occurrences { get; init; }
}

internal sealed class SigmaFileResult {
    public required string Path { get; init; }
    public required string Category { get; init; }
    public required SigmaFileStatus Status { get; init; }
    public required int CompiledRules { get; init; }
    public required IReadOnlyList<SigmaFileDiagnostic> Diagnostics { get; init; }
}

internal sealed class SigmaFileDiagnostic {
    public required string Code { get; init; }
    public required string Severity { get; init; }
    public required string Message { get; init; }
    public required int DocumentIndex { get; init; }
}
