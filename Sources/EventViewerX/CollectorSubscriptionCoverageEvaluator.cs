using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace EventViewerX;

internal sealed class CollectorSubscriptionCoverageResult {
    internal CollectorSubscriptionCoverageResult(
        EventReadinessStatus status,
        string evidence,
        string remediation,
        EventReadinessDiagnosticKind diagnosticKind) {

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

internal static class CollectorSubscriptionCoverageEvaluator {
    private static readonly Regex EventIdValue = new(
        @"EventID\s*=\s*(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ProviderNameValue = new(
        "@Name\\s*=\\s*(?:'(?<single>[^']*)'|\"(?<double>[^\"]*)\")",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex QuotedLiteral = new(
        "'[^']*'|\"[^\"]*\"",
        RegexOptions.CultureInvariant);

    internal static CollectorSubscriptionCoverageResult Evaluate(
        CollectorSubscriptionSnapshot subscription,
        IReadOnlyList<EventSourceDefinition> sources) {

        if (string.IsNullOrWhiteSpace(subscription.RawXml)) {
            return Unknown(
                "The subscription query paths cannot be proven because the raw subscription XML was not available.",
                "Read the complete subscription locally and compare its QueryList with the selected EventViewerX types.");
        }
        if (!TryReadQueryList(subscription.RawXml!, out XDocument? queryList, out string? error)) {
            return Unknown(
                "The subscription QueryList could not be parsed: " + error,
                "Inspect the stored subscription XML and replace an invalid or unsupported query definition.");
        }

        var missing = new List<string>();
        var uncertain = new List<string>();
        foreach (EventSourceDefinition source in sources) {
            SourceCoverage coverage = EvaluateSource(queryList!, source, sources);
            if (coverage == SourceCoverage.Missing) {
                missing.Add(DescribeSource(source));
            } else if (coverage == SourceCoverage.Unknown) {
                uncertain.Add(DescribeSource(source));
            }
        }
        if (missing.Count > 0) {
            return new CollectorSubscriptionCoverageResult(
                EventReadinessStatus.Fail,
                "The named subscription does not select: " + string.Join("; ", missing) + ".",
                "Regenerate or update the subscription QueryList from the selected EventViewerX types.",
                EventReadinessDiagnosticKind.Missing);
        }
        if (uncertain.Count > 0) {
            return Unknown(
                "Coverage could not be proven for complex query clauses: " + string.Join("; ", uncertain) + ".",
                "Use an EventViewerX-generated event-ID QueryList or verify the complex predicates manually.");
        }
        return new CollectorSubscriptionCoverageResult(
            EventReadinessStatus.Pass,
            $"The named subscription QueryList covers all {sources.Count} selected channel/event-ID source definition(s).",
            string.Empty,
            EventReadinessDiagnosticKind.None);
    }

    private static SourceCoverage EvaluateSource(
        XDocument queryList,
        EventSourceDefinition source,
        IReadOnlyList<EventSourceDefinition> allSources) {
        XElement[] queries = queryList
            .Descendants()
            .Where(static element => string.Equals(element.Name.LocalName, "Query", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (queries.Length == 0) {
            return SourceCoverage.Missing;
        }

        bool uncertain = false;
        IReadOnlyList<string?> expectedProviders = source.ProviderNames.Count == 0
            ? new string?[] { null }
            : source.ProviderNames.Select(static provider => (string?)provider).ToArray();
        foreach (int eventId in source.EventIds) {
            string[] expectedProvidersForEvent = allSources
                .Where(candidate =>
                    string.Equals(
                        candidate.LogName,
                        source.LogName,
                        StringComparison.OrdinalIgnoreCase) &&
                    candidate.EventIds.Contains(eventId))
                .SelectMany(static candidate => candidate.ProviderNames)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            foreach (string? providerName in expectedProviders) {
                bool covered = false;
                bool unknown = false;
                foreach (XElement query in queries) {
                    SourceCoverage queryCoverage = EvaluateQuery(
                        query,
                        source,
                        eventId,
                        providerName,
                        expectedProvidersForEvent);
                    if (queryCoverage == SourceCoverage.Overbroad) {
                        return SourceCoverage.Missing;
                    }
                    covered |= queryCoverage == SourceCoverage.Covered;
                    unknown |= queryCoverage == SourceCoverage.Unknown;
                }
                if (!covered && !unknown) {
                    return SourceCoverage.Missing;
                }
                uncertain |= unknown;
            }
        }
        return uncertain ? SourceCoverage.Unknown : SourceCoverage.Covered;
    }

    private static SourceCoverage EvaluateQuery(
        XElement query,
        EventSourceDefinition source,
        int eventId,
        string? providerName,
        IReadOnlyList<string> expectedProvidersForEvent) {

        string[] selections = query
            .Descendants()
            .Where(static element => string.Equals(element.Name.LocalName, "Select", StringComparison.OrdinalIgnoreCase))
            .Where(element => string.Equals(ResolvePath(element), source.LogName, StringComparison.OrdinalIgnoreCase))
            .Select(static element => element.Value.Trim())
            .ToArray();
        if (selections.Length == 0) {
            return SourceCoverage.Missing;
        }
        string[] suppressions = query
            .Descendants()
            .Where(static element => string.Equals(element.Name.LocalName, "Suppress", StringComparison.OrdinalIgnoreCase))
            .Where(element => string.Equals(ResolvePath(element), source.LogName, StringComparison.OrdinalIgnoreCase))
            .Select(static element => element.Value.Trim())
            .ToArray();

        string[] referencedProviders = selections
            .Concat(suppressions)
            .SelectMany(ExtractProviderNames)
            .Concat(expectedProvidersForEvent)
            .Where(static provider => !string.IsNullOrWhiteSpace(provider))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string sentinelProvider = CreateSentinelProvider(referencedProviders);
        string[] providerClasses = referencedProviders
            .Append(sentinelProvider)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (providerName == null) {
            bool unknown = false;
            foreach (string providerClass in providerClasses) {
                ClauseCoverage coverage = EvaluateNetQuery(
                    selections,
                    suppressions,
                    eventId,
                    providerClass);
                if (coverage == ClauseCoverage.NotSelected) {
                    return SourceCoverage.Missing;
                }
                unknown |= coverage == ClauseCoverage.Unknown;
            }
            return unknown ? SourceCoverage.Unknown : SourceCoverage.Covered;
        }

        ClauseCoverage expectedCoverage = EvaluateNetQuery(
            selections,
            suppressions,
            eventId,
            providerName);
        bool uncertain = expectedCoverage == ClauseCoverage.Unknown;
        foreach (string nonExpectedProvider in providerClasses.Where(candidate =>
                     !expectedProvidersForEvent.Contains(candidate, StringComparer.OrdinalIgnoreCase))) {
            ClauseCoverage coverage = EvaluateNetQuery(
                selections,
                suppressions,
                eventId,
                nonExpectedProvider);
            if (coverage == ClauseCoverage.Selected) {
                return SourceCoverage.Overbroad;
            }
            uncertain |= coverage == ClauseCoverage.Unknown;
        }
        if (uncertain) {
            return SourceCoverage.Unknown;
        }
        return expectedCoverage == ClauseCoverage.Selected
            ? SourceCoverage.Covered
            : SourceCoverage.Missing;
    }

    private static ClauseCoverage EvaluateNetQuery(
        IReadOnlyList<string> selections,
        IReadOnlyList<string> suppressions,
        int eventId,
        string providerName) {

        bool selected = false;
        bool uncertainSelection = false;
        foreach (string expression in selections) {
            if (string.Equals(expression, "*", StringComparison.Ordinal)) {
                selected = true;
            } else if (TryEvaluateSupportedSystemExpression(
                           expression,
                           eventId,
                           providerName,
                           out bool matches)) {
                selected |= matches;
            } else {
                uncertainSelection = true;
            }
        }
        bool uncertainSuppression = false;
        foreach (string expression in suppressions) {
            if (string.Equals(expression, "*", StringComparison.Ordinal)) {
                return ClauseCoverage.NotSelected;
            }
            if (TryEvaluateSupportedSystemExpression(
                    expression,
                    eventId,
                    providerName,
                    out bool suppressed)) {
                if (suppressed) {
                    return ClauseCoverage.NotSelected;
                }
            } else {
                uncertainSuppression = true;
            }
        }
        if (!selected) {
            return uncertainSelection ? ClauseCoverage.Unknown : ClauseCoverage.NotSelected;
        }
        return uncertainSelection || uncertainSuppression
            ? ClauseCoverage.Unknown
            : ClauseCoverage.Selected;
    }

    private static bool TryReadQueryList(
        string rawXml,
        out XDocument? queryList,
        out string? error) {

        queryList = null;
        error = null;
        try {
            XDocument outer = ParseXml(rawXml);
            if (string.Equals(outer.Root?.Name.LocalName, "QueryList", StringComparison.OrdinalIgnoreCase)) {
                queryList = outer;
                return true;
            }
            if (!string.Equals(outer.Root?.Name.LocalName, "Subscription", StringComparison.OrdinalIgnoreCase)) {
                error = "The root is neither Subscription nor QueryList.";
                return false;
            }
            XElement? query = outer.Root!
                .Elements()
                .FirstOrDefault(static element => string.Equals(element.Name.LocalName, "Query", StringComparison.OrdinalIgnoreCase));
            if (query == null || string.IsNullOrWhiteSpace(query.Value)) {
                error = "The subscription does not contain an embedded QueryList.";
                return false;
            }
            queryList = ParseXml(query.Value.Trim());
            if (!string.Equals(queryList.Root?.Name.LocalName, "QueryList", StringComparison.OrdinalIgnoreCase)) {
                error = "The embedded query root is not QueryList.";
                queryList = null;
                return false;
            }
            return true;
        } catch (XmlException exception) {
            error = exception.Message;
            return false;
        }
    }

    private static XDocument ParseXml(string xml) {
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreWhitespace = true
        });
        return XDocument.Load(reader, LoadOptions.None);
    }

    private static bool TryEvaluateSupportedSystemExpression(
        string expression,
        int eventId,
        string providerName,
        out bool matches) {

        matches = false;
        string normalized = Regex.Replace(expression, @"[\s()]", string.Empty);
        foreach (Match match in EventIdValue.Matches(normalized)) {
            if (!int.TryParse(
                    match.Groups[1].Value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out _)) {
                return false;
            }
        }

        MatchCollection providerMatches = ProviderNameValue.Matches(expression);
        bool mentionsProvider = Regex.IsMatch(
            expression,
            @"\bProvider\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        int nameComparisons = Regex.Matches(
            expression,
            @"@Name\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count;
        if (mentionsProvider != (providerMatches.Count > 0) || nameComparisons != providerMatches.Count) {
            return false;
        }

        if (!HasOnlySupportedSystemSelectionSyntax(expression)) {
            return false;
        }
        return TryEvaluateXPath(expression, eventId, providerName, out matches);
    }

    private static IEnumerable<string> ExtractProviderNames(string expression) =>
        ProviderNameValue.Matches(expression)
            .Cast<Match>()
            .Select(static match => match.Groups["single"].Success
                ? match.Groups["single"].Value
                : match.Groups["double"].Value);

    private static string CreateSentinelProvider(IReadOnlyList<string> selectedProviders) {
        string sentinel = "__EventViewerX_Unlisted_Provider__";
        while (selectedProviders.Contains(sentinel, StringComparer.OrdinalIgnoreCase)) {
            sentinel += "_";
        }
        return sentinel;
    }

    private static bool TryEvaluateXPath(
        string expression,
        int eventId,
        string providerName,
        out bool matches) {

        matches = false;
        try {
            var document = new XDocument(
                new XElement(
                    "Event",
                    new XElement(
                        "System",
                        new XElement("Provider", new XAttribute("Name", providerName)),
                        new XElement("EventID", eventId))));
            object result = document.CreateNavigator()!.Evaluate(expression);
            matches = result switch {
                bool boolean => boolean,
                XPathNodeIterator iterator => iterator.MoveNext(),
                _ => false
            };
            return true;
        } catch (XPathException) {
            return false;
        }
    }

    private static bool HasOnlySupportedSystemSelectionSyntax(string expression) {
        string scrubbed = QuotedLiteral.Replace(expression, "''");
        scrubbed = Regex.Replace(scrubbed, @"[()]", string.Empty);
        scrubbed = EventIdValue.Replace(scrubbed, string.Empty);
        scrubbed = Regex.Replace(
            scrubbed,
            "@Name\\s*=\\s*(?:''|\"\")",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        scrubbed = Regex.Replace(
            scrubbed,
            @"\b(?:System|Provider|and|or)\b",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        scrubbed = Regex.Replace(scrubbed, "[\\*\\[\\]@='\"]", string.Empty);
        scrubbed = Regex.Replace(scrubbed, @"\s", string.Empty);
        return scrubbed.Length == 0;
    }

    private static string ResolvePath(XElement element) =>
        element.Attribute("Path")?.Value.Trim() ??
        element.Parent?.Attribute("Path")?.Value.Trim() ??
        string.Empty;

    private static string DescribeSource(EventSourceDefinition source) {
        string providers = source.ProviderNames.Count == 0
            ? string.Empty
            : " providers=" + string.Join(",", source.ProviderNames);
        return source.LogName + " [" + string.Join(",", source.EventIds.OrderBy(static id => id)) + "]" + providers;
    }

    private static CollectorSubscriptionCoverageResult Unknown(string evidence, string remediation) => new(
        EventReadinessStatus.Unknown,
        evidence,
        remediation,
        EventReadinessDiagnosticKind.NoEvidence);

    private enum SourceCoverage {
        Covered,
        Missing,
        Unknown,
        Overbroad
    }

    private enum ClauseCoverage {
        Selected,
        NotSelected,
        Unknown
    }
}
