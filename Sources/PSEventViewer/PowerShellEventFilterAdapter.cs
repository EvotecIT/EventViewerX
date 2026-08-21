using System.Collections;
using System.Globalization;

namespace PSEventViewer;

/// <summary>
/// Converts the public PowerShell hashtable contract into the reusable EventViewerX query model.
/// </summary>
internal static class PowerShellEventFilterAdapter {
    private static readonly HashSet<string> ReservedKeys = new(
        new[] {
            "LogName",
            "Path",
            "ProviderName",
            "Keywords",
            "Id",
            "Level",
            "StartTime",
            "EndTime",
            "UserID",
            "Data",
            "SuppressHashFilter"
        },
        StringComparer.OrdinalIgnoreCase);

    internal static PowerShellEventFilterBinding Bind(Hashtable source) {
        if (source == null) {
            throw new ArgumentNullException(nameof(source));
        }

        string[] logNames = ReadNormalizedStrings(source, "LogName");
        string[] paths = ReadNormalizedStrings(source, "Path");
        string[] providers = ReadNormalizedStrings(source, "ProviderName");
        if (logNames.Length == 0 &&
            paths.Length == 0 &&
            providers.Length == 0) {
            throw new PSArgumentException(
                "FilterHashtable must contain LogName, Path, or ProviderName.");
        }

        EventFilter select = BindFilter(source);
        EventFilter? suppress = null;
        if (TryGetValue(source, "SuppressHashFilter", out object? rawSuppress) &&
            rawSuppress != null) {
            Hashtable suppressTable = ConvertValue<Hashtable>(
                rawSuppress,
                "SuppressHashFilter");
            if (ContainsKey(suppressTable, "LogName") ||
                ContainsKey(suppressTable, "Path")) {
                throw new PSArgumentException(
                    "SuppressHashFilter describes event predicates only; LogName and Path belong in the outer FilterHashtable.");
            }
            suppress = BindFilter(suppressTable);
        }

        return new PowerShellEventFilterBinding(
            logNames,
            paths,
            select,
            suppress,
            logNames.Length == 0 &&
            paths.Length == 0 &&
            providers.Length > 0);
    }

    internal static EventFilter BindFilter(Hashtable source) {
        var namedData = new Dictionary<string, IReadOnlyList<string>>(
            StringComparer.Ordinal);
        foreach (DictionaryEntry entry in source) {
            string key = Convert.ToString(
                Unwrap(entry.Key),
                CultureInfo.InvariantCulture) ?? string.Empty;
            if (key.Length == 0 || ReservedKeys.Contains(key)) {
                continue;
            }
            namedData[key] = ReadLiteralStrings(entry.Value, key);
        }

        return new EventFilter {
            EventIds = ReadValues<int>(source, "Id"),
            ProviderNames = ReadNormalizedStrings(source, "ProviderName"),
            Keywords = ReadValues<long>(source, "Keywords"),
            Levels = ReadValues<byte>(source, "Level"),
            StartTime = ReadNullable<DateTime>(source, "StartTime"),
            EndTime = ReadNullable<DateTime>(source, "EndTime"),
            UserIds = ReadNormalizedStrings(source, "UserID"),
            Data = ReadLiteralStrings(source, "Data"),
            NamedData = namedData.Count == 0 ? null : namedData
        };
    }

    internal static EventFilter BindWatcherFilter(
        Hashtable source) {

        if (ContainsKey(source, "LogName") ||
            ContainsKey(source, "Path") ||
            ContainsKey(source, "SuppressHashFilter")) {
            throw new PSArgumentException(
                "Watcher FilterHashtable accepts event predicates only. Use the LogName parameter for the subscription source; Path and SuppressHashFilter are not supported by EvtSubscribe.");
        }
        EventFilter filter = BindFilter(source);
        return filter;
    }

    internal static EventFilter CreateFilter(
        IReadOnlyList<int>? eventIds,
        IReadOnlyList<long>? recordIds,
        IReadOnlyList<string>? providerNames,
        IReadOnlyList<Level>? levels,
        IReadOnlyList<long>? keywords,
        DateTime? startTime,
        DateTime? endTime,
        TimePeriod? timePeriod,
        IReadOnlyList<string>? userIds,
        IReadOnlyList<string>? data,
        Hashtable? namedData,
        Hashtable? excludedNamedData,
        IReadOnlyList<int>? excludedEventIds) {

        (DateTime? resolvedStart, DateTime? resolvedEnd) =
            EventTimeRange.Resolve(startTime, endTime, timePeriod);
        return new EventFilter {
            EventIds = eventIds,
            RecordIds = recordIds,
            ProviderNames = Normalize(providerNames),
            Levels = levels?.Select(static value => (byte)value).ToArray(),
            Keywords = keywords,
            StartTime = resolvedStart,
            EndTime = resolvedEnd,
            UserIds = Normalize(userIds),
            Data = data,
            NamedData = ConvertNamedData(namedData),
            ExcludedNamedData = ConvertNamedData(excludedNamedData),
            ExcludedEventIds = excludedEventIds
        };
    }

