using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace EventViewerX.Sigma;

/// <summary>Parses, validates, and compiles supported Sigma 2.x YAML into native EventViewerX detections.</summary>
public static class SigmaRuleCompiler {
    /// <summary>Compiles one or more YAML documents separated by <c>---</c>.</summary>
    public static SigmaCompilationResult CompileYaml(string yaml) {
        if (string.IsNullOrWhiteSpace(yaml)) {
            throw new ArgumentException("Sigma YAML cannot be empty.", nameof(yaml));
        }
        var diagnostics = new List<SigmaDiagnostic>();
        var stream = new YamlStream();
        try {
            stream.Load(new StringReader(yaml));
        } catch (YamlException exception) {
            diagnostics.Add(new SigmaDiagnostic(
                "EVXSIGMA001",
                SigmaDiagnosticSeverity.Error,
                "Sigma YAML is invalid: " + exception.Message,
                0));
            return new SigmaCompilationResult(Array.Empty<IEventDetectionRule>(), diagnostics);
        }

        var baseRules = new List<CompiledBaseRule>();
        var correlations = new List<CompiledCorrelation>();
        for (int index = 0; index < stream.Documents.Count; index++) {
            if (stream.Documents[index].RootNode is not YamlMappingNode root) {
                diagnostics.Add(Error("EVXSIGMA002", "A Sigma document root must be a mapping.", index));
                continue;
            }
            if (TryGet(root, "detection", out _)) {
                TryCompileBaseRule(root, index, diagnostics, baseRules);
            } else if (!TryGet(root, "correlation", out _)) {
                diagnostics.Add(Error(
                    "EVXSIGMA003",
                    "EventViewerX supports Sigma detection and correlation documents; filters must be expressed as EVX tuning suppressions.",
                    index));
            }
        }

        for (int index = 0; index < stream.Documents.Count; index++) {
            if (stream.Documents[index].RootNode is YamlMappingNode root && TryGet(root, "correlation", out _)) {
                TryCompileCorrelation(root, index, baseRules, diagnostics, correlations);
            }
        }

        var referenced = new HashSet<CompiledBaseRule>();
        foreach (CompiledCorrelation correlation in correlations) {
            if (!correlation.GenerateBaseRules) {
                referenced.UnionWith(correlation.ReferencedRules);
            }
        }
        IEventDetectionRule[] rules = baseRules
            .Where(rule => !referenced.Contains(rule))
            .Select(static rule => (IEventDetectionRule)new EventDetectionRule(rule.Definition))
            .Concat(correlations.Select(static correlation =>
                (IEventDetectionRule)new EventDetectionRule(correlation.Definition)))
            .ToArray();
        return new SigmaCompilationResult(rules, diagnostics);
    }

