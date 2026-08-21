using System.Globalization;

namespace EventViewerX;

/// <summary>
/// Converts typed event-filter values to culture-independent text shared by
/// query compilation and checkpoint identity generation.
/// </summary>
internal static class EventFilterValueConverter {
    /// <summary>
    /// Converts a scalar filter value using the stable representation used by
    /// both the native query and its checkpoint identity.
    /// </summary>
    internal static string ToInvariantString(object? value) {
        if (value == null) {
            return string.Empty;
        }
        if (value is DateTime dateTime) {
            return dateTime
                .ToUniversalTime()
                .ToString(
                    "O",
                    CultureInfo.InvariantCulture);
        }
        if (value is DateTimeOffset dateTimeOffset) {
            return dateTimeOffset
                .ToUniversalTime()
                .ToString(
                    "O",
                    CultureInfo.InvariantCulture);
        }
        if (value is IFormattable formattable) {
            return formattable.ToString(
                       null,
                       CultureInfo.InvariantCulture) ??
                   string.Empty;
        }
        return Convert.ToString(
                   value,
                   CultureInfo.InvariantCulture) ??
               string.Empty;
    }
}
