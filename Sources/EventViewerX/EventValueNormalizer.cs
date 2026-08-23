using System.Collections;
using System.Globalization;

namespace EventViewerX;

internal static class EventValueNormalizer {
    internal static EventNormalizedValue Create(
        EventValueContext context,
        object? value,
        string displayValue,
        EventNormalizedValueKind kind,
        string normalizer,
        int version,
        EventNormalizationOutcome outcome = EventNormalizationOutcome.Normalized,
        bool isLossless = true,
        params string[] warnings) => new() {
            RawValue = context.RawValue,
            Value = value,
            DisplayValue = displayValue,
            Kind = kind,
            Normalizer = normalizer,
            NormalizerVersion = version,
            Outcome = outcome,
            IsLossless = isLossless,
            Warnings = warnings.Where(static warning => !string.IsNullOrWhiteSpace(warning)).ToArray()
        };

    internal static EventNormalizedValue Unchanged(EventValueContext context) {
        object? value = context.RawValue;
        return Create(
            context,
            value,
            Format(value),
            ResolveKind(value),
            "identity",
            1,
            EventNormalizationOutcome.Unchanged);
    }

    internal static string Format(object? value) => value switch {
        null => string.Empty,
        DateTime date => date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset date => date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        IEnumerable values when value is not string => string.Join(", ", Enumerate(values)),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
        _ => value.ToString() ?? string.Empty
    };

    private static EventNormalizedValueKind ResolveKind(object? value) => value switch {
        DateTime or DateTimeOffset => EventNormalizedValueKind.DateTime,
        Guid => EventNormalizedValueKind.Guid,
        IEnumerable when value is not string => EventNormalizedValueKind.MultiValue,
        string => EventNormalizedValueKind.Text,
        _ => EventNormalizedValueKind.Unknown
    };

    private static IEnumerable<string> Enumerate(IEnumerable source) {
        foreach (object? item in source) {
            yield return Format(item);
        }
    }
}
