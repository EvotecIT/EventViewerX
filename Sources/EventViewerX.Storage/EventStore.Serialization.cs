using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using EventViewerX.Reporting;

namespace EventViewerX.Storage;

public sealed partial class EventStore {
    private readonly StoreSerialization _serialization;
    private JsonSerializerOptions JsonOptions => _serialization.Options;

    /// <summary>Creates a store using generated JSON contracts for additional caller-owned value types.</summary>
    /// <param name="path">SQLite database path.</param>
    /// <param name="valueTypes">Generated contracts for additional values. An empty collection uses the built-in EventViewerX contracts.</param>
    public EventStore(string path, IEnumerable<JsonTypeInfo> valueTypes)
        : this(path, StoreSerialization.Create(valueTypes)) { }

    private EventStore(string path, StoreSerialization serialization) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Store path cannot be empty.", nameof(path));
        }
        Path = System.IO.Path.GetFullPath(path);
        _serialization = serialization;
    }

    private sealed class StoreSerialization {
        private StoreSerialization(Func<string?, Type> resolveType,
            Func<string?, string> normalizeTypeName,
            Func<string, Type, object?> deserializeValue,
            JsonSerializerOptions options) {
            ResolveType = resolveType;
            NormalizeTypeName = normalizeTypeName;
            DeserializeValue = deserializeValue;
            Options = options;
        }

        internal Func<string?, Type> ResolveType { get; }
        internal Func<string?, string> NormalizeTypeName { get; }
        internal Func<string, Type, object?> DeserializeValue { get; }
        internal JsonSerializerOptions Options { get; }

        internal static StoreSerialization Create(IEnumerable<JsonTypeInfo> valueTypes) {
            if (valueTypes == null) {
                throw new ArgumentNullException(nameof(valueTypes));
            }
            JsonTypeInfo[] contracts = valueTypes.ToArray();
            if (contracts.Any(static contract => contract == null)) {
                throw new ArgumentException("Generated value contracts cannot contain null entries.", nameof(valueTypes));
            }
            Dictionary<Type, JsonTypeInfo> byType = contracts.ToDictionary(static contract => contract.Type);
            Dictionary<string, Type> byName = contracts.ToDictionary(
                static contract => EventReportColumnSchema.GetStableTypeName(contract.Type),
                static contract => contract.Type, StringComparer.Ordinal);
            JsonSerializerOptions options = EventJson.WithValueContracts(CreateJsonOptions(), contracts);
            Type Resolve(string? name) => name != null && byName.TryGetValue(name.Trim(), out Type? type)
                ? type : EventReportColumnSchema.ResolveKnownValueTypeName(name);
            JsonTypeInfo Contract(Type type) => byType.TryGetValue(type, out JsonTypeInfo? contract)
                ? contract : EventJson.GetTypeInfo(type, options);
            return new StoreSerialization(Resolve,
                name => Resolve(name) is Type type && type != typeof(object)
                    ? EventReportColumnSchema.GetStableTypeName(type)
                    : EventReportColumnSchema.NormalizeKnownValueTypeName(name),
                (json, type) => JsonSerializer.Deserialize(json, Contract(type)), options);
        }

#if NET8_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Arbitrary stored CLR values require preserved JSON metadata. Supply generated value contracts to the EventStore constructor for Native AOT.")]
        [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Arbitrary stored CLR values may require runtime JSON serialization code. Supply generated value contracts to the EventStore constructor for Native AOT.")]
#endif
        internal static StoreSerialization CreateReflection() {
            JsonSerializerOptions options = CreateJsonOptions();
            options.Converters.Add(new JsonStringEnumConverter());
            return new StoreSerialization(EventReportColumnSchema.ResolveValueTypeName,
                EventReportColumnSchema.NormalizeValueTypeName,
                (json, type) => JsonSerializer.Deserialize(json, type, options),
                EventJson.WithReflectionValues(options, stringEnums: false));
        }
    }

}
