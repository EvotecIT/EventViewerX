using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace EventViewerX.Evtx;

internal static class EvtxDumpJsonProjector {
    internal static SavedEventRecord Create(string json) {
        using JsonDocument document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("Event", out JsonElement eventElement) ||
            eventElement.ValueKind != JsonValueKind.Object) {
            throw new InvalidDataException("The JSONL record does not contain an Event object.");
        }
        var root = new XElement("Event");
        foreach (JsonProperty property in eventElement.EnumerateObject()) {
            if (property.NameEquals("#attributes")) {
                continue;
            }
            if (property.NameEquals("EventData")) {
                root.Add(CreateEventData(property.Value));
                continue;
            }
            AddElements(root, property.Name, property.Value);
        }
        string xml = root.ToString(SaveOptions.DisableFormatting);
        return SavedEventXmlProjector.Create(xml);
    }

    private static XElement CreateEventData(JsonElement value) {
        var result = new XElement("EventData");
        if (value.ValueKind != JsonValueKind.Object) {
            return result;
        }
        foreach (JsonProperty property in value.EnumerateObject()) {
            if (property.Value.ValueKind == JsonValueKind.Array) {
                foreach (JsonElement item in property.Value.EnumerateArray()) {
                    result.Add(new XElement("Data", new XAttribute("Name", property.Name), Scalar(item)));
                }
            } else {
                result.Add(new XElement("Data", new XAttribute("Name", property.Name), Scalar(property.Value)));
            }
        }
        return result;
    }

    private static void AddElements(XElement parent, string name, JsonElement value) {
        if (value.ValueKind == JsonValueKind.Array) {
            foreach (JsonElement item in value.EnumerateArray()) {
                AddElements(parent, name, item);
            }
            return;
        }
        var element = new XElement(NormalizeXmlName(name));
        if (value.ValueKind == JsonValueKind.Object) {
            foreach (JsonProperty property in value.EnumerateObject()) {
                if (property.NameEquals("#attributes") && property.Value.ValueKind == JsonValueKind.Object) {
                    foreach (JsonProperty attribute in property.Value.EnumerateObject()) {
                        if (!attribute.Name.StartsWith("xmlns", StringComparison.Ordinal)) {
                            element.SetAttributeValue(NormalizeXmlName(attribute.Name), Scalar(attribute.Value));
                        }
                    }
                } else {
                    AddElements(element, property.Name, property.Value);
                }
            }
        } else if (value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)) {
            element.Value = Scalar(value);
        }
        parent.Add(element);
    }

    private static string Scalar(JsonElement value) => value.ValueKind switch {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
        _ => value.GetRawText()
    };

    private static string NormalizeXmlName(string name) {
        int prefixSeparator = name.LastIndexOf(':');
        string localName = prefixSeparator >= 0 && prefixSeparator + 1 < name.Length
            ? name.Substring(prefixSeparator + 1)
            : name;
        return XmlConvert.EncodeLocalName(localName);
    }
}
