using System.Collections;
using System.Globalization;

namespace EventViewerX.Reporting;

/// <summary>Builds one semantic presentation model shared by interactive report renderers.</summary>
internal static class EventReportPresentationProjection {
    private const int MaximumPrimaryColumns = 5;

    private static readonly string[] TypedPriorities = {
        "Object Affected", "Target", "Account Name", "User", "Privileges", "Privileges Translated",
        "Failure Reason", "Attribute", "Attribute Value", "Service Name", "Action", "Change",
        "Status", "Result", "Outcome", "Who", "When", "Time Created", "IP Address", "Computer"
    };

    private static readonly string[] GenericPriorities = {
        "Time Created", "Event ID", "Level", "Source Computer", "Message", "Source Log"
    };

    internal static EventReportPresentationSection Create(EventReportSection section) {
        if (section == null) {
            throw new ArgumentNullException(nameof(section));
        }

        List<Dictionary<string, object?>> projected = EventReportTableProjection.Project(section);
        string[] priorities = section.Kind == EventReportSectionKind.Generic
            ? GenericPriorities
            : TypedPriorities;
        IReadOnlyDictionary<string, string> displayNames =
            EventReportTableProjection.CreateUniqueDisplayNames(section.Columns);
        var candidates = section.Columns
            .Select((column, index) => CreateColumn(
                column,
                displayNames[column.Name],
                projected,
                priorities,
                index))
            .Where(static column => column.HasValues)
            .ToList();
        HashSet<string> primaryNames = candidates
            .OrderBy(static column => column.Priority)
            .ThenBy(static column => column.IsConstant)
            .ThenBy(static column => column.SourceIndex)
            .Take(MaximumPrimaryColumns)
            .Select(static column => column.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<EventReportPresentationColumn> columns = candidates
            .OrderBy(column => primaryNames.Contains(column.Name) ? 0 : 1)
            .ThenBy(static column => column.Priority)
            .ThenBy(static column => column.IsConstant)
            .ThenBy(static column => column.SourceIndex)
            .Select(column => new EventReportPresentationColumn(
                column.Name,
                column.DisplayName,
                primaryNames.Contains(column.Name),
                column.IsConstant))
            .ToList();

        return new EventReportPresentationSection(section, columns, projected);
    }

    internal static string FormatValue(object? value) => value switch {
        null => string.Empty,
        string text => IsPlaceholder(text) ? string.Empty : CollapseWhitespace(text),
        DateTime date => date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        DateTimeOffset date => date.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
        IEnumerable enumerable => FormatEnumerable(enumerable),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
        _ => value.ToString() ?? string.Empty
    };

    internal static string BuildTitle(
        EventReportPresentationSection section,
        IReadOnlyDictionary<string, object?> row,
        EventReportRow source) {

        string[] preferred = section.Section.Kind == EventReportSectionKind.Generic
            ? new[] { "Message", "Source Computer", "Provider" }
            : new[] { "Object Affected", "Account Name", "User", "Who", "Action", "Service Name", "Computer" };
        foreach (string name in preferred) {
            EventReportPresentationColumn? column = section.Columns.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.DisplayName, name, StringComparison.OrdinalIgnoreCase));
            if (column != null && TryFormattedValue(row, column.Name, out string value)) {
                return Truncate(value, 96);
            }
        }

        return $"{section.Section.DisplayName} · {source.TimeCreated:yyyy-MM-dd HH:mm:ss}";
    }

    internal static string BuildSummary(
        EventReportPresentationSection section,
        IReadOnlyDictionary<string, object?> row) {

        return string.Join(" · ", section.PrimaryColumns
            .Select(column => TryFormattedValue(row, column.Name, out string value)
                ? value
                : string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Take(3));
    }

    private static PresentationCandidate CreateColumn(
        EventReportColumn column,
        string displayName,
        IReadOnlyList<Dictionary<string, object?>> rows,
        IReadOnlyList<string> priorities,
        int sourceIndex) {

        string[] values = rows
            .Select(row => row.TryGetValue(column.Name, out object? value)
                ? FormatValue(value)
                : string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        int priority = IndexOf(priorities, column.DisplayName);
        if (priority < 0) {
            priority = priorities.Count + sourceIndex;
        }

        return new PresentationCandidate(
            column.Name,
            displayName,
            sourceIndex,
            priority,
            values.Length > 0,
            values.Length > 0 && values.Distinct(StringComparer.OrdinalIgnoreCase).Take(2).Count() == 1);
    }

    private static bool TryFormattedValue(
        IReadOnlyDictionary<string, object?> row,
        string name,
        out string value) {

        if (row.TryGetValue(name, out object? raw)) {
            value = FormatValue(raw);
            return !string.IsNullOrWhiteSpace(value);
        }

        value = string.Empty;
        return false;
    }

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

    private static bool IsPlaceholder(string value) {
        string trimmed = value.Trim();
        return trimmed.Length == 0 || trimmed.All(static character =>
            character == '-' || character == '\\' || character == '/' || char.IsWhiteSpace(character));
    }

    private static string CollapseWhitespace(string value) =>
        string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value.Substring(0, length - 1) + "…";

    private static int IndexOf(IReadOnlyList<string> source, string value) {
        for (int index = 0; index < source.Count; index++) {
            if (string.Equals(source[index], value, StringComparison.OrdinalIgnoreCase)) {
                return index;
            }
        }
        return -1;
    }

    private sealed class PresentationCandidate {
        internal PresentationCandidate(
            string name,
            string displayName,
            int sourceIndex,
            int priority,
            bool hasValues,
            bool isConstant) {

            Name = name;
            DisplayName = displayName;
            SourceIndex = sourceIndex;
            Priority = priority;
            HasValues = hasValues;
            IsConstant = isConstant;
        }

        internal string Name { get; }
        internal string DisplayName { get; }
        internal int SourceIndex { get; }
        internal int Priority { get; }
        internal bool HasValues { get; }
        internal bool IsConstant { get; }
    }
}

internal sealed class EventReportPresentationSection {
    internal EventReportPresentationSection(
        EventReportSection section,
        IReadOnlyList<EventReportPresentationColumn> columns,
        IReadOnlyList<Dictionary<string, object?>> rows) {

        Section = section;
        Columns = columns;
        Rows = rows;
    }

    internal EventReportSection Section { get; }
    internal IReadOnlyList<EventReportPresentationColumn> Columns { get; }
    internal IReadOnlyList<EventReportPresentationColumn> PrimaryColumns =>
        Columns.Where(static column => column.IsPrimary).ToArray();
    internal IReadOnlyList<Dictionary<string, object?>> Rows { get; }
}

internal sealed class EventReportPresentationColumn {
    internal EventReportPresentationColumn(string name, string displayName, bool isPrimary, bool isConstant) {
        Name = name;
        DisplayName = displayName;
        IsPrimary = isPrimary;
        IsConstant = isConstant;
    }

    internal string Name { get; }
    internal string DisplayName { get; }
    internal bool IsPrimary { get; }
    internal bool IsConstant { get; }
}
