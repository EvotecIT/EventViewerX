using System.Security.Cryptography;
using System.Text;
using EventViewerX.Sigma;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace EventViewerX.SigmaAudit;

internal static class SigmaCorpusCompiler {
    internal static SigmaFileResult[] Audit(
        string corpusRoot,
        string scopeRoot,
        IReadOnlyList<string> paths,
        SigmaCompilationOptions options) {

        var files = new List<CorpusFile>(paths.Count);
        var documents = new List<CorpusDocument>();
        foreach (string path in paths) {
            CorpusFile file = ReadFile(corpusRoot, scopeRoot, path, documents.Count);
            files.Add(file);
            documents.AddRange(file.Documents);
        }

        if (documents.Count == 0) {
            return files.Select(static file => ResultForUnreadableFile(file)).ToArray();
        }

        string yaml = string.Join(
            Environment.NewLine + "---" + Environment.NewLine,
            files.Where(static file => file.Error == null)
                .Select(static file => file.Yaml));
        SigmaCompilationResult compilation = SigmaRuleCompiler.CompileYaml(yaml, options);
        var compiledSourceIds = new HashSet<string>(
            compilation.Rules.Select(static rule => rule.Definition.SourceId),
            StringComparer.OrdinalIgnoreCase);

        return files.Select(file => ResultForFile(
            file,
            compilation,
            compiledSourceIds)).ToArray();
    }

    private static CorpusFile ReadFile(
        string corpusRoot,
        string scopeRoot,
        string path,
        int firstDocumentIndex) {

        string relativePath = Path
            .GetRelativePath(corpusRoot, path)
            .Replace(Path.DirectorySeparatorChar, '/');
        string category = Path
            .GetRelativePath(scopeRoot, path)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Split('/')[0];
        try {
            string yaml = File.ReadAllText(path);
            var stream = new YamlStream();
            using var reader = new StringReader(yaml);
            stream.Load(reader);
            if (stream.Documents.Count == 0) {
                throw new InvalidDataException("Sigma YAML contains no documents.");
            }

            CorpusDocument[] documents = stream.Documents
                .Select((document, index) => CreateDocument(
                    document,
                    firstDocumentIndex + index))
                .ToArray();
            return new CorpusFile(relativePath, category, yaml, documents, null);
        } catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            InvalidDataException or
            YamlException) {

            return new CorpusFile(
                relativePath,
                category,
                string.Empty,
                Array.Empty<CorpusDocument>(),
                exception.Message);
        }
    }

    private static CorpusDocument CreateDocument(YamlDocument document, int index) {
        string yaml = document.RootNode.ToString();
        string? sourceId = null;
        if (document.RootNode is YamlMappingNode root &&
            (HasKey(root, "detection") || HasKey(root, "correlation"))) {

            sourceId = ScalarValue(root, "id");
            if (string.IsNullOrWhiteSpace(sourceId)) {
                sourceId = "generated-" + Hash(yaml)[..24].ToLowerInvariant();
            }
        }
        return new CorpusDocument(index, sourceId);
    }

    private static SigmaFileResult ResultForFile(
        CorpusFile file,
        SigmaCompilationResult compilation,
        ISet<string> compiledSourceIds) {

        if (file.Error != null) {
            return ResultForUnreadableFile(file);
        }

        var documentIndexes = new HashSet<int>(
            file.Documents.Select(static document => document.Index));
        SigmaDiagnostic[] sourceDiagnostics = compilation.Diagnostics
            .Where(item => documentIndexes.Contains(item.DocumentIndex))
            .ToArray();
        SigmaFileDiagnostic[] diagnostics = sourceDiagnostics
            .Select(item => new SigmaFileDiagnostic {
                Code = item.Code,
                Severity = item.Severity.ToString(),
                Message = item.Message,
                DocumentIndex = item.DocumentIndex - file.Documents[0].Index
            })
            .ToArray();
        var failedDocumentIndexes = new HashSet<int>(sourceDiagnostics
            .Where(static item => item.Severity == SigmaDiagnosticSeverity.Error)
            .Select(static item => item.DocumentIndex));
        int compiledRules = file.Documents.Count(document =>
            document.SourceId != null &&
            compiledSourceIds.Contains(document.SourceId) &&
            !failedDocumentIndexes.Contains(document.Index));
        bool errors = sourceDiagnostics.Any(static item =>
            item.Severity == SigmaDiagnosticSeverity.Error);
        bool warnings = sourceDiagnostics.Any(static item =>
            item.Severity == SigmaDiagnosticSeverity.Warning);
        return new SigmaFileResult {
            Path = file.RelativePath,
            Category = file.Category,
            Status = errors
                ? SigmaFileStatus.Unsupported
                : warnings
                    ? SigmaFileStatus.SupportedWithWarnings
                    : SigmaFileStatus.Supported,
            CompiledRules = compiledRules,
            Diagnostics = diagnostics
        };
    }

    private static SigmaFileResult ResultForUnreadableFile(CorpusFile file) =>
        new() {
            Path = file.RelativePath,
            Category = file.Category,
            Status = SigmaFileStatus.Unsupported,
            CompiledRules = 0,
            Diagnostics = new[] {
                new SigmaFileDiagnostic {
                    Code = "EVXSIGMAAUDIT001",
                    Severity = "Error",
                    Message = file.Error ?? "Sigma YAML contains no compilable documents.",
                    DocumentIndex = 0
                }
            }
        };

    private static bool HasKey(YamlMappingNode root, string key) =>
        root.Children.Keys
            .OfType<YamlScalarNode>()
            .Any(node => string.Equals(node.Value, key, StringComparison.OrdinalIgnoreCase));

    private static string? ScalarValue(YamlMappingNode root, string key) {
        foreach (KeyValuePair<YamlNode, YamlNode> item in root.Children) {
            if (item.Key is YamlScalarNode scalarKey &&
                string.Equals(scalarKey.Value, key, StringComparison.OrdinalIgnoreCase) &&
                item.Value is YamlScalarNode scalarValue) {

                return scalarValue.Value?.Trim();
            }
        }
        return null;
    }

    private static string Hash(string value) {
        using SHA256 sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }

    private sealed record CorpusFile(
        string RelativePath,
        string Category,
        string Yaml,
        IReadOnlyList<CorpusDocument> Documents,
        string? Error);

    private sealed record CorpusDocument(int Index, string? SourceId);
}
