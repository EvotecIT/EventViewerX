using System.Globalization;
using System.Text;

namespace EventViewerX.SigmaAudit;

internal static class MarkdownReportWriter {
    internal static string Render(SigmaAuditReport report) {
        var builder = new StringBuilder();
        builder.AppendLine("# Sigma compatibility audit");
        builder.AppendLine();
        builder.AppendLine($"- Repository: `{report.Repository}`");
        builder.AppendLine($"- Commit: `{report.Commit}`");
        builder.AppendLine($"- Scope: `{report.Scope}`");
        builder.AppendLine($"- Telemetry profile: `{report.ProfileId}` version `{report.ProfileVersion}`");
        builder.AppendLine($"- Generated: `{report.GeneratedAtUtc:O}`");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine("| Files | Supported | Supported with warnings | Unsupported | Compiled rules | Supported % |");
        builder.AppendLine("| ---: | ---: | ---: | ---: | ---: | ---: |");
        builder.AppendLine(
            $"| {report.Summary.TotalFiles} | {report.Summary.SupportedFiles} | " +
            $"{report.Summary.SupportedWithWarningsFiles} | {report.Summary.UnsupportedFiles} | " +
            $"{report.Summary.CompiledRules} | {Number(report.Summary.SupportedPercent)} |" );
        builder.AppendLine();
        builder.AppendLine("## Categories");
        builder.AppendLine();
        builder.AppendLine("| Category | Files | Supported | Warnings | Unsupported | Supported % |");
        builder.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: |");
        foreach (SigmaCategorySummary category in report.Categories) {
            builder.AppendLine(
                $"| {Escape(category.Category)} | {category.TotalFiles} | " +
                $"{category.SupportedFiles} | {category.SupportedWithWarningsFiles} | " +
                $"{category.UnsupportedFiles} | {Number(category.SupportedPercent)} |" );
        }
        builder.AppendLine();
        builder.AppendLine("## Diagnostic coverage");
        builder.AppendLine();
        builder.AppendLine("| Code | Severity | Files | Occurrences |");
        builder.AppendLine("| --- | --- | ---: | ---: |");
        foreach (SigmaDiagnosticSummary diagnostic in report.Diagnostics) {
            builder.AppendLine(
                $"| `{Escape(diagnostic.Code)}` | {Escape(diagnostic.Severity)} | " +
                $"{diagnostic.FileCount} | {diagnostic.Occurrences} |" );
        }
        return builder.ToString();
    }

    private static string Number(double value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Escape(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal);
}
