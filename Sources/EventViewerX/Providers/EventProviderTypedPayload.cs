using System.Reflection;
using System.Security.Principal;

namespace EventViewerX.Providers;

/// <summary>Infers stable manifest fields and named values from typed payloads.</summary>
public static class EventProviderTypedPayload {
    private static readonly ConcurrentDictionary<
        Type,
        TypedPayloadDescriptor> Cache = new();

    /// <summary>Describes the public readable properties of a payload type.</summary>
    public static IReadOnlyList<EventProviderFieldDefinition> Describe<TPayload>() {
        return Describe(typeof(TPayload));
    }

    /// <summary>Describes the public readable properties of a payload type.</summary>
    public static IReadOnlyList<EventProviderFieldDefinition> Describe(
        Type payloadType) {

        if (payloadType == null) {
            throw new ArgumentNullException(nameof(payloadType));
        }
        return Cache.GetOrAdd(
                payloadType,
                CreateDescriptor)
            .Bindings
            .Select(static binding =>
                Clone(binding.Field))
            .ToArray();
    }

    /// <summary>
    /// Reads public properties into a case-insensitive named payload dictionary.
    /// </summary>
    public static IReadOnlyDictionary<string, object?> Read<TPayload>(
        TPayload payload) {

        if (payload == null) {
            throw new ArgumentNullException(nameof(payload));
        }
        TypedPayloadDescriptor descriptor =
            Cache.GetOrAdd(
                payload.GetType(),
                CreateDescriptor);
        var values = new Dictionary<string, object?>(
            StringComparer.OrdinalIgnoreCase);
        foreach (TypedPayloadBinding binding in
                 descriptor.Bindings) {
            values[binding.Field.Name] =
                binding.Property.GetValue(payload, null);
        }
        return values;
    }

    private static TypedPayloadDescriptor CreateDescriptor(
        Type payloadType) {

        PropertyInfo[] properties = payloadType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property =>
                property.CanRead &&
                property.GetIndexParameters().Length == 0)
            .ToArray();
        if (properties.Length == 0) {
            throw new ArgumentException(
                $"Payload type '{payloadType.FullName}' has no public readable properties.",
                nameof(payloadType));
        }

        var ordered = properties
            .Select(property => new {
                Property = property,
                Attribute = property.GetCustomAttribute<
                    EventProviderPayloadFieldAttribute>()
            })
            .OrderBy(static item =>
                item.Attribute?.Order ?? int.MaxValue)
            .ThenBy(static item => item.Property.MetadataToken)
            .ToArray();

        var explicitOrders = new HashSet<int>();
        foreach (var item in ordered) {
            if (item.Attribute == null) {
                continue;
            }
            if (item.Attribute.Order < 0 ||
                !explicitOrders.Add(item.Attribute.Order)) {
                throw new ArgumentException(
                    $"Payload type '{payloadType.FullName}' contains a negative or duplicate EventProviderPayloadField order.");
            }
        }

