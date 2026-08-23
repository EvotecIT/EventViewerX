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
                date.Kind == DateTimeKind.Utc
                    ? EventNormalizationOutcome.Unchanged
                    : EventNormalizationOutcome.Normalized);
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
        if (fileTime == 0 && IsPasswordLastSet(context)) {
            return EventValueNormalizer.Create(
                context,
                "PasswordChangeRequired",
                "Must change password at next logon",
                EventNormalizedValueKind.DirectorySentinel,
                Name,
                Version);
        }
        if (fileTime == 0 || fileTime == long.MaxValue) {
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

    private static bool IsPasswordLastSet(EventValueContext context) {
        if (string.Equals(context.FieldName, "PasswordLastSet", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(context.FieldName, "PwdLastSet", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }
        if (!string.Equals(context.FieldName, "AttributeValue", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }
        return TrySibling(context.Values, "AttributeName", out string attribute) ||
               TrySibling(context.Values, "AttributeLDAPDisplayName", out attribute)
            ? string.Equals(attribute, "pwdLastSet", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(attribute, "PasswordLastSet", StringComparison.OrdinalIgnoreCase)
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
