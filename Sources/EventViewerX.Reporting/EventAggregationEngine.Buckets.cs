using System.Globalization;
using System.Text;

namespace EventViewerX.Reporting;

public static partial class EventAggregationEngine {
    internal static bool TryCreateGroup(
        EventAggregationDefinition definition,
        IReadOnlyDictionary<string, object?> values,
        out AggregationGroup group) {

        var dimensions = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var identity = new StringBuilder();
        foreach (string field in definition.GroupBy) {
            values.TryGetValue(field, out object? value);
            if (value == null && definition.GroupNulls == EventAggregationNullPolicy.Exclude) {
                group = AggregationGroup.Empty;
                return false;
            }
            dimensions[field] = value;
            WriteCanonical(identity, value);
        }
        group = new AggregationGroup(identity.ToString(), dimensions);
        return true;
    }

    internal static AggregationBucketRange CreateBucket(
        EventAggregationDefinition definition,
        DateTime timestamp) {

        if (definition.Bucket == EventAggregationBucket.None) {
            return definition.WindowStart.HasValue
                ? new AggregationBucketRange(
                    definition.WindowStart.Value,
                    definition.WindowEnd!.Value,
                    definition.WindowStart.Value.ToString("O", CultureInfo.InvariantCulture) + "/" +
                    definition.WindowEnd.Value.ToString("O", CultureInfo.InvariantCulture),
                    null)
                : AggregationBucketRange.None;
        }
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(definition.TimeZoneId);
        DateTime utc = timestamp.ToUniversalTime();
        DateTimeOffset local = TimeZoneInfo.ConvertTime(new DateTimeOffset(utc), zone);
        DateTime localStart = definition.Bucket switch {
            EventAggregationBucket.Hour => new DateTime(local.Year, local.Month, local.Day, local.Hour, 0, 0, DateTimeKind.Unspecified),
            EventAggregationBucket.Day => new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Unspecified),
            EventAggregationBucket.Week => new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Unspecified)
                .AddDays(-(((int)local.DayOfWeek + 6) % 7)),
            EventAggregationBucket.Month => new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Unspecified),
            _ => throw new ArgumentOutOfRangeException(nameof(definition.Bucket))
        };
        DateTime localEnd = definition.Bucket switch {
            EventAggregationBucket.Hour => localStart.AddHours(1),
            EventAggregationBucket.Day => localStart.AddDays(1),
            EventAggregationBucket.Week => localStart.AddDays(7),
            EventAggregationBucket.Month => localStart.AddMonths(1),
            _ => throw new ArgumentOutOfRangeException(nameof(definition.Bucket))
        };
        DateTime startUtc = ResolveBoundary(zone, localStart, local.Offset, preferMatchingOffset: definition.Bucket == EventAggregationBucket.Hour);
        DateTime endUtc = definition.Bucket == EventAggregationBucket.Hour
            ? startUtc.AddHours(1)
            : ResolveBoundary(zone, localEnd, local.Offset, preferMatchingOffset: false);
        string label = localStart.ToString(
            definition.Bucket == EventAggregationBucket.Hour ? "yyyy-MM-dd HH:00" : "yyyy-MM-dd",
            CultureInfo.InvariantCulture) + " " + FormatOffset(zone.GetUtcOffset(startUtc));
        string identity = startUtc.Ticks.ToString("D19", CultureInfo.InvariantCulture) + "/" +
                          endUtc.Ticks.ToString("D19", CultureInfo.InvariantCulture);
        return new AggregationBucketRange(startUtc, endUtc, identity, label);
    }

    private static DateTime ResolveBoundary(
        TimeZoneInfo zone,
        DateTime local,
        TimeSpan preferredOffset,
        bool preferMatchingOffset) {

        DateTime candidate = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(candidate)) {
            candidate = candidate.AddMinutes(1);
        }
        TimeSpan offset;
        if (zone.IsAmbiguousTime(candidate)) {
            TimeSpan[] offsets = zone.GetAmbiguousTimeOffsets(candidate)
                .OrderByDescending(static value => value)
                .ToArray();
            offset = preferMatchingOffset && offsets.Contains(preferredOffset)
                ? preferredOffset
                : offsets[0];
        } else {
            offset = zone.GetUtcOffset(candidate);
        }
        return new DateTimeOffset(candidate, offset).UtcDateTime;
    }

    private static string FormatOffset(TimeSpan offset) =>
        (offset < TimeSpan.Zero ? "-" : "+") + offset.Duration().ToString(@"hh\:mm", CultureInfo.InvariantCulture);

    internal static string Canonicalize(object? value) {
        var result = new StringBuilder();
        WriteCanonical(result, value);
        return result.ToString();
    }

    private static void WriteCanonical(StringBuilder builder, object? value) {
        string type;
        string text;
        switch (value) {
            case null:
                type = "null";
                text = string.Empty;
                break;
            case DateTime date:
                type = "datetime";
                text = date.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture);
                break;
            case DateTimeOffset date:
                type = "datetime";
                text = date.UtcTicks.ToString(CultureInfo.InvariantCulture);
                break;
            case string valueText:
                type = "text";
                text = valueText.Normalize(NormalizationForm.FormC).ToUpperInvariant();
                break;
            case System.Collections.IEnumerable enumerable when value is not string:
                type = "collection";
                text = string.Join("\u001f", EnumerateCanonical(enumerable));
                break;
            case IFormattable formattable:
                type = value.GetType().FullName ?? value.GetType().Name;
                text = formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;
                break;
            default:
                type = value.GetType().FullName ?? value.GetType().Name;
                text = value.ToString() ?? string.Empty;
                break;
        }
        builder.Append(type.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(type)
            .Append(text.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(text).Append('|');
    }

    private static IEnumerable<string> EnumerateCanonical(System.Collections.IEnumerable values) {
        foreach (object? value in values) {
            yield return Canonicalize(value);
        }
    }
}

