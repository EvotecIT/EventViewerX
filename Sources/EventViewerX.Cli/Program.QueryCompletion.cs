using System.Text.Json;
using EventViewerX.Reporting;

namespace EventViewerX.Cli;

internal static partial class Program {
    private static void ValidateQuerySummaryPath(CliArguments options) {
        if (options.Has("explain") && (options.Has("summary-file") || options.Has("require-complete"))) {
            throw new ArgumentException("--summary-file and --require-complete require a query execution and cannot be combined with --explain.");
        }
        if (options.Get("summary-file") is not string summaryPath) {
            return;
        }
        string destination = Path.GetFullPath(summaryPath);
        var inputPaths = new List<string>(options.GetMany("path"));
        foreach (string option in new[] { "store", "write-store", "context-store", "definition" }) {
            if (options.Get(option) is string path) {
                inputPaths.Add(path);
            }
        }
        if (options.Get("where") is string predicatePath && File.Exists(predicatePath)) {
            inputPaths.Add(predicatePath);
        }
        if (inputPaths.Any(path => string.Equals(Path.GetFullPath(path), destination,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))) {
            throw new ArgumentException("--summary-file must be separate from query input and history files.");
        }
    }

    private static int CompleteQuery(EventReportSummary summary, CliArguments options, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.IsNullOrWhiteSpace(summary.CompletenessDiagnostic)) {
            Console.Error.WriteLine(summary.CompletenessDiagnostic);
        } else if (summary.ScanLimitReached) {
            Console.Error.WriteLine("The query reached a candidate or result limit.");
        }
        foreach (EventReportCoverage source in summary.Coverage) {
            if (!source.Succeeded) {
                Console.Error.WriteLine($"{source.MachineName}/{source.LogName}: {source.Status} {source.Detail}");
            }
        }
        if (options.Get("summary-file") is string summaryPath) {
            cancellationToken.ThrowIfCancellationRequested();
            string destination = Path.GetFullPath(summaryPath);
            string? directory = Path.GetDirectoryName(destination);
            if (directory != null) {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(destination, JsonSerializer.Serialize(summary, JsonOptions));
        }
        return options.Has("require-complete") && !summary.IsComplete ? 2 : 0;
    }
}
