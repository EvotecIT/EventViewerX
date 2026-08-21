using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Xml.Linq;
using EventViewerX.Native;

namespace EventViewerX;

/// <summary>
/// Resolves registered provider manifests and writes schema-valid ETW events
/// without depending on PowerShell internals.
/// </summary>
public static class ManifestEventWriter {
    /// <summary>
    /// Resolves the exact registered event definition selected by a request.
    /// </summary>
    public static ManifestEventDefinition ResolveDefinition(
        ManifestEventWriteRequest request) {

        ValidateRequest(request);
        using var metadata = new ProviderMetadata(
            request.ProviderName.Trim(),
            EventLogSession.GlobalSession,
            CultureInfo.InvariantCulture);
        IReadOnlyDictionary<long, byte> channelIds =
            WindowsEventProviderManifestMetadata.GetChannelIds(
                metadata.Name);
        return ResolveDefinition(
            metadata.Name,
            metadata.Id,
            metadata.LogLinks
                .Select(static link => link.LogName ?? string.Empty)
                .ToArray(),
            metadata.Events
                .Select(eventMetadata =>
                    new EventProviderEventMetadataSnapshot(
                        eventMetadata.Id,
                        eventMetadata.Version,
                        eventMetadata.LogLink?.LogName ?? string.Empty,
                        channelIds[
                            WindowsEventProviderManifestMetadata.CreateKey(
                                eventMetadata.Id,
                                eventMetadata.Version)],
                        eventMetadata.Level?.Value,
                        eventMetadata.Opcode?.Value,
                        eventMetadata.Task?.Value,
                        eventMetadata.Keywords?
                            .Select(static keyword => keyword.Value)
                            .ToArray() ?? Array.Empty<long>(),
                        eventMetadata.Template ?? string.Empty,
                        string.Empty))
                .ToArray(),
            request.Id,
            request.Version);
    }

    /// <summary>
    /// Validates a request against its registered provider manifest and writes
    /// the event through the native Windows ETW API.
    /// </summary>
    public static ManifestEventWriteResult Write(
        ManifestEventWriteRequest request) {

        ManifestEventDefinition definition = ResolveDefinition(request);
        return Write(
            definition,
            request.Payload ?? Array.Empty<object?>(),
            new WindowsManifestEventProvider());
    }

    /// <summary>
    /// Writes a previously resolved event definition without reopening provider
    /// metadata. This is useful for repeated writes of the same event schema.
    /// </summary>
    public static ManifestEventWriteResult Write(
        ManifestEventDefinition definition,
        IReadOnlyList<object?> payload) {

        return Write(
            definition,
            payload,
            new WindowsManifestEventProvider());
    }

    internal static ManifestEventDefinition ResolveDefinition(
        string providerName,
        Guid providerId,
        IReadOnlyList<string> logLinks,
        IReadOnlyList<EventProviderEventMetadataSnapshot> events,
        int id,
        byte? version) {

        EventProviderEventMetadataSnapshot[] matches = events
            .Where(candidate => candidate.Id == id)
            .ToArray();
        if (matches.Length == 0) {
            throw new ArgumentException(
                $"Provider '{providerName}' does not declare event ID {id}.",
                nameof(id));
        }

        EventProviderEventMetadataSnapshot selected;
        if (version.HasValue) {
            selected = matches.FirstOrDefault(
                candidate => candidate.Version == version.Value) ??
                throw new ArgumentException(
                    $"Provider '{providerName}' does not declare event ID " +
                    $"{id} version {version.Value}.",
                    nameof(version));
        } else if (matches.Length == 1) {
            selected = matches[0];
        } else {
            string versions = string.Join(
                ", ",
                matches
                    .Select(static candidate => candidate.Version)
                    .Distinct()
                    .OrderBy(static candidate => candidate));
            throw new ArgumentException(
                $"Provider '{providerName}' declares event ID {id} in " +
                $"multiple versions ({versions}); specify Version.",
                nameof(version));
        }

        if (selected.Id < 0 || selected.Id > ushort.MaxValue) {
            throw new InvalidOperationException(
                $"Provider '{providerName}' declares event ID {selected.Id}, " +
                "which cannot be represented by an ETW event descriptor.");
        }

        int channel = selected.ChannelId ??
            throw new InvalidOperationException(
                $"Provider '{providerName}' event {id} did not expose its native channel identifier.");
        if (!string.IsNullOrWhiteSpace(selected.LogName)) {
            int logLinkIndex = IndexOf(
                logLinks,
                selected.LogName);
            if (logLinkIndex < 0) {
                throw new InvalidOperationException(
                    $"Provider '{providerName}' event {id} references channel " +
                    $"'{selected.LogName}', which is absent from the provider " +
                    "log-link table.");
            }
        }
        long keywords = 0;
        foreach (long keyword in selected.Keywords) {
            keywords |= keyword;
        }

        return new ManifestEventDefinition {
            ProviderName = providerName,
            ProviderId = providerId,
            Id = checked((int)selected.Id),
            Version = selected.Version,
            Channel = checked((byte)channel),
            Level = checked((byte)(selected.Level ?? 0)),
            Opcode = checked((byte)(selected.Opcode ?? 0)),
            Task = checked((ushort)(selected.Task ?? 0)),
            Keywords = keywords,
            LogName = selected.LogName,
            Template = selected.Template,
            PayloadFields = ParsePayloadFields(selected.Template)
        };
    }

