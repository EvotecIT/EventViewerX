using System.Collections;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace EventViewerX;

/// <summary>Compiles reusable typed event predicates into fast managed delegates.</summary>
public static class EventPredicateEvaluator {
    private static readonly ConcurrentDictionary<(Type Type, string Field), Func<object, object?>> Accessors = new();
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    /// <summary>Compiles a predicate for built-in typed records.</summary>
    public static Func<EventTypeRecord, bool> Compile(EventPredicate predicate) {
        EventPredicate snapshot = ValidateAndClone(predicate);
        Func<object, bool> compiled = CompileCore(snapshot);
        return record => record != null && EvaluateSafely(compiled, record);
    }

    /// <summary>Compiles a predicate for declarative custom records.</summary>
    public static Func<CustomEventRecord, bool> CompileCustom(EventPredicate predicate) {
        EventPredicate snapshot = ValidateAndClone(predicate);
        Func<object, bool> compiled = CompileCore(snapshot);
        return record => record != null && EvaluateSafely(compiled, record);
    }

    /// <summary>Compiles a predicate for a case-insensitive canonical field dictionary.</summary>
    public static Func<IReadOnlyDictionary<string, object?>, bool> CompileFields(EventPredicate predicate) {
        EventPredicate snapshot = ValidateAndClone(predicate);
        Func<object, bool> compiled = CompileCore(snapshot);
        return fields => fields != null && EvaluateSafely(compiled, fields);
    }

    /// <summary>Evaluates a predicate against a built-in or custom event record.</summary>
    public static bool Matches(EventPredicate predicate, object record) {
        if (record == null) {
            throw new ArgumentNullException(nameof(record));
        }
        predicate?.Validate();
        return EvaluateSafely(predicate!, record);
    }

    /// <summary>Evaluates a predicate against a case-insensitive field dictionary.</summary>
    public static bool Matches(
        EventPredicate predicate,
        IReadOnlyDictionary<string, object?> fields) {

        if (fields == null) {
            throw new ArgumentNullException(nameof(fields));
        }
        predicate?.Validate();
        return EvaluateSafely(predicate!, fields);
    }

    private static EventPredicate ValidateAndClone(EventPredicate predicate) {
        if (predicate == null) {
            throw new ArgumentNullException(nameof(predicate));
        }
        predicate.Validate();
        return predicate.Clone();
    }

    private static bool EvaluateSafely(EventPredicate predicate, object record) {
        try {
            return EvaluateCore(predicate, record);
        } catch (ArgumentException) {
            return false;
        } catch (FormatException) {
            return false;
        } catch (InvalidCastException) {
            return false;
        } catch (OverflowException) {
            return false;
        } catch (RegexMatchTimeoutException) {
            return false;
        }
    }

    private static bool EvaluateSafely(Func<object, bool> compiled, object record) {
        try {
            return compiled(record);
        } catch (ArgumentException) {
            return false;
        } catch (FormatException) {
            return false;
        } catch (InvalidCastException) {
            return false;
        } catch (OverflowException) {
            return false;
        } catch (RegexMatchTimeoutException) {
            return false;
        }
    }

    private static Func<object, bool> CompileCore(EventPredicate predicate) {
        switch (predicate.Kind) {
            case EventPredicateKind.All: {
                Func<object, bool>[] children = predicate.Children.Select(CompileCore).ToArray();
                return record => children.All(child => child(record));
            }
            case EventPredicateKind.Any: {
                Func<object, bool>[] children = predicate.Children.Select(CompileCore).ToArray();
                return record => children.Any(child => child(record));
            }
            case EventPredicateKind.Not: {
                Func<object, bool> child = CompileCore(predicate.Children[0]);
                return record => !child(record);
            }
            case EventPredicateKind.Comparison: {
                string field = predicate.Field!;
                EventPredicateOperator comparison = predicate.Operator;
                string?[] expectedValues = predicate.Values.ToArray();
                bool ignoreCase = predicate.IgnoreCase;
                Regex? pattern = CreateCompiledPattern(comparison, expectedValues, ignoreCase);
                return record => {
                    object? actual = ResolveValue(record, field);
                    return pattern != null
                        ? actual != null && pattern.IsMatch(ToText(actual))
                        : Compare(actual, comparison, expectedValues, ignoreCase);
                };
            }
            default:
                throw new InvalidOperationException($"Unsupported predicate kind '{predicate.Kind}'.");
        }
    }

