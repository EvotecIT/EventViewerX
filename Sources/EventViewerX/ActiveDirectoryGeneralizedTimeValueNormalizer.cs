using System.Globalization;
using System.Text.RegularExpressions;

namespace EventViewerX;

internal sealed class ActiveDirectoryGeneralizedTimeValueNormalizer : IEventValueNormalizer {
    private static readonly HashSet<string> FieldNames = new(
        new[] { "WhenChanged", "WhenCreated" },
        StringComparer.OrdinalIgnoreCase);
    private static readonly Regex GeneralizedTime = new(
        @"^(?<date>\d{14})(?<fraction>[\.,]\d+)?(?<zone>Z|[+-]\d{4})$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public string Name => "active-directory-generalized-time";

    public int Version => 1;

    public bool CanNormalize(EventValueContext context) =>
        FieldNames.Contains(context.FieldName) || IsGeneralizedTimeAttributeValue(context);

    public EventNormalizedValue Normalize(EventValueContext context) {
        if (context.RawValue is DateTime date) {
            DateTime utc = date.Kind switch {
                DateTimeKind.Utc => date,
                DateTimeKind.Local => date.ToUniversalTime(),
                _ => DateTime.SpecifyKind(date, DateTimeKind.Utc)
            };
            return Create(
                context,
                utc,
                date.Kind == DateTimeKind.Utc
                    ? EventNormalizationOutcome.Unchanged
                    : EventNormalizationOutcome.Normalized);
        }
        if (context.RawValue is DateTimeOffset offset) {
            return Create(context, offset.UtcDateTime, EventNormalizationOutcome.Unchanged);
        }
        string raw = EventValueNormalizer.Format(context.RawValue).Trim();
        Match match = GeneralizedTime.Match(raw);
        if (!match.Success || !DateTime.TryParseExact(
                match.Groups["date"].Value,
                "yyyyMMddHHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime local)) {
            return Malformed(context, raw);
        }
        long fractionTicks = 0;
        string fraction = match.Groups["fraction"].Value.TrimStart('.', ',');
        bool isLossless = true;
        if (fraction.Length > 0) {
            string ticks = (fraction + "0000000").Substring(0, 7);
            if (!int.TryParse(ticks, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedTicks)) {
                return Malformed(context, raw);
            }
            fractionTicks = parsedTicks;
            isLossless = fraction.Length <= 7 || fraction.Substring(7).All(static digit => digit == '0');
        }
        local = DateTime.SpecifyKind(local.AddTicks(fractionTicks), DateTimeKind.Unspecified);
        string zone = match.Groups["zone"].Value;
        TimeSpan zoneOffset = TimeSpan.Zero;
        if (zone != "Z") {
            int offsetHours = int.Parse(zone.Substring(1, 2), CultureInfo.InvariantCulture);
            int offsetMinutes = int.Parse(zone.Substring(3, 2), CultureInfo.InvariantCulture);
            if (offsetHours > 14 || offsetMinutes > 59 || (offsetHours == 14 && offsetMinutes != 0)) {
                return Malformed(context, raw);
            }
            zoneOffset = TimeSpan.FromMinutes(
                (offsetHours * 60 + offsetMinutes) * (zone[0] == '-' ? -1 : 1));
        }
        try {
            return Create(
                context,
                new DateTimeOffset(local, zoneOffset).UtcDateTime,
                isLossless: isLossless,
                warnings: isLossless
                    ? Array.Empty<string>()
                    : new[] { "Fractional precision beyond 100 nanoseconds was truncated." });
        } catch (ArgumentException) {
            return Malformed(context, raw);
        }
    }

    private EventNormalizedValue Create(
        EventValueContext context,
        DateTime utc,
        EventNormalizationOutcome outcome = EventNormalizationOutcome.Normalized,
        bool isLossless = true,
        params string[] warnings) =>
        EventValueNormalizer.Create(
            context,
            utc,
            utc.ToString("O", CultureInfo.InvariantCulture),
            EventNormalizedValueKind.DateTime,
            Name,
            Version,
            outcome,
            isLossless,
            warnings);

    private EventNormalizedValue Malformed(EventValueContext context, string raw) =>
        EventValueNormalizer.Create(
            context,
            context.RawValue,
            raw,
            EventNormalizedValueKind.DateTime,
            Name,
            Version,
            EventNormalizationOutcome.Malformed,
            warnings: new[] { $"Generalized-time value '{raw}' is not a valid Active Directory timestamp." });

    private static bool IsGeneralizedTimeAttributeValue(EventValueContext context) {
        if (!string.Equals(context.FieldName, "AttributeValue", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }
        return TrySibling(context.Values, "AttributeName", out string attribute) ||
               TrySibling(context.Values, "AttributeLDAPDisplayName", out attribute)
            ? FieldNames.Contains(attribute)
            : false;
    }

    private static bool TrySibling(
        IReadOnlyDictionary<string, object?> values,
        string name,
        out string value) {

        value = values.TryGetValue(name, out object? raw)
            ? EventValueNormalizer.Format(raw).Trim()
            : string.Empty;
        return value.Length > 0;
    }
}
