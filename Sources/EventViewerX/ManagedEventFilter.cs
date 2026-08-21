namespace EventViewerX;

/// <summary>
/// Compiles an EventFilter into a bounded managed predicate for Windows
/// channels that cannot safely execute filtered native XPath.
/// </summary>
internal sealed class ManagedEventFilter {
    private readonly HashSet<int>? _eventIds;
    private readonly HashSet<long>? _recordIds;
    private readonly long? _minimumRecordIdExclusive;
    private readonly long? _maximumRecordIdExclusive;
    private readonly HashSet<string>? _providerNames;
    private readonly HashSet<byte>? _levels;
    private readonly long? _keywordMask;
    private readonly HashSet<string>? _userIds;
    private readonly HashSet<string>? _data;
    private readonly IReadOnlyDictionary<string, HashSet<string>?>? _namedData;
    private readonly IReadOnlyDictionary<string, HashSet<string>?>? _excludedNamedData;
    private readonly HashSet<int>? _excludedEventIds;

    private ManagedEventFilter(EventFilter filter) {
        _eventIds = ToSet(filter.EventIds);
        _recordIds = ToSet(filter.RecordIds);
        _minimumRecordIdExclusive = filter.MinimumRecordIdExclusive;
        _maximumRecordIdExclusive = filter.MaximumRecordIdExclusive;
        _providerNames = ToStringSet(filter.ProviderNames);
        _levels = ToSet(filter.Levels);
        _keywordMask = CombineKeywords(filter.Keywords);
        _userIds = ResolveUserIds(filter.UserIds);
        _data = ToStringSet(filter.Data);
        _namedData = ToNamedData(filter.NamedData);
        _excludedNamedData = ToNamedData(filter.ExcludedNamedData);
        _excludedEventIds = ToSet(filter.ExcludedEventIds);
        ValidateBounds();
    }

    internal static Func<EventObject, bool>? CreatePredicate(
        EventFilter? filter) {

        if (filter == null || !filter.HasAny) {
            return null;
        }
        var compiled = new ManagedEventFilter(filter.Clone());
        return compiled.Matches;
    }

    internal static bool RequiresStructuredData(EventFilter? filter) {
        return (filter?.Data?.Count ?? 0) > 0 ||
               (filter?.NamedData?.Count ?? 0) > 0 ||
               (filter?.ExcludedNamedData?.Count ?? 0) > 0;
    }

    private bool Matches(EventObject value) {
        if (_eventIds != null && !_eventIds.Contains(value.Id) ||
            _excludedEventIds != null && _excludedEventIds.Contains(value.Id) ||
            _recordIds != null &&
            (!value.RecordId.HasValue || !_recordIds.Contains(value.RecordId.Value)) ||
            _minimumRecordIdExclusive.HasValue &&
            (!value.RecordId.HasValue || value.RecordId.Value <= _minimumRecordIdExclusive.Value) ||
            _maximumRecordIdExclusive.HasValue &&
            (!value.RecordId.HasValue || value.RecordId.Value >= _maximumRecordIdExclusive.Value) ||
            _providerNames != null && !_providerNames.Contains(value.ProviderName) ||
            _levels != null && (!value.Level.HasValue || !_levels.Contains(value.Level.Value)) ||
            _keywordMask.HasValue &&
            (!value.Keywords.HasValue || (value.Keywords.Value & _keywordMask.Value) == 0) ||
            _userIds != null &&
            (value.UserId == null || !_userIds.Contains(value.UserId.Value))) {
            return false;
        }

        if (_data == null && _namedData == null && _excludedNamedData == null) {
            return true;
        }
        IReadOnlyDictionary<string, string> eventData = value.Data;
        if (_data != null && !eventData.Values.Any(_data.Contains)) {
            return false;
        }
        if (_namedData != null && !MatchesNamedData(eventData, _namedData)) {
            return false;
        }
        return _excludedNamedData == null ||
               !MatchesNamedData(eventData, _excludedNamedData);
    }

    private static bool MatchesNamedData(
        IReadOnlyDictionary<string, string> eventData,
        IReadOnlyDictionary<string, HashSet<string>?> expected) {

        foreach (KeyValuePair<string, HashSet<string>?> item in expected) {
            if (!eventData.TryGetValue(item.Key, out string? actual)) {
                return false;
            }
            if (item.Value != null && !item.Value.Contains(actual)) {
                return false;
            }
        }
        return true;
    }

    private void ValidateBounds() {
        if (_eventIds?.Any(static value => value < 0 || value > ushort.MaxValue) == true ||
            _excludedEventIds?.Any(static value => value < 0 || value > ushort.MaxValue) == true) {
            throw new ArgumentOutOfRangeException(
                nameof(EventFilter.EventIds),
                "Event IDs must be between 0 and 65535.");
        }
        if (_recordIds?.Any(static value => value <= 0) == true ||
            _minimumRecordIdExclusive < 0 ||
            _maximumRecordIdExclusive < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(EventFilter.RecordIds),
                "Event record IDs and boundaries must be non-negative, and explicit record IDs must be positive.");
        }
    }

    private static HashSet<T>? ToSet<T>(IReadOnlyList<T>? values) {
        return values == null || values.Count == 0
            ? null
            : new HashSet<T>(values);
    }

    private static HashSet<string>? ToStringSet(
        IReadOnlyList<string>? values) {

        string[] normalized = values?
            .Where(static value => !string.IsNullOrEmpty(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();
        return normalized.Length == 0
            ? null
            : new HashSet<string>(normalized, StringComparer.Ordinal);
    }

    private static long? CombineKeywords(IReadOnlyList<long>? values) {
        if (values == null || values.Count == 0) {
            return null;
        }
        long mask = 0;
        foreach (long value in values) {
            mask |= value;
        }
        return mask;
    }

    private static HashSet<string>? ResolveUserIds(
        IReadOnlyList<string>? values) {

        if (values == null || values.Count == 0) {
            return null;
        }
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string value in values) {
            if (!EventStructuredQueryFilterService.TryResolveUserId(
                    value,
                    out string? sid)) {
                throw new ArgumentException(
                    $"User identifier '{value}' is not a valid SID or resolvable account name.",
                    nameof(values));
            }
            result.Add(sid!);
        }
        return result;
    }

    private static IReadOnlyDictionary<string, HashSet<string>?>? ToNamedData(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? values) {

        if (values == null || values.Count == 0) {
            return null;
        }
        return values.ToDictionary(
            static item => item.Key,
            static item => item.Value.Count == 0
                ? null
                : new HashSet<string>(item.Value, StringComparer.Ordinal),
            StringComparer.OrdinalIgnoreCase);
    }
}
