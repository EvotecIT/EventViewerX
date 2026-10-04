using System.Security.Cryptography;
using EventViewerX.Reporting;

namespace EventViewerX.Cli;

internal static partial class Program {
    private static (EventReportPrivacyOptions? Options, byte[]? Key) LoadReportPrivacy(CliArguments options) {
        string? mode = options.Get("privacy");
        if (mode == null) {
            if (options.Has("privacy-key-file") || options.Has("retain-fields") || options.Has("pseudonymize-fields")) {
                throw new ArgumentException("Payload privacy options require --privacy omit or --privacy pseudonymize.");
            }
            return (null, null);
        }
        if (mode is not ("omit" or "pseudonymize")) { throw new ArgumentException("--privacy must be omit or pseudonymize."); }
        if (mode == "omit" && (options.Has("privacy-key-file") || options.Has("pseudonymize-fields"))) {
            throw new ArgumentException("Keyed fields require --privacy pseudonymize.");
        }
        var policy = new EventReportPrivacyOptions {
            RetainedValueFields = options.GetMany("retain-fields"),
            PseudonymizedValueFields = options.GetMany("pseudonymize-fields"),
            PseudonymizeSourceIdentities = mode == "pseudonymize"
        };
        byte[]? key = null;
        try {
            if (mode == "pseudonymize") {
                using FileStream input = File.OpenRead(options.Require("privacy-key-file"));
                key = new byte[32];
                input.ReadExactly(key);
                if (input.ReadByte() != -1) { throw new ArgumentException("The privacy key file must contain exactly 32 binary bytes."); }
            }
            EventReport empty = EventReportEngine.CreateStored(Array.Empty<EventReportRow>(),
                new[] { EventReportSectionSchema.CreateGeneric() });
            EventReportPrivacy.Apply(empty, policy, key); // Validate the policy before querying or writing artifacts.
            return (policy, key);
        } catch {
            if (key != null) { CryptographicOperations.ZeroMemory(key); }
            throw;
        }
    }

    private static void ValidateReportOutputPaths(CliArguments options) {
        StringComparer comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var inputs = new HashSet<string>(options.GetMany("path").Select(ComparisonPath), comparer);
        foreach (string name in new[] { "store", "write-store", "context-store", "definition", "mail-profile", "privacy-key-file" }) {
            if (options.Get(name) is string path) { inputs.Add(ComparisonPath(path)); }
        }
        if (options.Get("where") is string predicate && File.Exists(predicate)) { inputs.Add(ComparisonPath(predicate)); }
        var outputs = new HashSet<string>(comparer);
        bool hasReportOutput = false;
        foreach (string name in new[] { "html", "excel", "csv", "email-html", "bundle", "summary-file" }) {
            if (options.Get(name) is not string path) { continue; }
            Add(path);
            if (name != "summary-file") { hasReportOutput = true; }
            if (name == "email-html") { Add(Path.ChangeExtension(path, ".txt")); }
            if (name == "csv" && Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase)) { Add(path + ".metadata.json"); }
        }
        if (!hasReportOutput && !options.Has("mail-profile")) { throw new ArgumentException("report requires a rendered output, --bundle, or --mail-profile."); }
        if (options.Has("bundle-max-bytes") && !options.Has("bundle")) { throw new ArgumentException("--bundle-max-bytes requires --bundle."); }
        if (options.Get("bundle") is string bundle && File.Exists(Path.GetFullPath(bundle))) {
            throw new ArgumentException("The evidence bundle already exists; choose a new destination.");
        }
        void Add(string path) {
            string full = ComparisonPath(path);
            if (inputs.Contains(full) || !outputs.Add(full)) {
                throw new ArgumentException("Report outputs and sidecars must be separate from every input, key, history file and other output.");
            }
        }
    }

    private static string ComparisonPath(string path) {
        if (OperatingSystem.IsWindows()) {
            if (path.StartsWith("\\\\?\\UNC\\", StringComparison.OrdinalIgnoreCase)) { path = "\\\\" + path.Substring(8); }
            else if (path.StartsWith("\\\\?\\", StringComparison.Ordinal)) { path = path.Substring(4); }
        }
        return Path.GetFullPath(path);
    }

    private static void SaveEvidenceBundle(EventReport report, string path, CliArguments options,
        string? queryJson, string? definitionJson, EventReportPrivacyOptions? privacy, CancellationToken token) {
        string destination = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var artifacts = new Dictionary<string, Stream>();
        try {
            foreach ((string option, string name) in new[] { ("html", "report.html"), ("excel", "report.xlsx"), ("csv", "report.csv"), ("email-html", "email.html") }) {
                if (options.Get(option) is string artifactPath) {
                    string artifactName = option == "csv" && Path.GetExtension(artifactPath).Equals(".zip", StringComparison.OrdinalIgnoreCase)
                        ? "report-csv.zip" : name;
                    artifacts.Add(artifactName, File.OpenRead(artifactPath));
                    if (option == "email-html") { artifacts.Add("email.txt", File.OpenRead(Path.ChangeExtension(artifactPath, ".txt"))); }
                    if (option == "csv" && artifactName == "report.csv") { artifacts.Add("report.csv.metadata.json", File.OpenRead(artifactPath + ".metadata.json")); }
                }
            }
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                EventEvidenceBundle.Write(report, output, new EventEvidenceBundleOptions {
                    QueryJson = queryJson, DefinitionJson = definitionJson, PrivacyPolicy = privacy, Reports = artifacts,
                    MaximumUncompressedBytes = options.GetLong("bundle-max-bytes", 256L * 1024 * 1024)
                }, token);
            }
            token.ThrowIfCancellationRequested();
            File.Move(temporary, destination);
        } finally {
            foreach (Stream artifact in artifacts.Values) { artifact.Dispose(); }
            if (File.Exists(temporary)) {
                try { File.Delete(temporary); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
                    Console.Error.WriteLine("Temporary evidence file could not be removed: " + temporary);
                }
            }
        }
    }

    private static int VerifyEvidenceBundle(CliArguments options) {
        if (options.Subcommand != "verify") { throw new ArgumentException("Use bundle verify --path FILE.zip."); }
        using var cancellation = new ConsoleQueryCancellation();
        using FileStream source = File.OpenRead(options.Require("path"));
        return WriteJson(EventEvidenceBundle.Verify(source, options.GetLong("max-bytes", 256L * 1024 * 1024), cancellation.Token));
    }
}
