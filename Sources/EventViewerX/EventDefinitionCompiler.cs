using System.Xml.Linq;

namespace EventViewerX;

/// <summary>Compiles built-in or custom typed definitions to native Windows Event Log QueryList XML.</summary>
public static class EventDefinitionCompiler {
    /// <summary>Builds the native XPath for one source, optionally matching its original channel inside a collector log.</summary>
    public static string BuildSourceXPath(string logName, IReadOnlyList<int> eventIds,
        IReadOnlyList<string>? providerNames = null, bool collector = false) {
        if (string.IsNullOrWhiteSpace(logName)) {
            throw new ArgumentException("LogName cannot be empty.", nameof(logName));
        }
        if (eventIds == null || eventIds.Count == 0 || eventIds.Any(static id => id <= 0)) {
            throw new ArgumentException("EventIds must contain positive values.", nameof(eventIds));
        }
        if (providerNames != null && providerNames.Any(static provider => string.IsNullOrWhiteSpace(provider))) {
            throw new ArgumentException("ProviderNames cannot contain empty values.", nameof(providerNames));
        }
        string xpath = EventFilterCompiler.BuildXPath(new EventFilter {
            EventIds = eventIds.Distinct().OrderBy(static id => id).ToArray(),
            ProviderNames = (providerNames ?? Array.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static provider => provider, StringComparer.OrdinalIgnoreCase).ToArray()
        });
        return collector ? EventFilterCompiler.AddOriginalChannelPredicate(xpath, logName) : xpath;
    }

    /// <summary>Compiles built-in leaf or composite types.</summary>
    public static string BuildQueryXml(IEnumerable<EventType> types) {
        if (types == null) {
            throw new ArgumentNullException(nameof(types));
        }
        return Build(EventTypeCatalog.GetSources(types).Select(static source =>
            (source.LogName, source.EventIds, source.ProviderNames)));
    }

    /// <summary>Compiles a custom definition.</summary>
    public static string BuildQueryXml(EventDefinition definition) {
        if (definition == null) {
            throw new ArgumentNullException(nameof(definition));
        }
        definition.Validate();
        return Build(definition.Sources.Select(static source =>
            (source.LogName, source.EventIds, source.ProviderNames)));
    }

    private static string Build(IEnumerable<(string LogName, IReadOnlyList<int> EventIds, IReadOnlyList<string> ProviderNames)> sources) {
        var root = new XElement("QueryList");
        int id = 0;
        foreach (var source in sources) {
            IReadOnlyList<EventFilter> partitions = EventFilterPartitioner.Partition(new EventFilter {
                EventIds = source.EventIds.Distinct().OrderBy(static eventId => eventId).ToArray(),
                ProviderNames = source.ProviderNames.Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(static provider => provider, StringComparer.OrdinalIgnoreCase).ToArray()
            });
            root.Add(new XElement("Query",
                new XAttribute("Id", id++),
                new XAttribute("Path", source.LogName),
                partitions.Select(partition => new XElement(
                    "Select",
                    new XAttribute("Path", source.LogName),
                    EventFilterCompiler.BuildXPath(partition)))));
        }
        if (id == 0) {
            throw new ArgumentException("At least one event source is required.", nameof(sources));
        }
        return new XDocument(root).ToString(SaveOptions.DisableFormatting);
    }
}
