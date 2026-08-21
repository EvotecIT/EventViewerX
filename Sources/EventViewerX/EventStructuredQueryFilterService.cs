using System.Collections;
using System.Globalization;
using System.Security.Principal;
using System.Text.Json;

namespace EventViewerX;

/// <summary>
/// Normalizes structured event query filters and builds XPath expressions from them.
/// </summary>
public static class EventStructuredQueryFilterService {
    /// <summary>
    /// Maximum event IDs accepted by the structured filter service.
    /// </summary>
    public const int MaxEventIds = 256;

    /// <summary>
    /// Maximum event record IDs accepted by the structured filter service.
    /// </summary>
    public const int MaxRecordIds = 256;

    /// <summary>
    /// Maximum provider-name length.
    /// </summary>
    public const int MaxProviderNameLength = 260;

    /// <summary>
    /// Maximum user-id length.
    /// </summary>
    public const int MaxUserIdLength = 260;

    /// <summary>
    /// Maximum named-data keys.
    /// </summary>
    public const int MaxNamedDataKeys = 32;

    /// <summary>
    /// Maximum values per named-data key.
    /// </summary>
    public const int MaxNamedDataValuesPerKey = 16;

    /// <summary>
    /// Maximum named-data key length.
    /// </summary>
    public const int MaxNamedDataKeyLength = 128;

    /// <summary>
    /// Maximum named-data value length.
    /// </summary>
    public const int MaxNamedDataValueLength = 256;

    /// <summary>
    /// Canonical level names accepted by the service.
    /// </summary>
    public static IReadOnlyList<string> LevelNames { get; } = GetEnumValues<Level>()
        .Select(value => ToSnakeCase(value.ToString()))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    /// <summary>
    /// Canonical keyword names accepted by the service.
    /// </summary>
    public static IReadOnlyList<string> KeywordNames { get; } = GetEnumValues<Keywords>()
        .Select(value => ToSnakeCase(value.ToString()))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static readonly IReadOnlyDictionary<string, Level> LevelsByName = BuildLevelMap();
    private static readonly IReadOnlyDictionary<string, Keywords> KeywordsByName = BuildKeywordMap();

    /// <summary>
    /// Returns true when any structured filter dimension is supplied.
    /// </summary>
    public static bool HasAny(EventStructuredQueryFilter? filter) {
        return filter is not null &&
               ((filter.EventIds?.Count ?? 0) > 0
                || !string.IsNullOrWhiteSpace(filter.ProviderName)
                || filter.StartTimeUtc.HasValue
                || filter.EndTimeUtc.HasValue
                || filter.Level.HasValue
                || filter.Keywords.HasValue
                || !string.IsNullOrWhiteSpace(filter.UserId)
                || (filter.RecordIds?.Count ?? 0) > 0
                || (filter.NamedDataFilter?.Count ?? 0) > 0
                || (filter.NamedDataExcludeFilter?.Count ?? 0) > 0);
    }

    /// <summary>
    /// Validates and normalizes structured filter input.
    /// </summary>
    public static bool TryNormalize(
        EventStructuredQueryFilterInput? input,
        out EventStructuredQueryFilter? filter,
        out string? error) {
        filter = null;
        error = null;

        if (input is null) {
            filter = new EventStructuredQueryFilter();
            return true;
        }

        if (!TryNormalizeIds(input.EventIds, "event_ids", MaxEventIds, out var eventIds, out error)) {
            return false;
        }

        if (!TryNormalizeIds(input.RecordIds, "event_record_ids", MaxRecordIds, out var recordIds, out error)) {
            return false;
        }

        if (!TryNormalizeBoundedText(input.ProviderName, "provider_name", MaxProviderNameLength, out var providerName, out error)) {
            return false;
        }

        if (!TryNormalizeUserId(input.UserId, out var userId, out error)) {
            return false;
        }

        if (!TryNormalizeLevel(input.Level, out var level, out error)) {
            return false;
        }

        if (!TryNormalizeKeywords(input.Keywords, out var keywords, out error)) {
            return false;
        }

        if (!TryNormalizeNamedDataFilter(input.NamedDataFilter, "named_data_filter", out var namedDataFilter, out error)) {
            return false;
        }

        if (!TryNormalizeNamedDataFilter(input.NamedDataExcludeFilter, "named_data_exclude_filter", out var namedDataExcludeFilter, out error)) {
            return false;
        }

        if (input.StartTimeUtc.HasValue &&
            input.EndTimeUtc.HasValue &&
            input.StartTimeUtc.Value > input.EndTimeUtc.Value) {
            error = "start_time_utc must be less than or equal to end_time_utc.";
            return false;
        }

        filter = new EventStructuredQueryFilter {
            EventIds = eventIds,
            ProviderName = providerName,
            StartTimeUtc = input.StartTimeUtc,
            EndTimeUtc = input.EndTimeUtc,
            Level = level,
            Keywords = keywords,
            UserId = userId,
            RecordIds = recordIds,
            NamedDataFilter = namedDataFilter,
            NamedDataExcludeFilter = namedDataExcludeFilter
        };
        return true;
    }

