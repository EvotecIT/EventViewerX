using System.Globalization;

namespace EventViewerX;

internal sealed class ActiveDirectoryFileTimeValueNormalizer : IEventValueNormalizer {
    private static readonly HashSet<string> FieldNames = new(
        new[] {
            "AccountExpires", "BadPasswordTime", "LastLogon", "LastLogonTimestamp",
            "LockoutTime", "PasswordLastSet", "PwdLastSet"
        },
        StringComparer.OrdinalIgnoreCase);

    public string Name => "active-directory-filetime";

    public int Version => 1;

    public bool CanNormalize(EventValueContext context) =>
        FieldNames.Contains(context.FieldName) ||
        IsFileTimeAttributeValue(context);

    public EventNormalizedValue Normalize(EventValueContext context) {
        if (context.RawValue is DateTime date) {
            DateTime utc = date.ToUniversalTime();
            return EventValueNormalizer.Create(
                context,
                utc,
                utc.ToString("O", CultureInfo.InvariantCulture),
                EventNormalizedValueKind.DateTime,
                Name,
                Version,
                EventNormalizationOutcome.Unchanged);
        }
        string raw = EventValueNormalizer.Format(context.RawValue).Trim();
        if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long fileTime)) {
            return EventValueNormalizer.Create(
                context,
                context.RawValue,
                raw,
                EventNormalizedValueKind.DateTime,
                Name,
                Version,
                EventNormalizationOutcome.Malformed,
                warnings: new[] { $"FILETIME value '{raw}' is not a signed 64-bit integer." });
        }
        if (fileTime == 0 || fileTime == long.MaxValue || fileTime == 0x7FFFFFFFFFFFFFFF) {
            return EventValueNormalizer.Create(
                context,
                null,
                "Never",
                EventNormalizedValueKind.DateTime,
                Name,
                Version);
        }
        try {
            DateTime utc = DateTime.FromFileTimeUtc(fileTime);
            return EventValueNormalizer.Create(
                context,
                utc,
                utc.ToString("O", CultureInfo.InvariantCulture),
                EventNormalizedValueKind.DateTime,
                Name,
                Version);
        } catch (ArgumentOutOfRangeException) {
            return EventValueNormalizer.Create(
                context,
                context.RawValue,
                raw,
                EventNormalizedValueKind.DateTime,
                Name,
                Version,
                EventNormalizationOutcome.Malformed,
                warnings: new[] { $"FILETIME value '{raw}' is outside the supported UTC range." });
        }
    }

    private static bool IsFileTimeAttributeValue(EventValueContext context) {
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
