using System.Globalization;
using System.Net;
using System.Text.Json.Serialization;

namespace EventViewerX;

/// <summary>Declares one projected property for a custom event definition.</summary>
public sealed class EventDefinitionField {
    /// <summary>Output property name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Optional human-friendly report heading. The field name is used when empty.</summary>
    public string DisplayName { get; set; } = string.Empty;
    /// <summary>Optional field purpose shown by discovery and report surfaces.</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>Alternative field names accepted by predicate builders.</summary>
    public IReadOnlyList<string> Aliases { get; set; } = Array.Empty<string>();
    /// <summary>Portable output value type.</summary>
    public EventFieldValueKind ValueKind { get; set; } = EventFieldValueKind.String;
    /// <summary>CLR value type produced by the configured portable value kind.</summary>
    [JsonIgnore]
    public Type ValueType => ValueKind switch {
        EventFieldValueKind.Int32 => typeof(int),
        EventFieldValueKind.Int64 => typeof(long),
        EventFieldValueKind.Boolean => typeof(bool),
        EventFieldValueKind.DateTime => typeof(DateTime),
        EventFieldValueKind.Guid => typeof(Guid),
        EventFieldValueKind.IpAddress => typeof(System.Net.IPAddress),
        _ => typeof(string)
    };
    /// <summary>Value source.</summary>
    public EventFieldSource Source { get; set; }
    /// <summary>Data key, metadata property, or literal constant.</summary>
    public string SourceName { get; set; } = string.Empty;
    /// <summary>Fallback value when the selected source is absent.</summary>
    public string? DefaultValue { get; set; }

    internal object? ConvertValue(object? value) {
        if (value == null) {
            return null;
        }
        string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        try {
            return ValueKind switch {
                EventFieldValueKind.Int32 => Convert.ToInt32(value, CultureInfo.InvariantCulture),
                EventFieldValueKind.Int64 => Convert.ToInt64(value, CultureInfo.InvariantCulture),
                EventFieldValueKind.Boolean => Convert.ToBoolean(value, CultureInfo.InvariantCulture),
                EventFieldValueKind.DateTime => value is DateTime dateTime
                    ? dateTime
                    : DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                EventFieldValueKind.Guid => value is Guid guid ? guid : Guid.Parse(text),
                EventFieldValueKind.IpAddress => value is IPAddress address ? address : IPAddress.Parse(text),
                _ => text
            };
        } catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException) {
            throw new InvalidDataException(
                $"Field '{Name}' value '{text}' cannot be converted to {ValueKind}.",
                exception);
        }
    }
}