    internal static ManifestEventWriteResult Write(
        ManifestEventDefinition definition,
        IReadOnlyList<object?> payload,
        IManifestEventProvider provider) {

        if (definition == null) {
            throw new ArgumentNullException(nameof(definition));
        }
        if (payload == null) {
            throw new ArgumentNullException(nameof(payload));
        }
        if (provider == null) {
            throw new ArgumentNullException(nameof(provider));
        }
        if (definition.PayloadFields.Count != payload.Count) {
            throw new ArgumentException(
                $"Event {definition.Id} version {definition.Version} expects " +
                $"{definition.PayloadFields.Count} payload value(s), but " +
                $"{payload.Count} were supplied.",
                nameof(payload));
        }

        uint status = provider.Write(definition, payload);
        return new ManifestEventWriteResult {
            Definition = definition,
            PayloadCount = payload.Count,
            NativeStatus = status
        };
    }

    internal static IReadOnlyList<object?> OrderNamedPayload(
        ManifestEventDefinition definition,
        IReadOnlyDictionary<string, object?> values) {

        if (definition == null) {
            throw new ArgumentNullException(nameof(definition));
        }
        if (values == null) {
            throw new ArgumentNullException(nameof(values));
        }
        var supplied = new Dictionary<string, object?>(
            StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, object?> value in values) {
            if (supplied.ContainsKey(value.Key)) {
                throw new ArgumentException(
                    $"Payload field '{value.Key}' was supplied more than once.",
                    nameof(values));
            }
            supplied.Add(value.Key, value.Value);
        }
        var ordered = new object?[definition.PayloadFields.Count];
        for (int index = 0;
             index < definition.PayloadFields.Count;
             index++) {
            string name = definition.PayloadFields[index].Name;
            if (!supplied.TryGetValue(name, out object? value)) {
                throw new ArgumentException(
                    $"Required payload field '{name}' was not supplied.",
                    nameof(values));
            }
            ordered[index] = value;
            supplied.Remove(name);
        }
        if (supplied.Count > 0) {
            throw new ArgumentException(
                "Unknown payload field(s): " +
                string.Join(", ", supplied.Keys.OrderBy(
                    static name => name,
                    StringComparer.OrdinalIgnoreCase)),
                nameof(values));
        }
        return ordered;
    }

    internal static IReadOnlyList<ManifestEventPayloadField>
        ParsePayloadFields(string template) {

        if (string.IsNullOrWhiteSpace(template)) {
            return Array.Empty<ManifestEventPayloadField>();
        }

        XDocument document;
        try {
            document = XDocument.Parse(
                template,
                LoadOptions.PreserveWhitespace);
        } catch (Exception exception)
            when (exception is System.Xml.XmlException ||
                  exception is ArgumentException) {
            throw new InvalidOperationException(
                "The provider returned an invalid event template.",
                exception);
        }

        return document
            .Descendants()
            .Where(static element =>
                string.Equals(
                    element.Name.LocalName,
                    "data",
                    StringComparison.OrdinalIgnoreCase))
            .Select((element, index) =>
                new ManifestEventPayloadField {
                    Index = index,
                    Name = ReadAttribute(element, "name"),
                    InputType = ReadAttribute(element, "inType"),
                    OutputType = ReadAttribute(element, "outType"),
                    Map = ReadAttribute(element, "map"),
                    Length = ReadAttribute(element, "length"),
                    Count = ReadAttribute(element, "count")
                })
            .ToArray();
    }

    private static void ValidateRequest(
        ManifestEventWriteRequest request) {

        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }
        if (string.IsNullOrWhiteSpace(request.ProviderName)) {
            throw new ArgumentException(
                "ProviderName cannot be empty.",
                nameof(request));
        }
        if (request.Id < 0 || request.Id > ushort.MaxValue) {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Id must be between 0 and 65535.");
        }
    }

    private static int IndexOf(
        IReadOnlyList<string> values,
        string value) {

        for (int i = 0; i < values.Count; i++) {
            if (string.Equals(
                    values[i],
                    value,
                    StringComparison.OrdinalIgnoreCase)) {
                return i;
            }
        }
        return -1;
    }

    private static string ReadAttribute(
        XElement element,
        string name) {

        return element.Attributes()
            .FirstOrDefault(attribute =>
                string.Equals(
                    attribute.Name.LocalName,
                    name,
                    StringComparison.OrdinalIgnoreCase))
            ?.Value ?? string.Empty;
    }
}
