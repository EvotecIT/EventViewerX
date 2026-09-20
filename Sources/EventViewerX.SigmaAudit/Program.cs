using System.Text.Json;
using System.Text.Json.Serialization;

namespace EventViewerX.SigmaAudit;

internal static class Program {
    public static int Main(string[] args) {
        try {
            AuditOptions options = AuditOptions.Parse(args);
            SigmaAuditReport report = SigmaAuditRunner.Run(options);
            var jsonOptions = new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };
            jsonOptions.Converters.Add(new JsonStringEnumConverter());

            WriteOutput(options.OutputJson, JsonSerializer.Serialize(report, jsonOptions));
            WriteOutput(options.OutputMarkdown, MarkdownReportWriter.Render(report));

            Console.WriteLine(
                $"Audited {report.Summary.TotalFiles} Sigma files: " +
                $"{report.Summary.SupportedFiles} supported, " +
                $"{report.Summary.SupportedWithWarningsFiles} supported with warnings, " +
                $"{report.Summary.UnsupportedFiles} unsupported.");
            return 0;
        } catch (ArgumentException exception) {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine(AuditOptions.Usage);
            return 64;
        } catch (Exception exception) {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void WriteOutput(string path, string content) {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }
}

internal sealed class AuditOptions {
    internal const string Usage =
        "Usage: EventViewerX.SigmaAudit --corpus <rules-path> --commit <sha> " +
        "--output-json <path> --output-markdown <path> [--scope windows|all] " +
        "[--profile strict|windows-sysmon-powershell]";

    private AuditOptions(
        string corpusPath,
        string commit,
        string outputJson,
        string outputMarkdown,
        AuditScope scope,
        AuditProfile profile) {

        CorpusPath = corpusPath;
        Commit = commit;
        OutputJson = outputJson;
        OutputMarkdown = outputMarkdown;
        Scope = scope;
        Profile = profile;
    }

    internal string CorpusPath { get; }
    internal string Commit { get; }
    internal string OutputJson { get; }
    internal string OutputMarkdown { get; }
    internal AuditScope Scope { get; }
    internal AuditProfile Profile { get; }

    internal static AuditOptions Parse(IReadOnlyList<string> args) {
        var allowed = new HashSet<string>(
            new[] {
                "corpus",
                "commit",
                "output-json",
                "output-markdown",
                "scope",
                "profile"
            },
            StringComparer.OrdinalIgnoreCase);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Count; index += 2) {
            if (index + 1 >= args.Count || !args[index].StartsWith("--", StringComparison.Ordinal)) {
                throw new ArgumentException("Every Sigma audit option must use --name value syntax.");
            }
            string name = args[index][2..];
            if (!allowed.Contains(name)) {
                throw new ArgumentException($"Option '--{name}' is not supported.");
            }
            if (!values.TryAdd(name, args[index + 1])) {
                throw new ArgumentException($"Option '--{name}' was supplied more than once.");
            }
        }

        string corpus = Required(values, "corpus");
        string commit = Required(values, "commit");
        string outputJson = Required(values, "output-json");
        string outputMarkdown = Required(values, "output-markdown");
        string scopeText = values.TryGetValue("scope", out string? suppliedScope)
            ? suppliedScope
            : "windows";
        AuditScope scope = scopeText.ToLowerInvariant() switch {
            "windows" => AuditScope.Windows,
            "all" => AuditScope.All,
            _ => throw new ArgumentException("Scope must be 'windows' or 'all'.")
        };
        string profileText = values.TryGetValue("profile", out string? suppliedProfile)
            ? suppliedProfile
            : "strict";
        AuditProfile profile = profileText.ToLowerInvariant() switch {
            "strict" => AuditProfile.Strict,
            "windows-sysmon-powershell" => AuditProfile.WindowsSysmonAndPowerShell,
            _ => throw new ArgumentException(
                "Profile must be 'strict' or 'windows-sysmon-powershell'.")
        };
        string fullCorpus = Path.GetFullPath(corpus);
        if (!Directory.Exists(fullCorpus)) {
            throw new ArgumentException($"Sigma corpus path '{fullCorpus}' does not exist.");
        }
        if (commit.Length != 40 || commit.Any(static value => !Uri.IsHexDigit(value))) {
            throw new ArgumentException("Commit must be a full 40-character hexadecimal Git object ID.");
        }
        string fullOutputJson = Path.GetFullPath(outputJson);
        string fullOutputMarkdown = Path.GetFullPath(outputMarkdown);
        if (string.Equals(
            fullOutputJson,
            fullOutputMarkdown,
            StringComparison.OrdinalIgnoreCase)) {

            throw new ArgumentException(
                "Output JSON and Markdown paths must resolve to distinct files.");
        }
        return new AuditOptions(
            fullCorpus,
            commit.ToLowerInvariant(),
            fullOutputJson,
            fullOutputMarkdown,
            scope,
            profile);
    }

    private static string Required(
        IReadOnlyDictionary<string, string> values,
        string name) {

        if (!values.TryGetValue(name, out string? value) || string.IsNullOrWhiteSpace(value)) {
            throw new ArgumentException($"Option '--{name}' is required.");
        }
        return value.Trim();
    }
}
