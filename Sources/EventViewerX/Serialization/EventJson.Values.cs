using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace EventViewerX;

internal static partial class EventJson {
    internal static JsonSerializerOptions WithValueSerializer(JsonSerializerOptions options, Action<Utf8JsonWriter, object?> serialize) {
        var configured = new JsonSerializerOptions(options);
        configured.Converters.Add(new ValueConverter(serialize));
        return configured;
    }

    internal static JsonSerializerOptions WithValueContracts(JsonSerializerOptions options, IReadOnlyCollection<JsonTypeInfo> valueTypes) {
        if (valueTypes == null) {
            throw new ArgumentNullException(nameof(valueTypes));
        }
        if (valueTypes.Any(static contract => contract == null)) {
            throw new ArgumentException("Generated value contracts cannot contain null entries.", nameof(valueTypes));
        }
        Dictionary<Type, JsonTypeInfo> contracts = valueTypes.ToDictionary(static contract => contract.Type);
        var configured = new JsonSerializerOptions(options);
        configured.Converters.Add(new ValueConverter((writer, value) => {
            Type type = value?.GetType() ?? typeof(object);
            if (type == typeof(object)) {
                writer.WriteStartObject();
                writer.WriteEndObject();
                return;
            }
            JsonTypeInfo contract = contracts.TryGetValue(type, out JsonTypeInfo? supplied)
                ? supplied : GetTypeInfo(type, configured);
            JsonSerializer.Serialize(writer, value, contract);
        }));
        return configured;
    }

#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Arbitrary caller-owned values require preserved JSON metadata.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Arbitrary caller-owned values may require runtime JSON serialization code.")]
#endif
    internal static JsonSerializerOptions WithReflectionValues(JsonSerializerOptions options, bool stringEnums) {
        var reflectionOptions = new JsonSerializerOptions(options);
        if (stringEnums) {
            reflectionOptions.Converters.Add(new JsonStringEnumConverter());
        }
        return WithValueSerializer(options,
            (writer, value) => JsonSerializer.Serialize(writer, value, value?.GetType() ?? typeof(object), reflectionOptions));
    }

    private sealed class ValueConverter : JsonConverter<object> {
        private readonly Action<Utf8JsonWriter, object?> _serialize;

        internal ValueConverter(Action<Utf8JsonWriter, object?> serialize) {
            _serialize = serialize;
        }

        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return document.RootElement.Clone();
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options) =>
            _serialize(writer, value);
    }
}