    private static Regex? CreateCompiledPattern(
        EventPredicateOperator comparison,
        IReadOnlyList<string?> expectedValues,
        bool ignoreCase) {

        if (comparison is not EventPredicateOperator.MatchesWildcard and not EventPredicateOperator.MatchesRegex) {
            return null;
        }
        string expression = comparison == EventPredicateOperator.MatchesWildcard
            ? ToPowerShellWildcardRegex(expectedValues[0] ?? string.Empty)
            : expectedValues[0] ?? string.Empty;
        RegexOptions options = RegexOptions.CultureInvariant | RegexOptions.Compiled;
        if (ignoreCase) {
            options |= RegexOptions.IgnoreCase;
        }
        return new Regex(expression, options, RegexTimeout);
    }

    private static bool EvaluateCore(EventPredicate predicate, object record) {
        switch (predicate.Kind) {
            case EventPredicateKind.All:
                return predicate.Children.All(child => EvaluateCore(child, record));
            case EventPredicateKind.Any:
                return predicate.Children.Any(child => EvaluateCore(child, record));
            case EventPredicateKind.Not:
                return !EvaluateCore(predicate.Children[0], record);
            case EventPredicateKind.Comparison:
                object? actual = ResolveValue(record, predicate.Field!);
                return Compare(actual, predicate.Operator, predicate.Values, predicate.IgnoreCase);
            default:
                throw new InvalidOperationException($"Unsupported predicate kind '{predicate.Kind}'.");
        }
    }

    private static object? ResolveValue(object record, string field) {
        if (record is IReadOnlyDictionary<string, object?> fields) {
            return fields.TryGetValue(field, out object? dictionaryValue)
                ? dictionaryValue
                : fields.FirstOrDefault(item => string.Equals(
                    item.Key,
                    field,
                    StringComparison.OrdinalIgnoreCase)).Value;
        }
        if (record is CustomEventRecord custom) {
            if (custom.Values.TryGetValue(field, out object? customValue)) {
                return customValue;
            }
            return ResolveEventValue(custom.SourceEvent, field);
        }
        Func<object, object?> accessor = Accessors.GetOrAdd(
            (record.GetType(), field),
            static key => CreateAccessor(key.Type, key.Field));
        object? value = accessor(record);
        if (value != MissingValue.Instance) {
            return value;
        }
        return record is EventTypeRecord typed
            ? ResolveEventValue(typed.SourceEvent, field)
            : null;
    }