        TypedPayloadBinding[] bindings = ordered
            .Select(item => {
                Type propertyType = Nullable.GetUnderlyingType(
                    item.Property.PropertyType) ??
                    item.Property.PropertyType;
                EventProviderPayloadFieldAttribute? attribute =
                    item.Attribute;
                EventProviderFieldType fieldType =
                    attribute == null ||
                    attribute.Type == EventProviderFieldType.Auto
                        ? InferFieldType(propertyType)
                        : attribute.Type;
                string length = attribute?.Length ?? string.Empty;
                string count = attribute?.Count ?? string.Empty;
                ValidateVariableField(
                    payloadType,
                    item.Property,
                    propertyType,
                    fieldType,
                    length,
                    count);
                var field = new EventProviderFieldDefinition {
                    Name = string.IsNullOrWhiteSpace(attribute?.Name)
                        ? item.Property.Name
                        : attribute!.Name,
                    Type = fieldType,
                    OutputType = attribute?.OutputType ??
                                 EventProviderFieldOutputType.Default,
                    Map = attribute?.Map ?? string.Empty,
                    Length = length,
                    Count = count
                };
                return new TypedPayloadBinding(
                    item.Property,
                    field);
            })
            .ToArray();
        return new TypedPayloadDescriptor(bindings);
    }

    private static EventProviderFieldType InferFieldType(Type type) {
        if (type == typeof(string) || type == typeof(char)) {
            return EventProviderFieldType.UnicodeString;
        }
        if (type == typeof(sbyte)) {
            return EventProviderFieldType.Int8;
        }
        if (type == typeof(byte)) {
            return EventProviderFieldType.UInt8;
        }
        if (type == typeof(short)) {
            return EventProviderFieldType.Int16;
        }
        if (type == typeof(ushort)) {
            return EventProviderFieldType.UInt16;
        }
        if (type == typeof(int)) {
            return EventProviderFieldType.Int32;
        }
        if (type == typeof(uint)) {
            return EventProviderFieldType.UInt32;
        }
        if (type == typeof(long)) {
            return EventProviderFieldType.Int64;
        }
        if (type == typeof(ulong)) {
            return EventProviderFieldType.UInt64;
        }
        if (type == typeof(float)) {
            return EventProviderFieldType.Float;
        }
        if (type == typeof(double) || type == typeof(decimal)) {
            return EventProviderFieldType.Double;
        }
        if (type == typeof(bool)) {
            return EventProviderFieldType.Boolean;
        }
        if (type == typeof(Guid)) {
            return EventProviderFieldType.Guid;
        }
        if (type == typeof(DateTime) ||
            type == typeof(DateTimeOffset)) {
            return EventProviderFieldType.FileTime;
        }
        if (type == typeof(SecurityIdentifier)) {
            return EventProviderFieldType.Sid;
        }
        if (type == typeof(IntPtr) || type == typeof(UIntPtr)) {
            return EventProviderFieldType.Pointer;
        }
        if (type == typeof(byte[])) {
            return EventProviderFieldType.Binary;
        }
        if (type.IsArray) {
            return InferFieldType(type.GetElementType()!);
        }
        throw new NotSupportedException(
            $"Payload property type '{type.FullName}' requires an explicit EventProviderPayloadField type.");
    }

    private static void ValidateVariableField(
        Type payloadType,
        PropertyInfo property,
        Type propertyType,
        EventProviderFieldType fieldType,
        string length,
        string count) {

        if (propertyType == typeof(byte[]) &&
            fieldType == EventProviderFieldType.Binary &&
            string.IsNullOrWhiteSpace(length)) {
            throw new ArgumentException(
                $"Binary payload property '{payloadType.FullName}.{property.Name}' " +
                "requires EventProviderPayloadField.Length to name an earlier " +
                "numeric length field or provide a fixed length.");
        }
        if (propertyType.IsArray &&
            propertyType != typeof(byte[]) &&
            string.IsNullOrWhiteSpace(count)) {
            throw new ArgumentException(
                $"Array payload property '{payloadType.FullName}.{property.Name}' " +
                "requires EventProviderPayloadField.Count to name an earlier " +
                "numeric count field or provide a fixed count.");
        }
    }

    private static EventProviderFieldDefinition Clone(
        EventProviderFieldDefinition field) {

        return new EventProviderFieldDefinition {
            Name = field.Name,
            Type = field.Type,
            OutputType = field.OutputType,
            CustomOutputType = field.CustomOutputType,
            Map = field.Map,
            Length = field.Length,
            Count = field.Count
        };
    }

    private sealed class TypedPayloadDescriptor {
        internal TypedPayloadDescriptor(
            IReadOnlyList<TypedPayloadBinding> bindings) {

            Bindings = bindings;
        }

        internal IReadOnlyList<TypedPayloadBinding> Bindings { get; }
    }

    private sealed class TypedPayloadBinding {
        internal TypedPayloadBinding(
            PropertyInfo property,
            EventProviderFieldDefinition field) {

            Property = property;
            Field = field;
        }

        internal PropertyInfo Property { get; }
        internal EventProviderFieldDefinition Field { get; }
    }
}
