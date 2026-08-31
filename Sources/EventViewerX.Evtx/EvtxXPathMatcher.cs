using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Xml.Xsl;

namespace EventViewerX.Evtx;

internal sealed class EvtxXPathMatcher {
    private static readonly Regex SystemTimeComparison = new(
        "@SystemTime\\s*(?<operator>>=|<=|>|<|=)\\s*(?<quote>['\"])(?<value>[^'\"]+)\\k<quote>",
        RegexOptions.CultureInvariant);
    private static readonly EventXPathContext XPathContext = new();
    private readonly string _xpath;
    private readonly XPathExpression _expression;

    internal EvtxXPathMatcher(string? xpath) {
        _xpath = string.IsNullOrWhiteSpace(xpath) ? "*" : xpath!.Trim();
        try {
            string portableXPath = SystemTimeComparison.Replace(_xpath, static match => {
                string comparison = match.Groups["operator"].Value;
                string quote = match.Groups["quote"].Value;
                string value = match.Groups["value"].Value;
                return $"evx:compare-time(@SystemTime, {quote}{value}{quote}, '{comparison}')";
            });
            _expression = XPathExpression.Compile(portableXPath);
            _expression.SetContext(XPathContext);
        } catch (XPathException exception) {
            throw new NotSupportedException(
                $"The portable EVTX reader does not support XPath '{_xpath}'. " +
                "Use a standard XPath 1.0 expression or the Windows Eventing API.",
                exception);
        }
    }

    internal bool IsMatch(string xml) {
        if (_xpath == "*") {
            return true;
        }
        try {
            using var stringReader = new StringReader(xml);
            using XmlReader reader = XmlReader.Create(stringReader, new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            });
            XDocument document = XDocument.Load(reader, LoadOptions.None);
            if (document.Root == null) {
                return false;
            }
            var namespaceFree = new XDocument(RemoveNamespaces(document.Root));
            XPathNavigator navigator = namespaceFree.CreateNavigator()!;
            object result = navigator.Evaluate(_expression.Clone());
            return result switch {
                bool matched => matched,
                XPathNodeIterator nodes => nodes.MoveNext(),
                _ => false
            };
        } catch (XPathException exception) {
            throw new NotSupportedException(
                $"The portable EVTX reader cannot evaluate Windows-specific XPath '{_xpath}'. " +
                "Use a standard XPath 1.0 expression or the Windows Eventing API.",
                exception);
        }
    }

    private static XElement RemoveNamespaces(XElement element) => new(
        element.Name.LocalName,
        element.Attributes()
            .Where(static attribute => !attribute.IsNamespaceDeclaration)
            .Select(static attribute => new XAttribute(attribute.Name.LocalName, attribute.Value)),
        element.Nodes().Select(static node => node is XElement child
            ? RemoveNamespaces(child)
            : node));

    private sealed class EventXPathContext : XsltContext {
        private static readonly EventTimeComparisonFunction TimeComparison = new();

        internal EventXPathContext() : base(new NameTable()) {
            AddNamespace("evx", "urn:eventviewerx:xpath");
        }

        public override bool Whitespace => true;

        public override int CompareDocument(string baseUri, string nextBaseUri) =>
            string.CompareOrdinal(baseUri, nextBaseUri);

        public override bool PreserveWhitespace(XPathNavigator node) => true;

        public override IXsltContextFunction ResolveFunction(
            string prefix,
            string name,
            XPathResultType[] argumentTypes) {

            if (prefix == "evx" && name == "compare-time" && argumentTypes.Length == 3) {
                return TimeComparison;
            }
            throw new XPathException($"Unsupported portable XPath function '{prefix}:{name}'.");
        }

        public override IXsltContextVariable ResolveVariable(string prefix, string name) =>
            throw new XPathException($"Unsupported portable XPath variable '{prefix}:{name}'.");
    }

    private sealed class EventTimeComparisonFunction : IXsltContextFunction {
        private static readonly XPathResultType[] Parameters = {
            XPathResultType.NodeSet,
            XPathResultType.String,
            XPathResultType.String
        };

        public int Minargs => 3;
        public int Maxargs => 3;
        public XPathResultType ReturnType => XPathResultType.Boolean;
        public XPathResultType[] ArgTypes => Parameters;

        public object Invoke(XsltContext xsltContext, object[] args, XPathNavigator docContext) {
            if (args[0] is not XPathNodeIterator values || !values.MoveNext()) {
                return false;
            }
            string actualText = values.Current?.Value ?? string.Empty;
            string expectedText = Convert.ToString(args[1], CultureInfo.InvariantCulture) ?? string.Empty;
            string comparison = Convert.ToString(args[2], CultureInfo.InvariantCulture) ?? string.Empty;
            const DateTimeStyles styles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;
            if (!DateTimeOffset.TryParse(actualText, CultureInfo.InvariantCulture, styles, out DateTimeOffset actual) ||
                !DateTimeOffset.TryParse(expectedText, CultureInfo.InvariantCulture, styles, out DateTimeOffset expected)) {
                return false;
            }
            int result = actual.UtcDateTime.Ticks.CompareTo(expected.UtcDateTime.Ticks);
            return comparison switch {
                ">=" => result >= 0,
                "<=" => result <= 0,
                ">" => result > 0,
                "<" => result < 0,
                "=" => result == 0,
                _ => false
            };
        }
    }
}