    private static Func<object, object?> CreateAccessor(Type type, string field) {
        PropertyInfo? property = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(candidate =>
                candidate.GetIndexParameters().Length == 0 &&
                string.Equals(candidate.Name, field, StringComparison.OrdinalIgnoreCase));
        if (property != null) {
            return instance => property.GetValue(instance, null);
        }
        FieldInfo? member = type.GetFields(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(candidate => string.Equals(candidate.Name, field, StringComparison.OrdinalIgnoreCase));
        return member != null
            ? instance => member.GetValue(instance)
            : _ => MissingValue.Instance;
    }

    private static object? ResolveEventValue(EventObject source, string field) {
        if (string.Equals(field, "EventId", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(field, "Id", StringComparison.OrdinalIgnoreCase)) {
            return source.Id;
        }
        if (string.Equals(field, "RecordId", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(field, "EventRecordId", StringComparison.OrdinalIgnoreCase)) {
            return source.RecordId;
        }
        if (string.Equals(field, "TimeCreated", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(field, "When", StringComparison.OrdinalIgnoreCase)) {
            return source.TimeCreated;
        }
        if (string.Equals(field, "ProviderName", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(field, "Provider", StringComparison.OrdinalIgnoreCase)) {
            return source.ProviderName;
        }
        if (string.Equals(field, "Level", StringComparison.OrdinalIgnoreCase)) {
            return source.Level.HasValue
                ? (Level?)source.Level.Value
                : null;
        }
        if (string.Equals(field, "Message", StringComparison.OrdinalIgnoreCase)) {
            return source.Message;
        }
        if (string.Equals(field, "SourceComputer", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(field, "Computer", StringComparison.OrdinalIgnoreCase)) {
            return source.SourceComputer;
        }
        if (string.Equals(field, "SourceLogName", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(field, "LogName", StringComparison.OrdinalIgnoreCase)) {
            return source.OriginalLogName;
        }
        return source.Data.TryGetValue(field, out string? dataValue)
            ? dataValue
            : null;
    }

    private static bool Compare(
        object? actual,
        EventPredicateOperator comparison,
        IReadOnlyList<string?> expectedValues,
        bool ignoreCase) {

        if (comparison == EventPredicateOperator.IsNull) {
            return actual == null;
        }
        if (comparison == EventPredicateOperator.IsNotNull) {
            return actual != null;
        }
        if (comparison == EventPredicateOperator.NotIn) {
            return !expectedValues.Any(expected => Equal(actual, expected, ignoreCase));
        }
        if (comparison == EventPredicateOperator.In) {
            return expectedValues.Any(expected => Equal(actual, expected, ignoreCase));
        }
        if (actual == null) {
            return comparison == EventPredicateOperator.NotEqual && expectedValues[0] != null;
        }
        string? expectedValue = expectedValues[0];
        switch (comparison) {
            case EventPredicateOperator.Equal:
                return Equal(actual, expectedValue, ignoreCase);
            case EventPredicateOperator.NotEqual:
                return NotEqual(actual, expectedValue, ignoreCase);
            case EventPredicateOperator.Contains:
                return Contains(actual, expectedValue, ignoreCase);
            case EventPredicateOperator.StartsWith:
                return ToText(actual).StartsWith(expectedValue ?? string.Empty, TextComparison(ignoreCase));
            case EventPredicateOperator.EndsWith:
                return ToText(actual).EndsWith(expectedValue ?? string.Empty, TextComparison(ignoreCase));
            case EventPredicateOperator.MatchesWildcard:
                return Regex.IsMatch(
                    ToText(actual),
                    ToPowerShellWildcardRegex(expectedValue ?? string.Empty),
                    RegexOptions.CultureInvariant | (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None),
                    RegexTimeout);
            case EventPredicateOperator.MatchesRegex:
                return Regex.IsMatch(
                    ToText(actual),
                    expectedValue ?? string.Empty,
                    RegexOptions.CultureInvariant | (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None),
                    RegexTimeout);
            case EventPredicateOperator.GreaterThan:
                return CompareOrdered(actual, expectedValue, ignoreCase) > 0;
            case EventPredicateOperator.GreaterThanOrEqual:
                return CompareOrdered(actual, expectedValue, ignoreCase) >= 0;
            case EventPredicateOperator.LessThan:
                return CompareOrdered(actual, expectedValue, ignoreCase) < 0;
            case EventPredicateOperator.LessThanOrEqual:
                return CompareOrdered(actual, expectedValue, ignoreCase) <= 0;
            case EventPredicateOperator.InSubnet:
                return IsInSubnet(actual, expectedValue);
            default:
                throw new InvalidOperationException($"Unsupported predicate operator '{comparison}'.");
        }
    }

    private static bool Equal(object? actual, string? expected, bool ignoreCase) {
        if (actual == null) {
            return expected == null;
        }
        if (actual is IEnumerable enumerable && actual is not string) {
            foreach (object? item in enumerable) {
                if (EqualScalar(item, expected, ignoreCase)) {
                    return true;
                }
            }
            return false;
        }
        return EqualScalar(actual, expected, ignoreCase);
    }

    private static bool EqualScalar(object? actual, string? expected, bool ignoreCase) {
        if (actual == null) {
            return expected == null;
        }
        object? converted = ConvertExpected(expected, actual.GetType(), ignoreCase);
        if (actual is DateTime actualDateTime && converted is DateTime expectedDateTime) {
            return actualDateTime.ToUniversalTime() == expectedDateTime.ToUniversalTime();
        }
        if (actual is DateTimeOffset actualOffset && converted is DateTimeOffset expectedOffset) {
            return actualOffset.ToUniversalTime() == expectedOffset.ToUniversalTime();
        }
        if (actual is string actualText) {
            return string.Equals(actualText, converted as string, TextComparison(ignoreCase));
        }
        return Equals(actual, converted) ||
               string.Equals(ToText(actual), expected, TextComparison(ignoreCase));
    }

    private static bool NotEqual(object actual, string? expected, bool ignoreCase) {
        if (actual is IEnumerable enumerable && actual is not string) {
            foreach (object? item in enumerable) {
                if (!EqualScalar(item, expected, ignoreCase)) {
                    return true;
                }
            }
            return false;
        }
        return !EqualScalar(actual, expected, ignoreCase);
    }

    private static bool Contains(object? actual, string? expected, bool ignoreCase) {
        if (actual is IEnumerable enumerable && actual is not string) {
            foreach (object? item in enumerable) {
                if (EqualScalar(item, expected, ignoreCase)) {
                    return true;
                }
            }
            return false;
        }
        return ToText(actual).IndexOf(expected ?? string.Empty, TextComparison(ignoreCase)) >= 0;
    }

    private static string ToPowerShellWildcardRegex(string pattern) {
        var result = new StringBuilder("^");
        for (int index = 0; index < pattern.Length; index++) {
            char current = pattern[index];
            if (current == '`' && index + 1 < pattern.Length) {
                result.Append(Regex.Escape(pattern[++index].ToString()));
                continue;
            }
            if (current == '*') {
                result.Append(".*");
                continue;
            }
            if (current == '?') {
                result.Append('.');
                continue;
            }
            if (current == '[' && TryAppendWildcardCharacterClass(pattern, ref index, result)) {
                continue;
            }
            result.Append(Regex.Escape(current.ToString()));
        }
        return result.Append('$').ToString();
    }

    private static bool TryAppendWildcardCharacterClass(
        string pattern,
        ref int index,
        StringBuilder result) {

        int close = index + 1;
        while (close < pattern.Length) {
            if (pattern[close] == '`' && close + 1 < pattern.Length) {
                close += 2;
                continue;
            }
            if (pattern[close] == ']') {
                break;
            }
            close++;
        }
        if (close >= pattern.Length || close == index + 1) {
            return false;
        }
        result.Append('[');
        for (int current = index + 1; current < close; current++) {
            char value = pattern[current];
            bool wasEscaped = value == '`' && current + 1 < close;
            if (wasEscaped) {
                value = pattern[++current];
            }
            if (value is '\\' or ']' or '^' || (wasEscaped && value == '-')) {
                result.Append('\\');
            }
            result.Append(value);
        }
        result.Append(']');
        index = close;
        return true;
    }

    private static int CompareOrdered(object actual, string? expected, bool ignoreCase) {
        object? converted = ConvertExpected(expected, actual.GetType(), ignoreCase);
        if (actual is DateTime actualDateTime && converted is DateTime expectedDateTime) {
            return actualDateTime.ToUniversalTime().CompareTo(expectedDateTime.ToUniversalTime());
        }
        if (actual is DateTimeOffset actualOffset && converted is DateTimeOffset expectedOffset) {
            return actualOffset.ToUniversalTime().CompareTo(expectedOffset.ToUniversalTime());
        }
        if (actual is IComparable comparable && converted != null) {
            return comparable.CompareTo(converted);
        }
        return string.Compare(ToText(actual), expected, TextComparison(ignoreCase));
    }

    internal static bool TryConvertExpected(
        string? value,
        Type targetType,
        bool ignoreCase,
        out object? converted) {

        try {
            converted = ConvertExpected(value, targetType, ignoreCase);
            return true;
        } catch (ArgumentException) {
        } catch (FormatException) {
        } catch (InvalidCastException) {
        } catch (OverflowException) {
        }
        converted = null;
        return false;
    }

    private static object? ConvertExpected(string? value, Type targetType, bool ignoreCase) {
        if (value == null) {
            return null;
        }
        Type effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (effectiveType == typeof(string)) {
            return value;
        }
        if (effectiveType.IsEnum) {
            return Enum.Parse(effectiveType, value, ignoreCase);
        }
        if (effectiveType == typeof(Guid)) {
            return Guid.Parse(value);
        }
        if (effectiveType == typeof(DateTime)) {
            return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }
        if (effectiveType == typeof(DateTimeOffset)) {
            return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }
        if (effectiveType == typeof(IPAddress)) {
            return IPAddress.Parse(value);
        }
        return Convert.ChangeType(value, effectiveType, CultureInfo.InvariantCulture);
    }

    private static bool IsInSubnet(object? actual, string? subnet) {
        if (actual == null || string.IsNullOrWhiteSpace(subnet)) {
            return false;
        }
        string[] parts = subnet!.Split('/');
        if (parts.Length != 2 ||
            !IPAddress.TryParse(parts[0], out IPAddress? network) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int prefix) ||
            !IPAddress.TryParse(ToText(actual), out IPAddress? address)) {
            return false;
        }
        byte[] networkBytes = network.GetAddressBytes();
        byte[] addressBytes = address.GetAddressBytes();
        if (networkBytes.Length != addressBytes.Length || prefix < 0 || prefix > networkBytes.Length * 8) {
            return false;
        }
        int wholeBytes = prefix / 8;
        int remainingBits = prefix % 8;
        for (int index = 0; index < wholeBytes; index++) {
            if (networkBytes[index] != addressBytes[index]) {
                return false;
            }
        }
        if (remainingBits == 0) {
            return true;
        }
        int mask = 0xFF << (8 - remainingBits);
        return (networkBytes[wholeBytes] & mask) == (addressBytes[wholeBytes] & mask);
    }

    private static string ToText(object? value) =>
        Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

    private static StringComparison TextComparison(bool ignoreCase) =>
        ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private sealed class MissingValue {
        internal static MissingValue Instance { get; } = new();
    }
}
