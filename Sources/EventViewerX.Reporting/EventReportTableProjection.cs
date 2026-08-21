using System.Globalization;

using System.Collections;

namespace EventViewerX.Reporting;

/// <summary>Builds tabular projections from homogeneous report sections.</summary>
internal static class EventReportTableProjection {
    internal const int DefaultExpandedValueColumnLimit = 12;

    private static readonly EventReportColumn[] GenericColumns = {
        new(nameof(EventReportRow.TimeCreated), "Time Created", typeof(DateTime)),
        new(nameof(EventReportRow.EventId), "Event ID", typeof(int)),
        new(nameof(EventReportRow.Level), "Level", typeof(string)),
        new(nameof(EventReportRow.SourceComputer), "Source Computer", typeof(string)),
        new(nameof(EventReportRow.SourceLog), "Source Log", typeof(string)),
        new(nameof(EventReportRow.Provider), "Provider", typeof(string)),
        new(nameof(EventReportRow.RecordId), "Record ID", typeof(long?)),
        new(nameof(EventReportRow.Message), "Message", typeof(string)),
        new(nameof(EventReportRow.ContainerLog), "Container Log", typeof(string)),
        new(nameof(EventReportRow.CollectorComputer), "Collector Computer", typeof(string))
    };

    internal static IReadOnlyList<EventReportColumn> BuildGenericColumns(
        IEnumerable<EventReportRow> source,
        int expandedValueColumnLimit = DefaultExpandedValueColumnLimit) {

        List<EventReportRow> rows = source?.ToList() ?? throw new ArgumentNullException(nameof(source));
        if (expandedValueColumnLimit < 0) {
            throw new ArgumentOutOfRangeException(nameof(expandedValueColumnLimit));
        }
        string[] valueColumns = rows
            .SelectMany(static row => row.Values.Keys)
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .Where(static key => !EventReportRow.IsCommonFieldName(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(expandedValueColumnLimit + 1)
            .ToArray();
        var columns = new List<EventReportColumn>(GenericColumns.Length + valueColumns.Length);
        columns.AddRange(GenericColumns.Take(7));
        if (valueColumns.Length <= expandedValueColumnLimit) {
            foreach (string name in valueColumns) {
                Type valueType = rows.Select(row => row.Values.TryGetValue(name, out object? value) ? value?.GetType() : null)
                    .FirstOrDefault(static type => type != null) ?? typeof(object);
                columns.Add(new EventReportColumn(name, SplitWords(name), valueType));
            }
        } else {
            columns.Add(new EventReportColumn("Details", "Details", typeof(string)));
        }
        columns.AddRange(GenericColumns.Skip(7));
        return columns;
    }

    internal static List<Dictionary<string, object?>> Project(EventReportSection section) {
        if (section == null) {
            throw new ArgumentNullException(nameof(section));
        }
        return section.Rows.Select(row => ProjectRow(section, row)).ToList();
    }

    internal static IReadOnlyDictionary<string, string> CreateUniqueDisplayNames(
        IReadOnlyList<EventReportColumn> columns) {

        if (columns == null) {
            throw new ArgumentNullException(nameof(columns));
        }
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (EventReportColumn column in columns) {
            string baseName = string.IsNullOrWhiteSpace(column.DisplayName)
                ? column.Name
                : column.DisplayName;
            string candidate = baseName;
            if (!used.Add(candidate)) {
                string disambiguator = SplitWords(column.Name);
                candidate = $"{baseName} {disambiguator}";
                int suffix = 2;
                while (!used.Add(candidate)) {
                    candidate = $"{baseName} {disambiguator} {suffix++}";
                }
            }
            result[column.Name] = candidate;
        }
        return result;
    }

    internal static List<Dictionary<string, object?>> ProjectProvenance(IEnumerable<EventReportRow> rows) {
        if (rows == null) {
            throw new ArgumentNullException(nameof(rows));
        }
        return rows.Select(static row => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) {
            ["Time Created"] = row.TimeCreated,
            ["Type"] = row.Type,
            ["Event ID"] = row.EventId,
            ["Level"] = row.Level,
            ["Source Computer"] = row.SourceComputer,
            ["Source Log"] = row.SourceLog,
            ["Provider"] = row.Provider,
            ["Record ID"] = row.RecordId,
            ["Message"] = CollapseWhitespace(row.Message),
            ["Container Log"] = row.ContainerLog,
            ["Collector Computer"] = row.CollectorComputer
        }).ToList();
    }

    internal static string FormatDetails(
        EventReportRow row,
        int maximumValues = int.MaxValue,
        string? separator = null) {
        if (row == null) {
            throw new ArgumentNullException(nameof(row));
        }
        if (maximumValues < 0) {
            throw new ArgumentOutOfRangeException(nameof(maximumValues));
        }
        return string.Join(separator ?? Environment.NewLine, row.Values
            .Where(static item => !string.IsNullOrWhiteSpace(item.Key))
            .Take(maximumValues)
            .Select(static item => $"{SplitWords(item.Key)}: {FormatValue(item.Value)}"));
    }

    private static Dictionary<string, object?> ProjectRow(EventReportSection section, EventReportRow row) {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (EventReportColumn column in section.Columns) {
            if (section.Kind != EventReportSectionKind.Generic) {
                result[column.Name] = row.Values.TryGetValue(column.Name, out object? value)
                    ? NormalizeCellValue(value)
                    : null;
                continue;
            }
            result[column.Name] = column.Name switch {
                nameof(EventReportRow.TimeCreated) => row.TimeCreated,
                nameof(EventReportRow.Type) => row.Type,
                nameof(EventReportRow.EventId) => row.EventId,
                nameof(EventReportRow.RecordId) => row.RecordId,
                nameof(EventReportRow.Provider) => row.Provider,
                nameof(EventReportRow.SourceLog) => row.SourceLog,
                nameof(EventReportRow.ContainerLog) => row.ContainerLog,
                nameof(EventReportRow.SourceComputer) => row.SourceComputer,
                nameof(EventReportRow.CollectorComputer) => row.CollectorComputer,
                nameof(EventReportRow.Level) => row.Level,
                nameof(EventReportRow.Message) => CollapseWhitespace(row.Message),
                "Details" => FormatDetails(row, separator: "; "),
                _ => row.Values.TryGetValue(column.Name, out object? value) ? NormalizeCellValue(value) : null
            };
        }
        return result;
    }

    private static object? NormalizeCellValue(object? value) => value switch {
        null => null,
        string text => IsPlaceholder(text) ? null : text,
        IEnumerable enumerable => FormatEnumerable(enumerable),
        _ => value
    };

    private static bool IsPlaceholder(string value) {
        string trimmed = value.Trim();
        return trimmed.Length == 0 || trimmed.All(static character =>
            character == '-' || character == '\\' || character == '/' || char.IsWhiteSpace(character));
    }

    private static string FormatValue(object? value) => value switch {
        null => string.Empty,
        string text => text,
        DateTime date => date.ToString("u", CultureInfo.InvariantCulture),
        DateTimeOffset date => date.ToString("u", CultureInfo.InvariantCulture),
        IEnumerable enumerable => FormatEnumerable(enumerable),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
        _ => value.ToString() ?? string.Empty
    };

    private static string FormatEnumerable(IEnumerable values) {
        var items = new List<string>();
        foreach (object? item in values) {
            string formatted = item is DictionaryEntry entry
                ? $"{FormatValue(entry.Key)}: {FormatValue(entry.Value)}"
                : FormatValue(item);
            if (!string.IsNullOrWhiteSpace(formatted)) {
                items.Add(formatted);
            }
        }
        return string.Join(", ", items);
    }

    internal static string SplitWords(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }
        var result = new StringBuilder(value.Length + 8);
        for (int index = 0; index < value.Length; index++) {
            char current = value[index];
            if (index > 0 && char.IsUpper(current) &&
                (char.IsLower(value[index - 1]) || index + 1 < value.Length && char.IsLower(value[index + 1]))) {
                result.Append(' ');
            }
            result.Append(current);
        }
        return string.Join(" ", result.ToString().Split(' ').Select(static word => word switch {
            "Dhcp" => "DHCP",
            "Dns" => "DNS",
            "Gpo" => "GPO",
            "Guid" => "GUID",
            "Id" => "ID",
            "Ip" => "IP",
            "Rdp" => "RDP",
            "Sid" => "SID",
            "Smb" => "SMB",
            "Tgt" => "TGT",
            "Url" => "URL",
            "Wec" => "WEC",
            "Xml" => "XML",
            _ => word
        }));
    }

    private static string CollapseWhitespace(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }
        var result = new StringBuilder(value!.Length);
        bool pendingSpace = false;
        foreach (char character in value) {
            if (char.IsWhiteSpace(character)) {
                pendingSpace = result.Length > 0;
            } else {
                if (pendingSpace) {
                    result.Append(' ');
                    pendingSpace = false;
                }
                result.Append(character);
            }
        }
        return result.ToString();
    }
}
