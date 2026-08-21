using System.Globalization;

namespace EventViewerX.Reports.Correlation;

internal static partial class NamedEventsTimelineQueryExecutor {
    internal static bool TryParseUtcValue(string? value, out DateTime utc) {
        utc = default;
        var text = value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) {
            return false;
        }

        text = text.Trim();
        var hasExplicitOffset = HasExplicitOffsetOrUtcDesignator(text);

        if (hasExplicitOffset) {
            if (!DateTimeOffset.TryParse(
                    text,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                    out var parsedOffset)) {
                return false;
            }

            utc = parsedOffset.UtcDateTime;
            return true;
        }

        if (!DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var parsedDateTime)) {
            return false;
        }

        utc = parsedDateTime.Kind switch {
            DateTimeKind.Utc => parsedDateTime,
            DateTimeKind.Local => parsedDateTime.ToUniversalTime(),
            _ => DateTime.SpecifyKind(parsedDateTime, DateTimeKind.Utc)
        };
        return true;
    }

    private static bool HasExplicitOffsetOrUtcDesignator(string value) {
        if (value.EndsWith("Z", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        var searchStart = 0;
        var tIndex = value.IndexOf('T');
        if (tIndex >= 0 && tIndex + 1 < value.Length) {
            searchStart = tIndex + 1;
        } else {
            var spaceIndex = value.IndexOf(' ');
            if (spaceIndex >= 0 && spaceIndex + 1 < value.Length) {
                searchStart = spaceIndex + 1;
            }
        }

        for (var i = value.Length - 1; i >= searchStart; i--) {
            var ch = value[i];
            if (ch == '+' || ch == '-') {
                return true;
            }
        }

        return false;
    }

    private static DateTime? ParseUtc(string? value) {
        return TryParseUtcValue(value, out var utc) ? utc : null;
    }

    private static DateTime FloorToBucket(DateTime valueUtc, int bucketMinutes) {
        var utc = valueUtc.Kind == DateTimeKind.Utc ? valueUtc : valueUtc.ToUniversalTime();
        var bucketTicks = TimeSpan.FromMinutes(bucketMinutes).Ticks;
        if (bucketTicks <= 0) {
            return utc;
        }

        var flooredTicks = utc.Ticks - (utc.Ticks % bucketTicks);
        return new DateTime(flooredTicks, DateTimeKind.Utc);
    }
}
