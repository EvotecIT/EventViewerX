namespace EventViewerX;

/// <summary>Composes independently planned native filters using intersection semantics.</summary>
internal static class EventFilterIntersection {
    internal static bool TryCreate(
        EventFilter primary,
        EventFilter? additional,
        out EventFilter result) {

        if (primary == null) {
            throw new ArgumentNullException(nameof(primary));
        }
        result = primary.Clone();
        if (additional == null || !additional.HasAny) {
            return true;
        }
        if (!TryIntersect(primary.EventIds, additional.EventIds, out IReadOnlyList<int>? eventIds) ||
            !TryIntersect(primary.RecordIds, additional.RecordIds, out IReadOnlyList<long>? recordIds) ||
            !TryIntersect(primary.ProviderNames, additional.ProviderNames, out IReadOnlyList<string>? providers,
                StringComparer.OrdinalIgnoreCase) ||
            !TryIntersect(primary.Levels, additional.Levels, out IReadOnlyList<byte>? levels) ||
            !TryIntersect(primary.Keywords, additional.Keywords, out IReadOnlyList<long>? keywords)) {
            return false;
        }
        result.EventIds = eventIds;
        result.RecordIds = recordIds;
        result.ProviderNames = providers;
        result.Levels = levels;
        result.Keywords = keywords;
        result.StartTime = Latest(primary.StartTime, additional.StartTime);
        result.EndTime = Earliest(primary.EndTime, additional.EndTime);
        if (result.StartTime.HasValue && result.EndTime.HasValue && result.StartTime > result.EndTime) {
            return false;
        }
        return true;
    }

    private static bool TryIntersect<T>(
        IReadOnlyList<T>? left,
        IReadOnlyList<T>? right,
        out IReadOnlyList<T>? result,
        IEqualityComparer<T>? comparer = null) {

        comparer ??= EqualityComparer<T>.Default;
        bool hasLeft = left != null && left.Count > 0;
        bool hasRight = right != null && right.Count > 0;
        if (!hasLeft) {
            result = hasRight ? right!.Distinct(comparer).ToArray() : null;
            return true;
        }
        if (!hasRight) {
            result = left!.Distinct(comparer).ToArray();
            return true;
        }
        T[] intersection = left!.Intersect(right!, comparer).ToArray();
        result = intersection;
        return intersection.Length > 0;
    }

    private static DateTime? Latest(DateTime? left, DateTime? right) {
        if (!left.HasValue) {
            return ToUtc(right);
        }
        DateTime leftUtc = left.Value.ToUniversalTime();
        DateTime? rightUtc = ToUtc(right);
        return !rightUtc.HasValue || leftUtc >= rightUtc.Value ? leftUtc : rightUtc;
    }

    private static DateTime? Earliest(DateTime? left, DateTime? right) {
        if (!left.HasValue) {
            return ToUtc(right);
        }
        DateTime leftUtc = left.Value.ToUniversalTime();
        DateTime? rightUtc = ToUtc(right);
        return !rightUtc.HasValue || leftUtc <= rightUtc.Value ? leftUtc : rightUtc;
    }

    private static DateTime? ToUtc(DateTime? value) => value?.ToUniversalTime();
}
