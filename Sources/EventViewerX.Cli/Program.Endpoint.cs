using System.Globalization;
using System.Text;
using System.Text.Json;
using EventViewerX.Reporting;

namespace EventViewerX.Cli;

internal static partial class Program {
    private static int Diagnostic(CliArguments options) {
        using var cancellation = new ConsoleQueryCancellation();
        if (options.Subcommand == "code") {
            return WriteJson(EventDiagnosticCode.Resolve(options.Require("value"), ParseEnum(options.Get("kind"), EventDiagnosticCodeKind.Unknown, "--kind")));
        }
        if (options.Subcommand != "read") { throw new ArgumentException("Use diagnostics read or diagnostics code."); }
        string inputPath = System.IO.Path.GetFullPath(options.Require("path"));
        EventDiagnosticCheckpoint? checkpoint = null;
        string? checkpointPath = options.Get("checkpoint");
        if (checkpointPath != null) {
            checkpointPath = System.IO.Path.GetFullPath(checkpointPath);
            StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (string.Equals(ResolveDiagnosticPath(inputPath), ResolveDiagnosticPath(checkpointPath), comparison)) {
                throw new ArgumentException("The checkpoint path must be different from the diagnostic input path.");
            }
        }
        if (checkpointPath != null && File.Exists(checkpointPath)) {
            if (new FileInfo(checkpointPath).Length > 64 * 1024) { throw new InvalidDataException("Checkpoint exceeds 64 KiB."); }
            checkpoint = JsonSerializer.Deserialize<EventDiagnosticCheckpoint>(File.ReadAllText(checkpointPath), JsonOptions) ?? throw new InvalidDataException("Missing checkpoint.");
        }
        var result = EventDiagnosticLogReader.Read(inputPath, new EventDiagnosticReadOptions {
            UtcOffset = Offset(options), FinalFile = !options.Has("live"), MaximumRecords = options.GetInt("max-records", 10_000),
            MaximumBatchBytes = options.GetInt("max-batch-bytes", 8 * 1024 * 1024), MaximumRecordBytes = options.GetInt("max-record-bytes", 256 * 1024)
        }, checkpoint, cancellation.Token);
        int exit = WriteJson(result);
        // The caller must durably retain stdout before accepting this position. A reader cannot make an external sink atomic.
        if (checkpointPath != null) {
            string temporary = System.IO.Path.GetFullPath(checkpointPath) + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                File.WriteAllText(temporary, JsonSerializer.Serialize(result.Checkpoint, JsonOptions), new UTF8Encoding(false));
                File.Move(temporary, checkpointPath, true);
            } finally { if (File.Exists(temporary)) { File.Delete(temporary); } }
        }
        return exit;
    }

    private static string ResolveDiagnosticPath(string fullPath) {
        string root = System.IO.Path.GetPathRoot(fullPath)!;
        string[] parts = fullPath.Substring(root.Length).Split(System.IO.Path.DirectorySeparatorChar);
        string directory = root;
        for (int index = 0; index < parts.Length - 1; index++) {
            directory = System.IO.Path.Combine(directory, parts[index]);
            if (Directory.Exists(directory)) {
                directory = new DirectoryInfo(directory).ResolveLinkTarget(true)?.FullName ?? directory;
            }
        }
        string path = System.IO.Path.Combine(directory, parts[parts.Length - 1]);
        return File.Exists(path) ? new FileInfo(path).ResolveLinkTarget(true)?.FullName ?? path : path;
    }

    private static TimeSpan? Offset(CliArguments options) => options.Get("utc-offset-minutes") is string minutes
        ? TimeSpan.FromMinutes(int.Parse(minutes, CultureInfo.InvariantCulture)) : null;

    private static int EndpointInvestigation(CliArguments options) {
        using var cancellation = new ConsoleQueryCancellation();
        EventInvestigationSession session;
        if (options.Subcommand == "create") {
            var inputs = new List<EventEndpointInput>();
            foreach ((string flag, string kind) in new[] { ("log", "Log"), ("dsreg", "DsRegCmd"), ("facts", "Facts"), ("registry", "Registry"), ("attachment", "Attachment") }) {
                foreach (string path in options.GetMany(flag)) {
                    inputs.Add(new EventEndpointInput { Path = path, Kind = kind, Device = options.Get("device") ?? string.Empty, UtcOffset = Offset(options), ExecutionContext = options.Get("context") ?? "Unknown",
                        CapturedAt = options.Get("captured-at") is string at ? ParseCaptureTime(at) : null });
                }
            }
            session = EventInvestigationSession.CreateEndpoint(options.Require("directory"), new EventEndpointCapture {
                Inputs = inputs.ToArray(), ExpectedJoin = options.Get("expected-join"), MaximumRecords = options.GetInt("max-records", 25_000),
                MaximumInputBytes = options.GetLong("max-input-bytes", 256L * 1024 * 1024), MaximumRecordBytes = options.GetLong("max-record-bytes", 64L * 1024 * 1024)
            }, cancellationToken: cancellation.Token);
        } else if (options.Subcommand is "replay" or "inspect") {
            session = EventInvestigationSession.Open(options.Require("directory"), cancellation.Token);
        } else { throw new ArgumentException("Use investigation create, inspect, or replay."); }
        EventEndpointAnalysis analysis = options.Subcommand == "replay" ? session.ReplayEndpoint(options.Has("allow-different-engine"), cancellation.Token) : session.ReadEndpointAnalysis(cancellation.Token);
        if (options.Get("html") is string html) {
            string destination = System.IO.Path.GetFullPath(html);
            if (destination.StartsWith(session.DirectoryPath.TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) {
                throw new ArgumentException("Write presentation exports outside the immutable investigation directory.");
            }
            using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(output, new UTF8Encoding(false));
            writer.Write(EventEndpointHtmlRenderer.Render(analysis, options.Has("include-sensitive")));
        }
        return WriteJson(new { Directory = session.DirectoryPath, analysis.InputComplete, analysis.Applications, analysis.Findings, analysis.CoverageDiagnostics });
    }

    private static DateTimeOffset ParseCaptureTime(string value) {
        if (!value.EndsWith("Z", StringComparison.OrdinalIgnoreCase) && !System.Text.RegularExpressions.Regex.IsMatch(value, "[+-]\\d{2}:\\d{2}$")) { throw new ArgumentException("--captured-at requires an explicit UTC offset or Z."); }
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
    }
}