    internal static IReadOnlyDictionary<string, IReadOnlyList<string>>?
        ConvertNamedData(Hashtable? table) {

        if (table == null || table.Count == 0) {
            return null;
        }
        var output = new Dictionary<string, IReadOnlyList<string>>(
            StringComparer.Ordinal);
        foreach (DictionaryEntry entry in table) {
            string key = EventFilterValueConverter
                .ToInvariantString(Unwrap(entry.Key))
                .Trim();
            if (key.Length == 0) {
                throw new PSArgumentException(
                    "Named-data filter keys cannot be empty.");
            }
            output[key] = entry.Value == null
                ? new[] { string.Empty }
                : Enumerate(entry.Value)
                    .Select(EventFilterValueConverter.ToInvariantString)
                    .ToArray();
        }
        return output;
    }

    private static IReadOnlyList<string>? Normalize(
        IReadOnlyList<string>? values) {

        string[] normalized = values?
            .Select(static value => value?.Trim() ?? string.Empty)
            .Where(static value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();
        return normalized.Length == 0 ? null : normalized;
    }

    private static IReadOnlyList<T>? ReadValues<T>(
        Hashtable source,
        string key) {

        if (!TryGetValue(source, key, out object? raw) || raw == null) {
            return null;
        }
        return Enumerate(raw)
            .Select(value => ConvertValue<T>(value, key))
            .Distinct()
            .ToArray();
    }

    private static T? ReadNullable<T>(
        Hashtable source,
        string key) where T : struct {

        if (!TryGetValue(source, key, out object? raw) || raw == null) {
            return null;
        }
        object[] values = Enumerate(raw).ToArray();
        if (values.Length != 1) {
            throw new PSArgumentException(
                $"FilterHashtable key '{key}' accepts one value.");
        }
        return ConvertValue<T>(values[0], key);
    }

    private static string[] ReadNormalizedStrings(
        Hashtable source,
        string key) {

        return TryGetValue(source, key, out object? raw)
            ? ReadNormalizedStrings(raw, key)
            : Array.Empty<string>();
    }

    private static string[] ReadNormalizedStrings(
        object? raw,
        string key) {

        if (raw == null) {
            return Array.Empty<string>();
        }
        return Enumerate(raw)
            .Select(value => ConvertValue<string>(value, key).Trim())
            .Where(static value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] ReadLiteralStrings(
        Hashtable source,
        string key) {

        return TryGetValue(source, key, out object? raw)
            ? ReadLiteralStrings(raw, key)
            : Array.Empty<string>();
    }

    private static string[] ReadLiteralStrings(
        object? raw,
        string key) {

        if (raw == null) {
            return Array.Empty<string>();
        }
        return Enumerate(raw)
            .Select(value => ConvertValue<string>(value, key))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<object> Enumerate(object raw) {
        raw = Unwrap(raw);
        if (raw is string || raw is not IEnumerable values) {
            yield return raw;
            yield break;
        }
        foreach (object? value in values) {
            if (value != null) {
                yield return Unwrap(value);
            }
        }
    }

    private static T ConvertValue<T>(
        object value,
        string key) {

        try {
            return LanguagePrimitives.ConvertTo<T>(Unwrap(value));
        } catch (PSInvalidCastException exception) {
            throw new PSArgumentException(
                $"FilterHashtable key '{key}' contains an invalid value: {exception.Message}",
                exception);
        }
    }

    private static object Unwrap(object value) {
        if (value is PSObject psObject && psObject.BaseObject != null) {
            return psObject.BaseObject;
        }
        return value;
    }

    private static bool ContainsKey(
        Hashtable source,
        string key) {

        return TryGetValue(source, key, out _);
    }

    private static bool TryGetValue(
        Hashtable source,
        string key,
        out object? value) {

        foreach (DictionaryEntry entry in source) {
            if (string.Equals(
                    Convert.ToString(entry.Key, CultureInfo.InvariantCulture),
                    key,
                    StringComparison.OrdinalIgnoreCase)) {
                value = entry.Value;
                return true;
            }
        }
        value = null;
        return false;
    }
}

internal sealed class PowerShellEventFilterBinding {
    internal PowerShellEventFilterBinding(
        IReadOnlyList<string> logNames,
        IReadOnlyList<string> paths,
        EventFilter select,
        EventFilter? suppress,
        bool providerOnly) {

        LogNames = logNames;
        Paths = paths;
        Select = select;
        Suppress = suppress;
        ProviderOnly = providerOnly;
    }

    internal IReadOnlyList<string> LogNames { get; }
    internal IReadOnlyList<string> Paths { get; }
    internal bool UsesChannels => LogNames.Count > 0;
    internal EventFilter Select { get; }
    internal EventFilter? Suppress { get; }
    internal bool UsesFiles => Paths.Count > 0;
    internal bool ProviderOnly { get; }
}
