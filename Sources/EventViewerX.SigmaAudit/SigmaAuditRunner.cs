using EventViewerX.Sigma;

namespace EventViewerX.SigmaAudit;

internal static class SigmaAuditRunner {
    private const string Repository = "https://github.com/SigmaHQ/sigma";

    internal static SigmaAuditReport Run(AuditOptions options) {
        string scanRoot = options.Scope == AuditScope.Windows
            ? Path.Combine(options.CorpusPath, "windows")
            : options.CorpusPath;
        if (!Directory.Exists(scanRoot)) {
            throw new ArgumentException(
                $"Sigma scope root '{scanRoot}' does not exist.");
        }
        GitCorpusSnapshot corpus = GitCorpusVerifier.Verify(
            options.CorpusPath,
            scanRoot,
            options.Commit);
        SigmaLogSourceProfile? profile = options.Profile switch {
            AuditProfile.Strict => null,
            AuditProfile.WindowsSysmonAndPowerShell =>
                SigmaLogSourceProfile.WindowsSysmonAndPowerShell,
            _ => throw new ArgumentOutOfRangeException(nameof(options.Profile))
        };
        var compilationOptions = new SigmaCompilationOptions {
            LogSourceProfile = profile
        };
        string scanPrefix = Path.GetFullPath(scanRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        string[] files = corpus.Files
            .Where(path => path.StartsWith(
                scanPrefix,
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (files.Length == 0) {
            throw new InvalidDataException(
                $"Sigma scope root '{scanRoot}' contains no YAML files.");
        }

        SigmaFileResult[] results = SigmaCorpusCompiler.Audit(
            options.CorpusPath,
            scanRoot,
            files,
            compilationOptions);
        int supported = results.Count(static item =>
            item.Status == SigmaFileStatus.Supported);
        int supportedWithWarnings = results.Count(static item =>
            item.Status == SigmaFileStatus.SupportedWithWarnings);
        int unsupported = results.Count(static item =>
            item.Status == SigmaFileStatus.Unsupported);
        int supportedTotal = supported + supportedWithWarnings;
        return new SigmaAuditReport {
            Repository = Repository,
            Commit = options.Commit,
            Scope = options.Scope,
            ProfileId = profile?.ProfileId ?? "strict",
            ProfileVersion = profile?.Version ?? "1.0.0",
            GeneratedAtUtc = DateTime.UtcNow,
            Summary = new SigmaAuditSummary {
                TotalFiles = results.Length,
                SupportedFiles = supported,
                SupportedWithWarningsFiles = supportedWithWarnings,
                UnsupportedFiles = unsupported,
                CompiledRules = results.Sum(static item => item.CompiledRules),
                SupportedPercent = Percent(supportedTotal, results.Length)
            },
            Categories = BuildCategories(results),
            Diagnostics = BuildDiagnostics(results),
            Files = results
        };
    }

    private static IReadOnlyList<SigmaCategorySummary> BuildCategories(
        IEnumerable<SigmaFileResult> results) => results
        .GroupBy(static item => item.Category, StringComparer.OrdinalIgnoreCase)
        .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
        .Select(static group => {
            int supported = group.Count(static item =>
                item.Status == SigmaFileStatus.Supported);
            int warnings = group.Count(static item =>
                item.Status == SigmaFileStatus.SupportedWithWarnings);
            int unsupported = group.Count(static item =>
                item.Status == SigmaFileStatus.Unsupported);
            return new SigmaCategorySummary {
                Category = group.Key,
                TotalFiles = group.Count(),
                SupportedFiles = supported,
                SupportedWithWarningsFiles = warnings,
                UnsupportedFiles = unsupported,
                SupportedPercent = Percent(
                    supported + warnings,
                    group.Count())
            };
        })
        .ToArray();

    private static IReadOnlyList<SigmaDiagnosticSummary> BuildDiagnostics(
        IEnumerable<SigmaFileResult> results) => results
        .SelectMany(static file => file.Diagnostics.Select(diagnostic =>
            new { File = file.Path, Diagnostic = diagnostic }))
        .GroupBy(
            static item => (item.Diagnostic.Code, item.Diagnostic.Severity),
            static item => item)
        .Select(static group => new SigmaDiagnosticSummary {
            Code = group.Key.Code,
            Severity = group.Key.Severity,
            FileCount = group.Select(static item => item.File)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            Occurrences = group.Count()
        })
        .OrderByDescending(static item => item.FileCount)
        .ThenBy(static item => item.Code, StringComparer.Ordinal)
        .ToArray();

    private static double Percent(int numerator, int denominator) =>
        denominator == 0
            ? 0
            : Math.Round(
                numerator * 100d / denominator,
                2,
                MidpointRounding.AwayFromZero);
}