    /// <summary>Loads and compiles Sigma YAML from disk.</summary>
    public static SigmaCompilationResult Load(string path) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Sigma path cannot be empty.", nameof(path));
        }
        return CompileYaml(File.ReadAllText(Path.GetFullPath(path)));
    }

    /// <summary>Creates a native integrity-protected pack from fully supported Sigma input.</summary>
    public static EventDetectionPack CompilePack(
        string yaml,
        string packId,
        string version,
        IEnumerable<string>? authors = null,
        string? license = null) {

        SigmaCompilationResult result = CompileYaml(yaml);
        if (!result.IsSupported) {
            _ = result.CompilePlan();
        }
        return EventDetectionPack.Create(
            packId,
            version,
            result.Rules.Select(static rule => rule.Definition),
            authors,
            license);
    }

    private static void TryCompileBaseRule(
        YamlMappingNode root,
        int documentIndex,
        ICollection<SigmaDiagnostic> diagnostics,
        ICollection<CompiledBaseRule> rules) {

        try {
            string title = RequiredText(root, "title");
            string sourceId = OptionalText(root, "id");
            string name = OptionalText(root, "name");
            string sourceHash = Hash(root.ToString());
            if (sourceId.Length == 0) {
                sourceId = "generated-" + sourceHash.Substring(0, 24).ToLowerInvariant();
                diagnostics.Add(new SigmaDiagnostic(
                    "EVXSIGMA010",
                    SigmaDiagnosticSeverity.Warning,
                    $"Sigma rule '{title}' has no id; a deterministic source-derived ID was assigned.",
                    documentIndex));
            }
            YamlMappingNode detection = Mapping(root, "detection");
            var selections = new Dictionary<string, EventPredicate>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<YamlNode, YamlNode> item in detection.Children) {
                string selectionName = Scalar(item.Key, "Sigma detection key");
                if (string.Equals(selectionName, "condition", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(selectionName, "timeframe", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }
                selections.Add(selectionName, SigmaSelectionCompiler.Compile(item.Value));
            }
            string condition = RequiredText(detection, "condition");
            EventPredicate predicate = SigmaConditionCompiler.Compile(condition, selections);
            LogSourceSelectors selectors = CompileLogSource(root, documentIndex, diagnostics);
            string status = OptionalText(root, "status");
            var definition = new EventDetectionRuleDefinition {
                RuleId = "SIGMA-" + sourceId,
                Version = "1.0.0",
                Title = title,
                Description = OptionalText(root, "description"),
                Severity = ParseSeverity(OptionalText(root, "level")),
                Confidence = Confidence(status),
                SourceKind = "Sigma",
                SourceId = sourceId,
                SourceStatus = status,
                SourceHash = sourceHash,
                License = OptionalText(root, "license"),
                Kind = EventDetectionRuleKind.Stateless,
                Channels = selectors.Channels,
                Providers = selectors.Providers,
                Predicate = predicate,
                Tags = TextList(root, "tags"),
                FalsePositives = TextList(root, "falsepositives"),
                References = TextList(root, "references")
            };
            rules.Add(new CompiledBaseRule(sourceId, name, definition));
        } catch (SigmaConditionException exception) {
            diagnostics.Add(Error(exception.Code, exception.Message, documentIndex));
        } catch (InvalidDataException exception) {
            diagnostics.Add(Error("EVXSIGMA011", exception.Message, documentIndex));
        } catch (ArgumentException exception) {
            diagnostics.Add(Error("EVXSIGMA012", exception.Message, documentIndex));
        }
    }

    private static void TryCompileCorrelation(
        YamlMappingNode root,
        int documentIndex,
        IReadOnlyList<CompiledBaseRule> baseRules,
        ICollection<SigmaDiagnostic> diagnostics,
        ICollection<CompiledCorrelation> correlations) {

        try {
            string title = RequiredText(root, "title");
            string sourceId = OptionalText(root, "id");
            string sourceHash = Hash(root.ToString());
            if (sourceId.Length == 0) {
                sourceId = "generated-" + sourceHash.Substring(0, 24).ToLowerInvariant();
            }
            YamlMappingNode correlation = Mapping(root, "correlation");
            if (TryGet(correlation, "aliases", out _)) {
                throw new SigmaConditionException(
                    "EVXSIGMA200",
                    "Sigma correlation aliases are rejected until EVX can preserve per-step alias joins exactly.");
            }
            string[] references = TextList(correlation, "rules");
            if (references.Length == 0) {
                throw new InvalidDataException("Sigma correlation.rules requires at least one rule reference.");
            }
            CompiledBaseRule[] related = references.Select(reference => ResolveBaseRule(baseRules, reference)).ToArray();
            string type = RequiredText(correlation, "type").ToLowerInvariant();
            TimeSpan window = ParseTimespan(RequiredText(correlation, "timespan"));
            string[] groupBy = TextList(correlation, "group-by");
            if (groupBy.Length > 1) {
                throw new SigmaConditionException(
                    "EVXSIGMA201",
                    "EventViewerX currently supports exactly one Sigma correlation group-by field; multiple fields are rejected.");
            }
            EventDetectionRuleDefinition definition = CorrelationDefinition(
                root,
                correlation,
                sourceId,
                sourceHash,
                title,
                type,
                window,
                groupBy.SingleOrDefault(),
                related);
            bool generate = OptionalBoolean(correlation, "generate");
            correlations.Add(new CompiledCorrelation(definition, related, generate));
        } catch (SigmaConditionException exception) {
            diagnostics.Add(Error(exception.Code, exception.Message, documentIndex));
        } catch (InvalidDataException exception) {
            diagnostics.Add(Error("EVXSIGMA202", exception.Message, documentIndex));
        } catch (ArgumentException exception) {
            diagnostics.Add(Error("EVXSIGMA203", exception.Message, documentIndex));
        }
    }

    private static EventDetectionRuleDefinition CorrelationDefinition(
        YamlMappingNode root,
        YamlMappingNode correlation,
        string sourceId,
        string sourceHash,
        string title,
        string type,
        TimeSpan window,
        string? groupBy,
        IReadOnlyList<CompiledBaseRule> related) {

        var definition = new EventDetectionRuleDefinition {
            RuleId = "SIGMA-" + sourceId,
            Version = "1.0.0",
            Title = title,
            Description = OptionalText(root, "description"),
            Severity = ParseSeverity(OptionalText(root, "level")),
            Confidence = Confidence(OptionalText(root, "status")),
            SourceKind = "Sigma",
            SourceId = sourceId,
            SourceStatus = OptionalText(root, "status"),
            SourceHash = sourceHash,
            License = OptionalText(root, "license"),
            Window = window,
            GroupBy = MapCorrelationField(groupBy),
            Tags = TextList(root, "tags"),
            FalsePositives = TextList(root, "falsepositives"),
            References = TextList(root, "references")
        };
        if (type is "temporal" or "temporal_ordered") {
            definition.Kind = type == "temporal"
                ? EventDetectionRuleKind.Temporal
                : EventDetectionRuleKind.OrderedTemporal;
            definition.Steps = related.Select(rule => new EventDetectionStepDefinition {
                Name = string.IsNullOrWhiteSpace(rule.Name) ? rule.SourceId : rule.Name,
                EventTypes = rule.Definition.EventTypes,
                EventIds = rule.Definition.EventIds,
                Channels = rule.Definition.Channels,
                Providers = rule.Definition.Providers,
                Predicate = rule.Definition.Predicate
            }).ToArray();
            return definition;
        }
        if (related.Count != 1) {
            throw new SigmaConditionException(
                "EVXSIGMA204",
                "Event-count and value-count correlations with multiple base rules are rejected to avoid selector cross-product weakening.");
        }
        CompiledBaseRule source = related[0];
        definition.EventTypes = source.Definition.EventTypes;
        definition.EventIds = source.Definition.EventIds;
        definition.Channels = source.Definition.Channels;
        definition.Providers = source.Definition.Providers;
        definition.Predicate = source.Definition.Predicate;
        (int threshold, string? distinctField) = ParseCorrelationCondition(correlation, type);
        definition.Threshold = threshold;
        if (type == "event_count") {
            definition.Kind = EventDetectionRuleKind.Threshold;
        } else if (type == "value_count") {
            definition.Kind = EventDetectionRuleKind.DistinctValue;
            definition.DistinctBy = MapCorrelationField(distinctField);
        } else {
            throw new SigmaConditionException(
                "EVXSIGMA205",
                $"Sigma correlation type '{type}' is unsupported. Supported types: event_count, value_count, temporal, temporal_ordered.");
        }
        return definition;
    }

    private static (int Threshold, string? Field) ParseCorrelationCondition(
        YamlMappingNode correlation,
        string type) {

        YamlMappingNode condition = Mapping(correlation, "condition");
        string? field = OptionalText(condition, "field");
        int? threshold = null;
        foreach (string comparison in new[] { "gte", "gt", "eq", "lt", "lte", "neq" }) {
            if (!TryGet(condition, comparison, out YamlNode? node)) {
                continue;
            }
            if (threshold.HasValue) {
                throw new InvalidDataException("Sigma correlation.condition must contain exactly one comparison.");
            }
            if (!int.TryParse(Scalar(node!, "Sigma correlation threshold"), NumberStyles.None, CultureInfo.InvariantCulture, out int value) ||
                value < 1) {
                throw new InvalidDataException("Sigma correlation threshold must be a positive integer.");
            }
            threshold = comparison switch {
                "gte" => value,
                "gt" when value < int.MaxValue => value + 1,
                _ => throw new SigmaConditionException(
                    "EVXSIGMA206",
                    $"Sigma correlation comparison '{comparison}' cannot be preserved by the EVX at-least threshold engine.")
            };
        }
        if (!threshold.HasValue) {
            throw new InvalidDataException("Sigma correlation.condition requires gte or gt.");
        }
        if (type == "value_count" && string.IsNullOrWhiteSpace(field)) {
            throw new InvalidDataException("Sigma value_count correlation requires condition.field.");
        }
        return (threshold.Value, field);
    }

    private static LogSourceSelectors CompileLogSource(
        YamlMappingNode root,
        int documentIndex,
        ICollection<SigmaDiagnostic> diagnostics) {

        if (!TryGet(root, "logsource", out YamlNode? node) || node is not YamlMappingNode logSource) {
            diagnostics.Add(new SigmaDiagnostic(
                "EVXSIGMA020",
                SigmaDiagnosticSeverity.Warning,
                "Sigma rule has no logsource; EVX will rely on exact managed predicate verification.",
                documentIndex));
            return new LogSourceSelectors(Array.Empty<string>(), Array.Empty<string>());
        }
        string product = OptionalText(logSource, "product");
        if (product.Length != 0 && !string.Equals(product, "windows", StringComparison.OrdinalIgnoreCase)) {
            throw new SigmaConditionException(
                "EVXSIGMA021",
                $"Sigma logsource product '{product}' is not supported by the Windows event observation adapter.");
        }
        string service = OptionalText(logSource, "service");
        string category = OptionalText(logSource, "category");
        string? channel = service.ToLowerInvariant() switch {
            "security" => "Security",
            "system" => "System",
            "application" => "Application",
            "powershell" => "Microsoft-Windows-PowerShell/Operational",
            "powershell-classic" => "Windows PowerShell",
            "windefend" or "defender" => "Microsoft-Windows-Windows Defender/Operational",
            "sysmon" => "Microsoft-Windows-Sysmon/Operational",
            "" => null,
            _ => throw new SigmaConditionException(
                "EVXSIGMA022",
                $"Sigma Windows logsource service '{service}' has no lossless EventViewerX channel mapping.")
        };
        if (service.Length == 0 && category.Length != 0 &&
            !string.Equals(category, "process_creation", StringComparison.OrdinalIgnoreCase)) {
            diagnostics.Add(new SigmaDiagnostic(
                "EVXSIGMA023",
                SigmaDiagnosticSeverity.Warning,
                $"Sigma category '{category}' has no lossless EventViewerX channel mapping; exact rule predicates remain enforced without a channel prefilter.",
                documentIndex));
        }
        return new LogSourceSelectors(
            channel == null ? Array.Empty<string>() : new[] { channel },
            Array.Empty<string>());
    }

    private static CompiledBaseRule ResolveBaseRule(
        IEnumerable<CompiledBaseRule> rules,
        string reference) {

        CompiledBaseRule[] matches = rules.Where(rule =>
            string.Equals(rule.SourceId, reference, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(rule.Name, reference, StringComparison.OrdinalIgnoreCase)).ToArray();
        return matches.Length switch {
            1 => matches[0],
            0 => throw new InvalidDataException($"Sigma correlation references unknown rule '{reference}'."),
            _ => throw new InvalidDataException($"Sigma correlation reference '{reference}' is ambiguous.")
        };
    }

    private static TimeSpan ParseTimespan(string value) {
        if (value.Length < 2 ||
            !int.TryParse(value.Substring(0, value.Length - 1), NumberStyles.None, CultureInfo.InvariantCulture, out int count) ||
            count <= 0) {
            throw new InvalidDataException($"Sigma timespan '{value}' is invalid.");
        }
        TimeSpan result = value[value.Length - 1] switch {
            's' => TimeSpan.FromSeconds(count),
            'm' => TimeSpan.FromMinutes(count),
            'h' => TimeSpan.FromHours(count),
            'd' => TimeSpan.FromDays(count),
            'w' => TimeSpan.FromDays(count * 7D),
            _ => throw new SigmaConditionException(
                "EVXSIGMA207",
                "EventViewerX supports bounded Sigma timespans in seconds, minutes, hours, days, or weeks; months and years are rejected.")
        };
        if (result > TimeSpan.FromDays(30)) {
            throw new SigmaConditionException("EVXSIGMA208", "Sigma correlation timespan exceeds the 30-day EVX safety bound.");
        }
        return result;
    }

    private static EventDetectionSeverity ParseSeverity(string value) => value.ToLowerInvariant() switch {
        "informational" or "" => EventDetectionSeverity.Informational,
        "low" => EventDetectionSeverity.Low,
        "medium" => EventDetectionSeverity.Medium,
        "high" => EventDetectionSeverity.High,
        "critical" => EventDetectionSeverity.Critical,
        _ => throw new InvalidDataException($"Sigma level '{value}' is not supported.")
    };

    private static int Confidence(string status) => status.ToLowerInvariant() switch {
        "stable" => 90,
        "test" => 70,
        "experimental" => 50,
        "deprecated" or "unsupported" => 20,
        _ => 60
    };

    private static string? MapCorrelationField(string? field) {
        if (string.IsNullOrWhiteSpace(field)) {
            return null;
        }
        return field!.ToLowerInvariant() switch {
            "targetusername" => "ObjectAffected",
            "username" or "user" => "Who",
            "computer" or "computername" => "SourceComputer",
            "sourceip" or "ipaddress" => "IpAddress",
            _ => field.Trim()
        };
    }

    private static YamlMappingNode Mapping(YamlMappingNode parent, string key) {
        if (!TryGet(parent, key, out YamlNode? node) || node is not YamlMappingNode mapping) {
            throw new InvalidDataException($"Sigma {key} must be a mapping.");
        }
        return mapping;
    }

    private static string RequiredText(YamlMappingNode mapping, string key) {
        string value = OptionalText(mapping, key);
        if (value.Length == 0) {
            throw new InvalidDataException($"Sigma {key} is required.");
        }
        return value;
    }

    private static string OptionalText(YamlMappingNode mapping, string key) =>
        TryGet(mapping, key, out YamlNode? node) && node is YamlScalarNode scalar
            ? scalar.Value?.Trim() ?? string.Empty
            : string.Empty;

    private static string[] TextList(YamlMappingNode mapping, string key) {
        if (!TryGet(mapping, key, out YamlNode? node)) {
            return Array.Empty<string>();
        }
        if (node is YamlScalarNode scalar) {
            string? scalarValue = scalar.Value;
            return string.IsNullOrWhiteSpace(scalarValue) ? Array.Empty<string>() : new[] { scalarValue!.Trim() };
        }
        if (node is not YamlSequenceNode sequence) {
            throw new InvalidDataException($"Sigma {key} must be a scalar or list.");
        }
        return sequence.Children.Select(item => Scalar(item, $"Sigma {key} item").Trim()).ToArray();
    }

    private static bool OptionalBoolean(YamlMappingNode mapping, string key) =>
        TryGet(mapping, key, out YamlNode? node) &&
        bool.TryParse(Scalar(node!, $"Sigma {key}"), out bool value) && value;

    private static bool TryGet(YamlMappingNode mapping, string key, out YamlNode? value) {
        foreach (KeyValuePair<YamlNode, YamlNode> item in mapping.Children) {
            if (item.Key is YamlScalarNode scalar && string.Equals(scalar.Value, key, StringComparison.OrdinalIgnoreCase)) {
                value = item.Value;
                return true;
            }
        }
        value = null;
        return false;
    }

    private static string Scalar(YamlNode node, string description) {
        if (node is not YamlScalarNode scalar || scalar.Value == null) {
            throw new InvalidDataException(description + " must be a scalar.");
        }
        return scalar.Value;
    }

    private static string Hash(string value) {
        using SHA256 algorithm = SHA256.Create();
        return string.Concat(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value))
            .Select(static item => item.ToString("X2", CultureInfo.InvariantCulture)));
    }

    private static SigmaDiagnostic Error(string code, string message, int documentIndex) =>
        new(code, SigmaDiagnosticSeverity.Error, message, documentIndex);

    private sealed class CompiledBaseRule {
        internal CompiledBaseRule(string sourceId, string name, EventDetectionRuleDefinition definition) {
            SourceId = sourceId;
            Name = name;
            Definition = definition;
        }

        internal string SourceId { get; }
        internal string Name { get; }
        internal EventDetectionRuleDefinition Definition { get; }
    }

    private sealed class CompiledCorrelation {
        internal CompiledCorrelation(
            EventDetectionRuleDefinition definition,
            IReadOnlyList<CompiledBaseRule> referencedRules,
            bool generateBaseRules) {

            Definition = definition;
            ReferencedRules = referencedRules;
            GenerateBaseRules = generateBaseRules;
        }

        internal EventDetectionRuleDefinition Definition { get; }
        internal IReadOnlyList<CompiledBaseRule> ReferencedRules { get; }
        internal bool GenerateBaseRules { get; }
    }

    private readonly struct LogSourceSelectors {
        internal LogSourceSelectors(string[] channels, string[] providers) {
            Channels = channels;
            Providers = providers;
        }

        internal string[] Channels { get; }
        internal string[] Providers { get; }
    }
}
