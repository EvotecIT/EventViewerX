using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace EventViewerX.Evtx;

internal sealed class EvtxXPathMatcher {
    private readonly string _xpath;

    internal EvtxXPathMatcher(string? xpath) {
        _xpath = string.IsNullOrWhiteSpace(xpath) ? "*" : xpath!.Trim();
        try {
            XPathExpression.Compile(_xpath);
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
            object result = namespaceFree.XPathEvaluate(_xpath);
            return result switch {
                bool matched => matched,
                IEnumerable<object> nodes => nodes.Any(),
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
}
