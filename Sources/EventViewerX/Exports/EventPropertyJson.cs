using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace EventViewerX.Exports;

internal static class EventPropertyJson {
    internal static string Serialize(IEnumerable values) {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) {
            writer.WriteStartArray();
            foreach (object? value in values) {
                Write(writer, value);
            }
            writer.WriteEndArray();
            writer.Flush();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    internal static void Write(Utf8JsonWriter writer, object? value) {
        switch (value) {
            case null:
                writer.WriteNullValue();
                break;
            case string text:
                writer.WriteStringValue(text);
                break;
            case char character:
                writer.WriteStringValue(character.ToString());
                break;
            case bool boolean:
                writer.WriteBooleanValue(boolean);
                break;
            case byte number:
                writer.WriteNumberValue(number);
                break;
            case sbyte number:
                writer.WriteNumberValue(number);
                break;
            case short number:
                writer.WriteNumberValue(number);
                break;
            case ushort number:
                writer.WriteNumberValue(number);
                break;
            case int number:
                writer.WriteNumberValue(number);
                break;
            case uint number:
                writer.WriteNumberValue(number);
                break;
            case long number:
                writer.WriteNumberValue(number);
                break;
            case ulong number:
                writer.WriteNumberValue(number);
                break;
            case float number:
                WriteFloatingPoint(writer, number);
                break;
            case double number:
                WriteFloatingPoint(writer, number);
                break;
            case decimal number:
                writer.WriteNumberValue(number);
                break;
            case DateTime dateTime:
                writer.WriteStringValue(dateTime.ToUniversalTime());
                break;
            case DateTimeOffset dateTimeOffset:
                writer.WriteStringValue(dateTimeOffset);
                break;
            case TimeSpan timeSpan:
                writer.WriteStringValue(timeSpan.ToString("c", CultureInfo.InvariantCulture));
                break;
            case Guid guid:
                writer.WriteStringValue(guid);
                break;
            case SecurityIdentifier sid:
                writer.WriteStringValue(sid.Value);
                break;
            case byte[] binary:
                writer.WriteBase64StringValue(binary);
                break;
            case IntPtr pointer:
                writer.WriteNumberValue(pointer.ToInt64());
                break;
            case UIntPtr pointer:
                writer.WriteNumberValue(pointer.ToUInt64());
                break;
            case Enum enumValue:
                writer.WriteStringValue(enumValue.ToString());
                break;
            case IEnumerable sequence:
                writer.WriteStartArray();
                foreach (object? item in sequence) {
                    Write(writer, item);
                }
                writer.WriteEndArray();
                break;
            default:
                writer.WriteStringValue(
                    Convert.ToString(value, CultureInfo.InvariantCulture) ??
                    string.Empty);
                break;
        }
    }

    private static void WriteFloatingPoint(
        Utf8JsonWriter writer,
        float number) {

        if (float.IsNaN(number)) {
            writer.WriteStringValue("NaN");
        } else if (float.IsPositiveInfinity(number)) {
            writer.WriteStringValue("Infinity");
        } else if (float.IsNegativeInfinity(number)) {
            writer.WriteStringValue("-Infinity");
        } else {
            writer.WriteNumberValue(number);
        }
    }

    private static void WriteFloatingPoint(
        Utf8JsonWriter writer,
        double number) {

        if (double.IsNaN(number)) {
            writer.WriteStringValue("NaN");
        } else if (double.IsPositiveInfinity(number)) {
            writer.WriteStringValue("Infinity");
        } else if (double.IsNegativeInfinity(number)) {
            writer.WriteStringValue("-Infinity");
        } else {
            writer.WriteNumberValue(number);
        }
    }
}