internal sealed class AggregationGroup {
    internal static readonly AggregationGroup Empty = new(string.Empty, new Dictionary<string, object?>());

    internal AggregationGroup(string identity, IReadOnlyDictionary<string, object?> dimensions) {
        Identity = identity;
        Dimensions = dimensions;
    }

    internal string Identity { get; }
    internal IReadOnlyDictionary<string, object?> Dimensions { get; private set; }

    internal void MergeDisplay(AggregationGroup candidate) {
        var merged = Dimensions.ToDictionary(
            static item => item.Key,
            static item => item.Value,
            StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, object?> item in candidate.Dimensions) {
            if (!merged.TryGetValue(item.Key, out object? current) ||
                CompareDisplay(item.Value, current) < 0) {
                merged[item.Key] = item.Value;
            }
        }
        Dimensions = merged;
    }

    private static int CompareDisplay(object? left, object? right) => string.CompareOrdinal(
        FormatDisplayIdentity(left),
        FormatDisplayIdentity(right));

    private static string FormatDisplayIdentity(object? value) => value switch {
        null => string.Empty,
        string text => text,
        DateTime date => date.ToUniversalTime().Ticks.ToString("D19", CultureInfo.InvariantCulture),
        DateTimeOffset date => date.UtcTicks.ToString("D19", CultureInfo.InvariantCulture),
        System.Collections.IEnumerable enumerable when value is not string =>
            "[" + string.Join("\u001f", EnumerateDisplay(enumerable)) + "]",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
        _ => value.ToString() ?? string.Empty
    };

    private static IEnumerable<string> EnumerateDisplay(System.Collections.IEnumerable values) {
        foreach (object? value in values) {
            yield return FormatDisplayIdentity(value);
        }
    }
}

internal sealed class AggregationBucketRange {
    internal static readonly AggregationBucketRange None = new(null, null, string.Empty, null);

    internal AggregationBucketRange(DateTime? startUtc, DateTime? endUtc, string identity, string? label) {
        StartUtc = startUtc;
        EndUtc = endUtc;
        Identity = identity;
        Label = label;
    }

    internal DateTime? StartUtc { get; }
    internal DateTime? EndUtc { get; }
    internal string Identity { get; }
    internal string? Label { get; }
}