    /// <summary>
    /// Builds a raw XPath query from a normalized structured filter.
    /// </summary>
    public static string BuildXPath(EventStructuredQueryFilter? filter) {
        if (!HasAny(filter)) {
            return "*";
        }

        EventStructuredQueryFilter normalizedFilter = filter!;
        return EventFilterCompiler.BuildXPath(new EventFilter {
            EventIds = normalizedFilter.EventIds,
            RecordIds = normalizedFilter.RecordIds,
            ProviderNames = CreateSingleValueArray(
                normalizedFilter.ProviderName),
            StartTime = normalizedFilter.StartTimeUtc,
            EndTime = normalizedFilter.EndTimeUtc,
            Levels = normalizedFilter.Level.HasValue
                ? new[] { (byte)normalizedFilter.Level.Value }
                : null,
            Keywords = normalizedFilter.Keywords.HasValue
                ? new[] { (long)normalizedFilter.Keywords.Value }
                : null,
            UserIds = CreateSingleValueArray(normalizedFilter.UserId),
            NamedData = ConvertNamedData(
                normalizedFilter.NamedDataFilter),
            ExcludedNamedData = ConvertNamedData(
                normalizedFilter.NamedDataExcludeFilter)
        });
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>>?
        ConvertNamedData(Hashtable? values) {

        if (values == null || values.Count == 0) {
            return null;
        }
        var normalized =
            new Dictionary<string, IReadOnlyList<string>>(
                StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in values) {
            string key = Convert.ToString(
                entry.Key,
                CultureInfo.InvariantCulture) ?? string.Empty;
            if (key.Length == 0) {
                continue;
            }
            IEnumerable<object?> rawValues =
                entry.Value is IEnumerable enumerable &&
                entry.Value is not string
                    ? enumerable.Cast<object?>()
                    : new[] { entry.Value };
            string[] items = rawValues
                .Select(value => Convert.ToString(
                    value,
                    CultureInfo.InvariantCulture) ?? string.Empty)
                .ToArray();
            normalized[key] = items;
        }
        return normalized.Count == 0 ? null : normalized;
    }

    private static bool TryNormalizeBoundedText(
        string? value,
        string argumentName,
        int maxLength,
        out string? normalized,
        out string? error) {
        normalized = null;
        error = null;

        if (string.IsNullOrWhiteSpace(value)) {
            return true;
        }

        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length > maxLength) {
            error = $"{argumentName} must be <= {maxLength} characters.";
            return false;
        }

        for (var i = 0; i < trimmed.Length; i++) {
            if (char.IsControl(trimmed[i])) {
                error = $"{argumentName} must not contain control characters.";
                return false;
            }
        }

        normalized = trimmed;
        return true;
    }

    private static bool TryNormalizeUserId(string? value, out string? normalized, out string? error) {
        normalized = null;
        error = null;

        if (!TryNormalizeBoundedText(value, "user_id", MaxUserIdLength, out var bounded, out error)) {
            return false;
        }

        if (string.IsNullOrWhiteSpace(bounded)) {
            return true;
        }

        if (!TryResolveUserId(bounded!, out normalized)) {
            error = "user_id must be a SID or a resolvable account name.";
            return false;
        }

        return true;
    }

