using System.Collections;

namespace EventViewerX;

internal sealed class EventMultiValueNormalizer : IEventValueNormalizer {
    private static readonly HashSet<string> TextCollectionFields = new(
        new[] {
            "ServicePrincipalName", "ServicePrincipalNames", "SPN", "SPNs",
            "AllowedToDelegateTo", "MemberOf", "Members", "Privileges"
        },
        StringComparer.OrdinalIgnoreCase);

    public string Name => "event-multivalue";

    public int Version => 1;

    public bool CanNormalize(EventValueContext context) =>
        context.RawValue is IEnumerable<string> ||
        TextCollectionFields.Contains(context.FieldName);

    public EventNormalizedValue Normalize(EventValueContext context) {
        string[] values = context.RawValue is IEnumerable enumerable && context.RawValue is not string
            ? Enumerate(enumerable)
            : Split(EventValueNormalizer.Format(context.RawValue));
        string[] canonical = values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .GroupBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.OrderBy(static value => value, StringComparer.Ordinal).First())
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        return EventValueNormalizer.Create(
            context,
            canonical,
            string.Join(", ", canonical),
            EventNormalizedValueKind.MultiValue,
            Name,
            Version);
    }

    private static string[] Enumerate(IEnumerable values) {
        var result = new List<string>();
        foreach (object? value in values) {
            result.Add(EventValueNormalizer.Format(value));
        }
        return result.ToArray();
    }

    private static string[] Split(string value) => value.Split(
        new[] { "\r\n", "\n", "\r", ";" },
        StringSplitOptions.RemoveEmptyEntries);
}
