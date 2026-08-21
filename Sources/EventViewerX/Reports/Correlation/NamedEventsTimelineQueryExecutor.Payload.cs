using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;

namespace EventViewerX.Reports.Correlation;

internal static partial class NamedEventsTimelineQueryExecutor {
    private const int MaxSnakeCaseCacheEntries = 4096;
    private static readonly ConcurrentDictionary<Type, PayloadExtractionPlan> PayloadExtractionPlanCache = new();
    private static readonly ConcurrentDictionary<string, string> SnakeCaseCache = new(StringComparer.Ordinal);

    private static Dictionary<string, object?> ExtractPayload(EventTypeRecord item) {
        if (item is null) {
            throw new ArgumentNullException(nameof(item));
        }

        var plan = PayloadExtractionPlanCache.GetOrAdd(item.GetType(), static type => BuildPayloadExtractionPlan(type));
        var payload = new Dictionary<string, object?>(
            plan.FieldAccessors.Length + plan.PropertyAccessors.Length,
            StringComparer.OrdinalIgnoreCase);

        foreach (var accessor in plan.FieldAccessors) {
            payload[accessor.Key] = NormalizeValue(accessor.Field.GetValue(item));
        }

        foreach (var accessor in plan.PropertyAccessors) {
            object? value;
            try {
                value = accessor.Property.GetValue(item);
            } catch (Exception ex) {
                Debug.WriteLine($"[NamedEventsTimelineQueryExecutor] Failed to read payload property '{accessor.Property.Name}': {ex.Message}");
                continue;
            }

            payload[accessor.Key] = NormalizeValue(value);
        }

        return payload;
    }

    private static PayloadExtractionPlan BuildPayloadExtractionPlan(Type type) {
        var fieldAccessors = new List<PayloadFieldAccessor>();
        var propertyAccessors = new List<PayloadPropertyAccessor>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance)) {
            if (!ShouldIncludeField(field)) {
                continue;
            }

            var key = ToSnakeCase(field.Name);
            if (seenKeys.Add(key)) {
                fieldAccessors.Add(new PayloadFieldAccessor(field, key));
            }
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
            if (!ShouldIncludeProperty(property)) {
                continue;
            }

            var key = ToSnakeCase(property.Name);
            if (seenKeys.Add(key)) {
                propertyAccessors.Add(new PayloadPropertyAccessor(property, key));
            }
        }

        return new PayloadExtractionPlan(fieldAccessors.ToArray(), propertyAccessors.ToArray());
    }

    private static Dictionary<string, object?> ProjectPayload(
        Dictionary<string, object?> payload,
        HashSet<string>? payloadKeySet) {
        if (payloadKeySet is null || payloadKeySet.Count == 0) {
            return payload;
        }

        var projected = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in payloadKeySet) {
            if (payload.TryGetValue(key, out var value)) {
                projected[key] = value;
            }
        }

        return projected;
    }

    private static string? ReadPayloadString(IReadOnlyDictionary<string, object?> payload, string key) {
        if (!payload.TryGetValue(key, out var value) || value is null) {
            return null;
        }

        var text = value.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static string? ReadPayloadUtc(IReadOnlyDictionary<string, object?> payload, string key) {
        if (!payload.TryGetValue(key, out var value) || value is null) {
            return null;
        }

        if (value is string text && TryParseUtcValue(text, out var parsedUtc)) {
            return parsedUtc == DateTime.MinValue
                ? null
                : parsedUtc.ToString("O");
        }

        if (value is DateTimeOffset dateTimeOffset) {
            return dateTimeOffset == DateTimeOffset.MinValue
                ? null
                : dateTimeOffset.UtcDateTime.ToString("O");
        }

        if (value is DateTime dateTime) {
            if (dateTime == DateTime.MinValue) {
                return null;
            }
            var parsed = dateTime.Kind switch {
                DateTimeKind.Utc => dateTime,
                DateTimeKind.Local => dateTime.ToUniversalTime(),
                _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
            };
            return parsed.ToString("O");
        }

        return value.ToString();
    }

    private static bool ShouldIncludeField(FieldInfo field) {
        if (field.Name.StartsWith("_", StringComparison.Ordinal)) {
            return false;
        }

        if (string.Equals(field.Name, nameof(EventTypeRecord.EventId), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(field.Name, nameof(EventTypeRecord.RecordId), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(field.Name, nameof(EventTypeRecord.MachineName), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(field.Name, nameof(EventTypeRecord.SourceLogName), StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        return !string.Equals(field.FieldType.Name, "EventObject", StringComparison.Ordinal);
    }

    private static bool ShouldIncludeProperty(PropertyInfo property) {
        if (property.DeclaringType == typeof(EventTypeRecord) ||
            property.Name.StartsWith("_", StringComparison.Ordinal) ||
            typeof(EventObject).IsAssignableFrom(property.PropertyType)) {
            return false;
        }

        return property.CanRead &&
               property.GetMethod is not null &&
               property.GetMethod.IsPublic &&
               property.GetIndexParameters().Length == 0;
    }

    private static object? NormalizeValue(object? value) {
        return value switch {
            null => null,
            DateTime dateTime => dateTime.ToUniversalTime().ToString("O"),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToUniversalTime().ToString("O"),
            Enum enumValue => enumValue.ToString(),
            _ => value
        };
    }

    private static string ToSnakeCase(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }

        if (SnakeCaseCache.TryGetValue(value, out var cached)) {
            return cached;
        }

        var normalized = ToSnakeCaseCore(value);
        if (SnakeCaseCache.Count < MaxSnakeCaseCacheEntries) {
            SnakeCaseCache.TryAdd(value, normalized);
        }

        return normalized;
    }

    private static string ToSnakeCaseCore(string value) {
        var sb = new StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++) {
            var c = value[i];
            if (!char.IsLetterOrDigit(c)) {
                if (sb.Length > 0 && sb[sb.Length - 1] != '_') {
                    sb.Append('_');
                }
                continue;
            }

            if (i > 0) {
                var prev = value[i - 1];
                var next = i + 1 < value.Length ? value[i + 1] : '\0';
                var shouldSplitUpper = char.IsUpper(c) &&
                    (char.IsLower(prev) || char.IsDigit(prev) || (char.IsUpper(prev) && next != '\0' && char.IsLower(next)));
                var shouldSplitDigit = char.IsDigit(c) && !char.IsDigit(prev);
                var shouldSplitLetter = char.IsLetter(c) && char.IsDigit(prev);

                if ((shouldSplitUpper || shouldSplitDigit || shouldSplitLetter) && sb.Length > 0 && sb[sb.Length - 1] != '_') {
                    sb.Append('_');
                }
            }

            sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString().Trim('_');
    }

    private sealed class PayloadExtractionPlan {
        public PayloadExtractionPlan(PayloadFieldAccessor[] fieldAccessors, PayloadPropertyAccessor[] propertyAccessors) {
            FieldAccessors = fieldAccessors;
            PropertyAccessors = propertyAccessors;
        }

        public PayloadFieldAccessor[] FieldAccessors { get; }
        public PayloadPropertyAccessor[] PropertyAccessors { get; }
    }

    private sealed class PayloadFieldAccessor {
        public PayloadFieldAccessor(FieldInfo field, string key) {
            Field = field;
            Key = key;
        }

        public FieldInfo Field { get; }
        public string Key { get; }
    }

    private sealed class PayloadPropertyAccessor {
        public PayloadPropertyAccessor(PropertyInfo property, string key) {
            Property = property;
            Key = key;
        }

        public PropertyInfo Property { get; }
        public string Key { get; }
    }
}
