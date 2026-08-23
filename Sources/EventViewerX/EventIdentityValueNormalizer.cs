using System.Text;
using System.Text.RegularExpressions;

namespace EventViewerX;

internal sealed class EventIdentityValueNormalizer : IEventValueNormalizer {
    private static readonly Regex SidPattern = new(
        @"^S-\d(?:-\d+)+$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex OidPattern = new(
        @"^\d+(?:\.\d+)+$",
        RegexOptions.CultureInvariant);

    public string Name => "event-identity";

    public int Version => 1;

    public bool CanNormalize(EventValueContext context) {
        string field = context.FieldName;
        return EndsWith(field, "Sid") || EndsWith(field, "Guid") || EndsWith(field, "Id") && IsGuid(context.RawValue) ||
               EndsWith(field, "DistinguishedName") || EndsWith(field, "ObjectDN") ||
               EndsWith(field, "OID") || EndsWith(field, "ObjectIdentifier");
    }

    public EventNormalizedValue Normalize(EventValueContext context) {
        string raw = EventValueNormalizer.Format(context.RawValue).Trim();
        string field = context.FieldName;
        if (EndsWith(field, "Sid")) {
            string canonical = raw.ToUpperInvariant();
            return SidPattern.IsMatch(canonical)
                ? EventValueNormalizer.Create(
                    context,
                    canonical,
                    canonical,
                    EventNormalizedValueKind.SecurityIdentifier,
                    Name,
                    Version)
                : Malformed(context, raw, EventNormalizedValueKind.SecurityIdentifier, "SID");
        }
        if (EndsWith(field, "Guid") || EndsWith(field, "Id") && IsGuid(context.RawValue)) {
            return Guid.TryParse(raw.Trim('{', '}'), out Guid guid)
                ? EventValueNormalizer.Create(
                    context,
                    guid,
                    guid.ToString("D"),
                    EventNormalizedValueKind.Guid,
                    Name,
                    Version)
                : Malformed(context, raw, EventNormalizedValueKind.Guid, "GUID");
        }
        if (EndsWith(field, "OID") || EndsWith(field, "ObjectIdentifier")) {
            return OidPattern.IsMatch(raw)
                ? EventValueNormalizer.Create(
                    context,
                    raw,
                    raw,
                    EventNormalizedValueKind.ObjectIdentifier,
                    Name,
                    Version,
                    EventNormalizationOutcome.Unchanged)
                : Malformed(context, raw, EventNormalizedValueKind.ObjectIdentifier, "OID");
        }
        string distinguishedName = NormalizeDistinguishedName(raw);
        return EventValueNormalizer.Create(
            context,
            distinguishedName,
            distinguishedName,
            EventNormalizedValueKind.DistinguishedName,
            Name,
            Version,
            string.Equals(raw, distinguishedName, StringComparison.Ordinal)
                ? EventNormalizationOutcome.Unchanged
                : EventNormalizationOutcome.Normalized);
    }

    private EventNormalizedValue Malformed(
        EventValueContext context,
        string raw,
        EventNormalizedValueKind kind,
        string label) => EventValueNormalizer.Create(
            context,
            context.RawValue,
            raw,
            kind,
            Name,
            Version,
            EventNormalizationOutcome.Malformed,
            warnings: new[] { $"{label} value '{raw}' is malformed." });

    private static bool EndsWith(string value, string suffix) =>
        value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);

    private static bool IsGuid(object? value) =>
        value is Guid || Guid.TryParse(EventValueNormalizer.Format(value).Trim('{', '}'), out _);

    private static string NormalizeDistinguishedName(string value) {
        var result = new StringBuilder(value.Length);
        bool escaped = false;
        bool pendingWhitespace = false;
        foreach (char character in value) {
            if (escaped) {
                if (pendingWhitespace) {
                    result.Append(' ');
                    pendingWhitespace = false;
                }
                result.Append(character);
                escaped = false;
                continue;
            }
            if (character == '\\') {
                if (pendingWhitespace) {
                    result.Append(' ');
                    pendingWhitespace = false;
                }
                result.Append(character);
                escaped = true;
                continue;
            }
            if (character == ',') {
                while (result.Length > 0 && result[result.Length - 1] == ' ') {
                    result.Length--;
                }
                result.Append(',');
                pendingWhitespace = false;
                continue;
            }
            if (char.IsWhiteSpace(character)) {
                pendingWhitespace = result.Length > 0 && result[result.Length - 1] != ',';
                continue;
            }
            if (pendingWhitespace) {
                result.Append(' ');
                pendingWhitespace = false;
            }
            result.Append(character);
        }
        return result.ToString().Trim();
    }
}
