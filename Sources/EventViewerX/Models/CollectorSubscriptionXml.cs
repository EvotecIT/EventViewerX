namespace EventViewerX;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Linq;

/// <summary>
/// Shared collector subscription XML normalization and comparison helpers.
/// </summary>
public static class CollectorSubscriptionXml {
    /// <summary>
    /// Tries to validate and normalize a collector subscription XML payload.
    /// </summary>
    public static bool TryNormalize(string? xml, out CollectorSubscriptionXmlDetails? details, out string? error) {
        details = null;
        error = null;

        if (string.IsNullOrWhiteSpace(xml)) {
            error = "XML cannot be null or empty.";
            return false;
        }

        try {
            using var reader = XmlReader.Create(new StringReader(xml), CreateReaderSettings());
            var document = XDocument.Load(reader, LoadOptions.None);
            if (!TryReadDetails(
                    document,
                    out var description,
                    out var destinationLog,
                    out var queries,
                    out error)) {
                return false;
            }

            details = new CollectorSubscriptionXmlDetails {
                NormalizedXml = NormalizeXml(document),
                Description = description,
                DestinationLog = destinationLog,
                Queries = queries
            };
            return true;
        } catch (XmlException ex) {
            error = $"Invalid XML content: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Determines whether two collector subscription XML payloads are equivalent after normalization.
    /// </summary>
    public static bool AreEquivalent(string? left, string? right) {
        return string.Equals(
            NormalizeForComparison(left),
            NormalizeForComparison(right),
            StringComparison.Ordinal);
    }

    private static XmlReaderSettings CreateReaderSettings() {
        return new XmlReaderSettings {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreWhitespace = true
        };
    }

    private static bool TryReadDetails(
        XDocument document,
        out string? description,
        out string? destinationLog,
        out IReadOnlyList<string> queries,
        out string? error) {
        description = null;
        destinationLog = null;
        error = null;

        var root = document.Root;
        if (root == null || !root.Name.LocalName.Equals("Subscription", StringComparison.OrdinalIgnoreCase)) {
            queries = Array.Empty<string>();
            error = "Root element must be <Subscription>.";
            return false;
        }

        description = NormalizeOptional(
            root.Elements()
                .FirstOrDefault(static element => element.Name.LocalName.Equals("Description", StringComparison.OrdinalIgnoreCase))
                ?.Value);
        destinationLog = NormalizeOptional(
            root.Elements()
                .FirstOrDefault(static element => element.Name.LocalName.Equals("LogFile", StringComparison.OrdinalIgnoreCase))
                ?.Value);
        var queryValues = new List<string>();
        queryValues.AddRange(root
            .Descendants()
            .Where(static element => element.Name.LocalName.Equals("Select", StringComparison.OrdinalIgnoreCase))
            .Select(static element => NormalizeOptional(element.Value))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>());
        foreach (XElement queryElement in root.Elements()
                     .Where(static element => element.Name.LocalName.Equals("Query", StringComparison.OrdinalIgnoreCase))) {
            string embedded = queryElement.Value.Trim();
            if (embedded.Length == 0 || !embedded.StartsWith("<", StringComparison.Ordinal)) {
                continue;
            }
            using var embeddedReader = XmlReader.Create(
                new StringReader(embedded),
                CreateReaderSettings());
            XDocument queryDocument = XDocument.Load(embeddedReader, LoadOptions.None);
            queryValues.AddRange(queryDocument
                .Descendants()
                .Where(static element => element.Name.LocalName.Equals("Select", StringComparison.OrdinalIgnoreCase))
                .Select(static element => NormalizeOptional(element.Value))
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>());
        }
        queries = queryValues.ToArray();
        return true;
    }

    private static string NormalizeXml(XDocument document) {
        using var writerBuffer = new StringWriter(CultureInfo.InvariantCulture);
        using var writer = XmlWriter.Create(writerBuffer, new XmlWriterSettings {
            OmitXmlDeclaration = true,
            Indent = false,
            NewLineHandling = NewLineHandling.None
        });

        document.WriteTo(writer);
        writer.Flush();
        return writerBuffer.ToString();
    }

    private static string? NormalizeForComparison(string? xml) {
        if (xml == null) {
            return null;
        }

        var trimmed = xml.Trim();
        if (trimmed.Length == 0) {
            return null;
        }

        try {
            using var reader = XmlReader.Create(
                new StringReader(trimmed),
                CreateReaderSettings());
            XDocument document = XDocument.Load(reader, LoadOptions.None);
            CanonicalizeSubscription(document);
            return NormalizeXml(document);
        } catch (XmlException) {
            return trimmed;
        }
    }

    private static void CanonicalizeSubscription(XDocument document) {
        XElement? root = document.Root;
        if (root == null) {
            return;
        }
        string contentFormat = root.Elements()
            .FirstOrDefault(static element => element.Name.LocalName.Equals("ContentFormat", StringComparison.OrdinalIgnoreCase))
            ?.Value.Trim() ?? string.Empty;
        if (contentFormat.Equals("Events", StringComparison.OrdinalIgnoreCase)) {
            root.Elements()
                .Where(static element => element.Name.LocalName.Equals("Locale", StringComparison.OrdinalIgnoreCase))
                .Remove();
        }
        root.Elements()
            .Where(static element =>
                element.Name.LocalName.Equals("AllowedSourceNonDomainComputers", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(element.Value) &&
                !element.HasElements &&
                !element.HasAttributes)
            .Remove();
        foreach (XElement query in root.Elements()
                     .Where(static element => element.Name.LocalName.Equals("Query", StringComparison.OrdinalIgnoreCase))) {
            string value = query.Value.Trim();
            if (value.StartsWith("<", StringComparison.Ordinal)) {
                query.ReplaceNodes(new XCData(CollectorSubscriptionDefinition.NormalizeQueryXml(value)));
            }
        }
        XElement? sources = root.Elements()
            .FirstOrDefault(static element => element.Name.LocalName.Equals("EventSources", StringComparison.OrdinalIgnoreCase));
        if (sources != null) {
            XElement[] ordered = sources.Elements()
                .OrderBy(static source => source.Elements()
                    .FirstOrDefault(static element => element.Name.LocalName.Equals("Address", StringComparison.OrdinalIgnoreCase))
                    ?.Value, StringComparer.OrdinalIgnoreCase)
                .Select(static source => new XElement(source))
                .ToArray();
            sources.ReplaceNodes(ordered);
        }
    }

    private static string? NormalizeOptional(string? value) {
        if (value == null) {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
