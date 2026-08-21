namespace EventViewerX;

/// <summary>
/// Builds Windows Event Log XPath and QueryList filters from strongly validated values.
/// </summary>
public static partial class WindowsEventFilterBuilder {
    internal static string EscapeXmlValue(string value) {
        return System.Security.SecurityElement.Escape(value);
    }

    internal static string FormatXPathStringLiteral(string value, string parameterName) {
        if (value.IndexOf('\'') < 0) {
            return $"'{value}'";
        }
        if (value.IndexOf('"') < 0) {
            return $"\"{value}\"";
        }

        throw new ArgumentException(
            "XPath string values containing both single and double quote characters are not supported by the Windows Event Log XPath subset.",
            parameterName);
    }

    internal static string FormatXmlEncodedXPathStringLiteral(string value, string parameterName) {
        return EscapeXmlValue(FormatXPathStringLiteral(value, parameterName));
    }
    private static string JoinXPathFilter(string newFilter, string existingFilter = "", string logic = "and", bool noParenthesis = false) {
        if (!string.IsNullOrEmpty(existingFilter)) {
            return noParenthesis
                ? $"{existingFilter} {logic} {newFilter}"
                : $"({existingFilter}) {logic} ({newFilter})";
        }
        return newFilter;
    }

    private static string InitializeXPathFilter(IEnumerable<object?> items, string forEachFormatString, string finalizeFormatString, string logic = "or", bool noParenthesis = false, bool formatStringLiterals = false, string parameterName = "value") {
        var filter = string.Empty;
        foreach (var item in items) {
            if (item == null) {
                continue;
            }
            string rawValue = item.ToString()!;
            var value = formatStringLiterals
                ? FormatXPathStringLiteral(rawValue, parameterName)
                : rawValue;
            var formatted = forEachFormatString.Replace("{0}", $"{value}");
            filter = JoinXPathFilter(formatted, filter, logic, noParenthesis);
        }
        return finalizeFormatString.Replace("{0}", $"{filter}");
    }

    private static IEnumerable<string> AsEnumerable(object? obj) {
        if (obj is IEnumerable enumerable and not string) {
            foreach (var o in enumerable) {
                if (o != null) {
                    yield return o.ToString()!;
                }
            }
        } else if (obj != null) {
            yield return obj.ToString()!;
        }
    }

    /// <summary>
    /// Cache for translated user identifiers to avoid repeated lookups
    /// </summary>
    private static readonly ConcurrentDictionary<string, string> userSidCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static string FormatEventTimeUtc(DateTime value) {
        return value
            .ToUniversalTime()
            .ToString(
                "o",
                System.Globalization.CultureInfo.InvariantCulture);
    }
}
