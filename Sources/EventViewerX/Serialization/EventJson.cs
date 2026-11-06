using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace EventViewerX;

// All persisted contracts use generated metadata, including on the full runtime.
internal static partial class EventJson {
    private static readonly JsonSerializerOptions DefaultOptions = new();
    private static readonly ConditionalWeakTable<JsonSerializerOptions, EventJsonContext> Contexts = new();

    private static JsonTypeInfo<T> TypeInfo<T>(JsonSerializerOptions? options) =>
        (JsonTypeInfo<T>)GetTypeInfo(typeof(T), options);

    internal static JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions? options = null) {
        EventJsonContext context = Contexts.GetValue(options ?? DefaultOptions,
            static settings => new EventJsonContext(new JsonSerializerOptions(settings)));
        return context.GetTypeInfo(type) ?? throw new NotSupportedException(
            $"JSON contract '{type.FullName}' has no generated metadata.");
    }

    internal static string Serialize<T>(T value, JsonSerializerOptions? options = null) =>
        JsonSerializer.Serialize(value, TypeInfo<T>(options));

    internal static byte[] SerializeToUtf8Bytes<T>(T value, JsonSerializerOptions? options = null) =>
        JsonSerializer.SerializeToUtf8Bytes(value, TypeInfo<T>(options));

    internal static JsonElement SerializeToElement<T>(T value, JsonSerializerOptions? options = null) =>
        JsonSerializer.SerializeToElement(value, TypeInfo<T>(options));

    internal static JsonElement SerializeToElement(object? value, Type type) =>
        JsonSerializer.SerializeToElement(value, GetTypeInfo(type));

    internal static T? Deserialize<T>(string json, JsonSerializerOptions? options = null) =>
        JsonSerializer.Deserialize(json, TypeInfo<T>(options));

    internal static object? Deserialize(string json, Type type, JsonSerializerOptions? options = null) =>
        JsonSerializer.Deserialize(json, GetTypeInfo(type, options));
}
