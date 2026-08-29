using System.Collections;
using System.Net;

namespace EventViewerX;

/// <summary>Describes one strongly typed field emitted by an event definition.</summary>
public sealed class EventFieldDefinition {
    internal EventFieldDefinition(
        string name,
        string displayName,
        Type valueType,
        bool isCommon,
        string description = "",
        IReadOnlyList<string>? aliases = null,
        EventFieldFilterStage? filterStage = null) {

        Name = name;
        DisplayName = displayName;
        ValueType = valueType;
        IsCommon = isCommon;
        Description = string.IsNullOrWhiteSpace(description)
            ? ResolveDescription(name, displayName, isCommon)
            : description.Trim();
        Aliases = aliases ?? ResolveAliases(name);
        FilterStage = filterStage ?? ResolveFilterStage(name);
        IsFilterable = IsSupportedType(valueType);
        SupportedOperators = IsFilterable
            ? ResolveOperators(name, valueType)
            : Array.Empty<EventPredicateOperator>();
    }

    /// <summary>CLR member name on the projected record.</summary>
    public string Name { get; }

    /// <summary>Human-friendly field label.</summary>
    public string DisplayName { get; }

    /// <summary>CLR value type.</summary>
    public Type ValueType { get; }

    /// <summary>Whether the field belongs to every typed event record.</summary>
    public bool IsCommon { get; }

    /// <summary>Field purpose shown by discovery surfaces.</summary>
    public string Description { get; }

    /// <summary>Alternative accepted field names.</summary>
    public IReadOnlyList<string> Aliases { get; }

    /// <summary>Earliest safe filter stage.</summary>
    public EventFieldFilterStage FilterStage { get; }

    /// <summary>Whether the value type has defined predicate semantics.</summary>
    public bool IsFilterable { get; }

    /// <summary>Comparison operations supported by this field type.</summary>
    public IReadOnlyList<EventPredicateOperator> SupportedOperators { get; }

    private static IReadOnlyList<string> ResolveAliases(string name) {
        if (string.Equals(name, "EventId", StringComparison.OrdinalIgnoreCase)) {
            return new[] { "Id" };
        }
        if (string.Equals(name, "RecordId", StringComparison.OrdinalIgnoreCase)) {
            return new[] { "EventRecordId" };
        }
        if (string.Equals(name, "TimeCreated", StringComparison.OrdinalIgnoreCase)) {
            return new[] { "When" };
        }
        if (string.Equals(name, "ProviderName", StringComparison.OrdinalIgnoreCase)) {
            return new[] { "Provider" };
        }
        if (string.Equals(name, "SourceComputer", StringComparison.OrdinalIgnoreCase)) {
            return new[] { "Computer" };
        }
        if (string.Equals(name, "SourceLogName", StringComparison.OrdinalIgnoreCase)) {
            return new[] { "LogName" };
        }
        return Array.Empty<string>();
    }

    private static EventFieldFilterStage ResolveFilterStage(string name) {
        return IsNativeField(name)
            ? EventFieldFilterStage.Native
            : EventFieldFilterStage.Managed;
    }

    private static string ResolveDescription(string name, string displayName, bool isCommon) => name switch {
        "EventId" => "Windows event identifier used by the source provider.",
        "RecordId" => "Monotonic record identifier within the source event channel.",
        "MachineName" => "Computer recorded by the original Windows event.",
        "SourceLogName" => "Original Windows event channel, including events read through ForwardedEvents.",
        "ContainerLogName" => "Channel or EVTX container from which EventViewerX read the event.",
        "SourceComputer" => "Original source computer that produced the event.",
        "CollectorComputer" => "Computer queried directly or used as the Windows Event Collector.",
        "TypeName" => "Stable leaf definition name assigned by the typed projection.",
        "TimeCreated" => "Timestamp recorded by the original Windows event.",
        "ProviderName" => "Windows event provider that emitted the event.",
        "Message" => "Provider-formatted event message when the selected read mode includes it.",
        "Who" => "Account or identity associated with the typed activity.",
        "When" => "Timestamp associated with the typed activity.",
        "Action" => "Normalized action described by the typed event.",
        "Computer" => "Computer associated with the typed activity.",
        _ when isCommon => $"Common typed event field '{displayName}'.",
        _ => $"Domain value projected from the event as '{displayName}'."
    };

    internal static bool IsNativeField(string name) =>
        new[] { "EventId", "Id", "RecordId", "EventRecordId", "TimeCreated", "ProviderName", "Provider", "Level" }
            .Contains(name, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<EventPredicateOperator> ResolveOperators(string name, Type type) {
        Type effectiveType = Nullable.GetUnderlyingType(type) ?? type;
        var values = new List<EventPredicateOperator> {
            EventPredicateOperator.Equal,
            EventPredicateOperator.NotEqual,
            EventPredicateOperator.In,
            EventPredicateOperator.NotIn,
            EventPredicateOperator.IsNull,
            EventPredicateOperator.IsNotNull
        };
        if (effectiveType == typeof(string)) {
            values.AddRange(new[] {
                EventPredicateOperator.Contains,
                EventPredicateOperator.StartsWith,
                EventPredicateOperator.EndsWith,
                EventPredicateOperator.MatchesWildcard,
                EventPredicateOperator.MatchesRegex
            });
            if (IsIpAddressField(name)) {
                values.Add(EventPredicateOperator.InSubnet);
            }
        } else if (effectiveType == typeof(IPAddress)) {
            values.Add(EventPredicateOperator.InSubnet);
        } else if (TryGetEnumerableElementType(effectiveType, out _)) {
            values.Add(EventPredicateOperator.Contains);
        } else if (effectiveType != typeof(bool) && effectiveType != typeof(Guid) && !effectiveType.IsEnum) {
            values.AddRange(new[] {
                EventPredicateOperator.GreaterThan,
                EventPredicateOperator.GreaterThanOrEqual,
                EventPredicateOperator.LessThan,
                EventPredicateOperator.LessThanOrEqual
            });
        }
        return values;
    }

    private static bool IsIpAddressField(string name) =>
        name.EndsWith("IpAddress", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("IPAddress", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("RemoteIp", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("ClientAddress", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("NASIPv4Address", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("NASIPv6Address", StringComparison.OrdinalIgnoreCase);

    private static bool IsSupportedType(Type type) {
        Type effectiveType = Nullable.GetUnderlyingType(type) ?? type;
        if (effectiveType == typeof(string) || effectiveType == typeof(decimal) ||
            effectiveType == typeof(DateTime) || effectiveType == typeof(DateTimeOffset) ||
            effectiveType == typeof(Guid) || effectiveType == typeof(IPAddress) ||
            effectiveType.IsPrimitive || effectiveType.IsEnum) {
            return true;
        }
        return TryGetEnumerableElementType(effectiveType, out Type? elementType) &&
               elementType != null && IsSupportedType(elementType);
    }

#if NET5_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2070", Justification =
        "Field schema inspection is best-effort; built-in record metadata is rooted by explicit registration.")]
#endif
    internal static bool TryGetEnumerableElementType(Type type, out Type? elementType) {
        elementType = null;
        if (type == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(type)) {
            return false;
        }
        if (type.IsArray) {
            elementType = type.GetElementType();
            return elementType != null;
        }
        Type? enumerable = type.GetInterfaces()
            .Concat(new[] { type })
            .FirstOrDefault(candidate =>
                candidate.IsGenericType &&
                candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        if (enumerable == null) {
            return false;
        }
        elementType = enumerable.GetGenericArguments()[0];
        return true;
    }
}
