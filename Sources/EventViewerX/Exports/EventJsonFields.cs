using System;
using System.Collections.Generic;
using System.Text.Json;

namespace EventViewerX.Exports;

internal static class EventJsonFields {
    internal static void WriteProperties(
        Utf8JsonWriter writer,
        IReadOnlyList<EventPropertyValue> properties) {

        writer.WriteStartArray();
        foreach (EventPropertyValue property in properties) {
            EventPropertyJson.Write(writer, property.Value);
        }
        writer.WriteEndArray();
    }

    internal static void WriteStrings(
        Utf8JsonWriter writer,
        IEnumerable<string> values) {

        writer.WriteStartArray();
        foreach (string value in values) {
            writer.WriteStringValue(value);
        }
        writer.WriteEndArray();
    }

    internal static void WriteData(
        Utf8JsonWriter writer,
        IReadOnlyDictionary<string, string> data,
        List<string> keys) {

        keys.Clear();
        foreach (string key in data.Keys) {
            keys.Add(key);
        }
        keys.Sort(StringComparer.Ordinal);

        writer.WriteStartObject();
        foreach (string key in keys) {
            writer.WriteString(key, data[key]);
        }
        writer.WriteEndObject();
    }

    internal static void WriteAttachments(
        Utf8JsonWriter writer,
        IReadOnlyList<byte[]> attachments) {

        writer.WriteStartArray();
        foreach (byte[] attachment in attachments) {
            writer.WriteBase64StringValue(attachment);
        }
        writer.WriteEndArray();
    }
}
