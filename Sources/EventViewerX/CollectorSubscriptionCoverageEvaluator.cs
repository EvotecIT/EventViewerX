using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

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
    private static readonly Regex EventIdOnlyExpression = new(
        @"^\s*\*\s*\[\s*System\s*\[\s*\(?\s*EventID\s*=\s*\d+\s*(?:or\s+EventID\s*=\s*\d+\s*)*\)?\s*\]\s*\]\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex EventIdValue = new(
        @"EventID\s*=\s*(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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
            SourceCoverage coverage = EvaluateSource(queryList!, source);
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

    private static SourceCoverage EvaluateSource(XDocument queryList, EventSourceDefinition source) {
        XElement[] selects = queryList
            .Descendants()
            .Where(static element => string.Equals(element.Name.LocalName, "Select", StringComparison.OrdinalIgnoreCase))
            .Where(element => string.Equals(ResolvePath(element), source.LogName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (selects.Length == 0) {
            return SourceCoverage.Missing;
        }

        bool selectsAll = false;
        bool uncertainSelect = false;
        var selectedIds = new HashSet<int>();
        foreach (XElement select in selects) {
            string expression = select.Value.Trim();
            if (string.Equals(expression, "*", StringComparison.Ordinal)) {
                selectsAll = true;
                continue;
            }
            if (!TryReadEventIds(expression, selectedIds)) {
                uncertainSelect = true;
            }
        }
        if (!selectsAll && source.EventIds.Any(id => !selectedIds.Contains(id))) {
            return uncertainSelect ? SourceCoverage.Unknown : SourceCoverage.Missing;
        }

        XElement[] suppressions = queryList
            .Descendants()
            .Where(static element => string.Equals(element.Name.LocalName, "Suppress", StringComparison.OrdinalIgnoreCase))
            .Where(element => string.Equals(ResolvePath(element), source.LogName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        foreach (XElement suppression in suppressions) {
            string expression = suppression.Value.Trim();
            if (string.Equals(expression, "*", StringComparison.Ordinal)) {
                return SourceCoverage.Missing;
            }
            var suppressedIds = new HashSet<int>();
            if (!TryReadEventIds(expression, suppressedIds)) {
                return SourceCoverage.Unknown;
            }
            if (source.EventIds.Any(suppressedIds.Contains)) {
                return SourceCoverage.Missing;
            }
        }
        return SourceCoverage.Covered;
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

    private static bool TryReadEventIds(string expression, HashSet<int> ids) {
        if (!EventIdOnlyExpression.IsMatch(expression)) {
            return false;
        }
        var parsedIds = new List<int>();
        foreach (Match match in EventIdValue.Matches(expression)) {
            if (!int.TryParse(
                    match.Groups[1].Value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int parsed)) {
                return false;
            }
            parsedIds.Add(parsed);
        }
        foreach (int parsed in parsedIds) {
            ids.Add(parsed);
        }
        return parsedIds.Count > 0;
    }

    private static string ResolvePath(XElement element) =>
        element.Attribute("Path")?.Value.Trim() ??
        element.Parent?.Attribute("Path")?.Value.Trim() ??
        string.Empty;

    private static string DescribeSource(EventSourceDefinition source) =>
        source.LogName + " [" + string.Join(",", source.EventIds.OrderBy(static id => id)) + "]";

    private static CollectorSubscriptionCoverageResult Unknown(string evidence, string remediation) => new(
        EventReadinessStatus.Unknown,
        evidence,
        remediation,
        EventReadinessDiagnosticKind.NoEvidence);

    private enum SourceCoverage {
        Covered,
        Missing,
        Unknown
    }
}