    private static bool TryNormalizeIds<T>(
        IReadOnlyList<T>? values,
        string argumentName,
        int maxItems,
        out IReadOnlyList<T>? normalized,
        out string? error)
        where T : struct, IComparable<T> {
        normalized = null;
        error = null;

        if (values is null || values.Count == 0) {
            return true;
        }

        if (values.Count > maxItems) {
            error = $"{argumentName} supports at most {maxItems} values.";
            return false;
        }

        var list = new List<T>(values.Count);
        var dedup = new HashSet<T>();
        foreach (var value in values) {
            if (value.CompareTo(default) <= 0) {
                error = argumentName == "event_ids"
                    ? $"{argumentName} values must be positive 32-bit integers."
                    : $"{argumentName} values must be positive integers.";
                return false;
            }

            if (dedup.Add(value)) {
                list.Add(value);
            }
        }

        normalized = list.Count == 0 ? null : list;
        return true;
    }

    private static bool TryNormalizeLevel(string? raw, out Level? level, out string? error) {
        level = null;
        error = null;

        if (TryParseSignedIntegerLiteral(raw, out var numericLiteral)) {
            if (numericLiteral < 0) {
                error = $"level must be any, a standard name ({string.Join(", ", LevelNames)}), or an integer from 0 through 255.";
                return false;
            }
        }

        var normalized = ToSnakeCase(raw ?? string.Empty);
        if (IsMalformedSignedToken(raw)) {
            error = $"level must be any, a standard name ({string.Join(", ", LevelNames)}), or an integer from 0 through 255.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(normalized) || string.Equals(normalized, "any", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        if (LevelsByName.TryGetValue(normalized, out var parsed)) {
            level = parsed;
            return true;
        }

        if (byte.TryParse(
                normalized,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out byte numericLevel)) {
            level = (Level)numericLevel;
            return true;
        }

        error = $"level must be any, a standard name ({string.Join(", ", LevelNames)}), or an integer from 0 through 255.";
        return false;
    }

    private static bool TryNormalizeKeywords(string? raw, out Keywords? keywords, out string? error) {
        keywords = null;
        error = null;

        if (TryParseSignedIntegerLiteral(raw, out var numericLiteral) && numericLiteral < 0) {
            error = $"keywords must be one of: any, {string.Join(", ", KeywordNames)}.";
            return false;
        }

        var normalized = ToSnakeCase(raw ?? string.Empty);
        if (IsMalformedSignedToken(raw)) {
            error = $"keywords must be one of: any, {string.Join(", ", KeywordNames)}.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(normalized) || string.Equals(normalized, "any", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        if (KeywordsByName.TryGetValue(normalized, out var parsed)) {
            if (Convert.ToInt64(parsed, CultureInfo.InvariantCulture) == 0L) {
                return true;
            }

            keywords = parsed;
            return true;
        }

        if (long.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericMask) && numericMask >= 0) {
            if (numericMask == 0) {
                return true;
            }

            keywords = (Keywords)numericMask;
            return true;
        }

        error = $"keywords must be one of: any, {string.Join(", ", KeywordNames)}.";
        return false;
    }

    private static bool TryNormalizeNamedDataFilter(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? raw,
        string argumentName,
        out Hashtable? filter,
        out string? error) {
        filter = null;
        error = null;

        if (raw is null) {
            return true;
        }

        if (raw.Count == 0) {
            error = $"{argumentName} must include at least one key.";
            return false;
        }

        if (raw.Count > MaxNamedDataKeys) {
            error = $"{argumentName} supports at most {MaxNamedDataKeys} keys.";
            return false;
        }

        var table = new Hashtable(StringComparer.Ordinal);
        foreach (var entry in raw) {
            var keyRaw = entry.Key;
            var rawValues = entry.Value;
            var key = (keyRaw ?? string.Empty).Trim();
            if (key.Length == 0) {
                error = $"{argumentName} keys must be non-empty strings.";
                return false;
            }

            for (var i = 0; i < key.Length; i++) {
                if (char.IsControl(key[i])) {
                    error = $"{argumentName} keys must not contain control characters.";
                    return false;
                }
            }

            if (key.Length > MaxNamedDataKeyLength) {
                error = $"{argumentName} keys must be <= {MaxNamedDataKeyLength} characters.";
                return false;
            }

            var values = rawValues ?? Array.Empty<string>();
            if (values.Count > MaxNamedDataValuesPerKey) {
                error = $"{argumentName}.{key} supports at most {MaxNamedDataValuesPerKey} values.";
                return false;
            }

            if (values.Count == 0) {
                if (string.Equals(argumentName, "named_data_exclude_filter", StringComparison.Ordinal)) {
                    error = $"{argumentName}.{key} must include at least one value.";
                    return false;
                }

                table[key] = Array.Empty<string>();
                continue;
            }

            var dedup = new List<string>(values.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rawValue in values) {
                var normalized = rawValue?.Trim() ?? string.Empty;
                if (normalized.Length > MaxNamedDataValueLength) {
                    error = $"{argumentName}.{key} values must be <= {MaxNamedDataValueLength} characters.";
                    return false;
                }

                for (var i = 0; i < normalized.Length; i++) {
                    if (char.IsControl(normalized[i])) {
                        error = $"{argumentName}.{key} values must not contain control characters.";
                        return false;
                    }
                }

                if (seen.Add(normalized)) {
                    dedup.Add(normalized);
                }
            }

            table[key] = dedup.ToArray();
        }

        filter = table.Count == 0 ? null : table;
        return true;
    }

    private static string ToSnakeCase(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }

        var replaced = value.Trim()
            .Replace('-', '_')
            .Replace(' ', '_');
        var normalized = TrimUnderscores(JsonNamingPolicy.SnakeCaseLower.ConvertName(replaced) ?? string.Empty);
        while (normalized.Contains("__", StringComparison.Ordinal)) {
            normalized = normalized.Replace("__", "_");
        }

        return normalized;
    }

    private static IReadOnlyDictionary<string, Level> BuildLevelMap() {
        var map = new Dictionary<string, Level>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in GetEnumValues<Level>()) {
            map[ToSnakeCase(value.ToString())] = value;
        }

        map["info"] = Level.Informational;
        map["information"] = Level.Informational;
        map["warn"] = Level.Warning;
        map["crit"] = Level.Critical;
        return map;
    }

    private static IReadOnlyDictionary<string, Keywords> BuildKeywordMap() {
        var map = new Dictionary<string, Keywords>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in GetEnumValues<Keywords>()) {
            map[ToSnakeCase(value.ToString())] = value;
        }

        return map;
    }

    private static IEnumerable<TEnum> GetEnumValues<TEnum>() where TEnum : struct {
        return Enum.GetValues(typeof(TEnum)).Cast<TEnum>();
    }

    private static string[]? CreateSingleValueArray(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }

        var normalized = (value ?? string.Empty).Trim();
        return new[] { normalized };
    }

    private static string TrimUnderscores(string value) {
        return value.Trim(UnderscoreTrimChars);
    }

    private static bool TryParseSignedIntegerLiteral(string? raw, out long value) {
        var trimmed = (raw ?? string.Empty).Trim();
        if (trimmed.Length == 0) {
            value = 0;
            return false;
        }

        return long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool IsMalformedSignedToken(string? raw) {
        var trimmed = (raw ?? string.Empty).Trim();
        if (trimmed.Length == 0) {
            return false;
        }

        if (!IsSignChar(trimmed[0])) {
            return false;
        }

        return !TryParseSignedIntegerLiteral(trimmed, out _);
    }

    internal static bool TryResolveUserId(string value, out string? normalized) {
        normalized = null;

        try {
            var sid = new SecurityIdentifier(value);
            normalized = sid.Value;
            return true;
        } catch (ArgumentException) {
            // Fall back to account-name resolution below.
        }

        try {
            var account = new NTAccount(value);
            normalized = account.Translate(typeof(SecurityIdentifier)).Value;
            return true;
        } catch (IdentityNotMappedException) {
            return false;
        } catch (SystemException) {
            return false;
        }
    }

    private static bool IsSignChar(char value) {
        return value == '-' || value == '+';
    }

    private static readonly char[] UnderscoreTrimChars = { '_' };
}
