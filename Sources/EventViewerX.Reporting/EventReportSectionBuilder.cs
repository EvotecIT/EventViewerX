namespace EventViewerX.Reporting;

internal static class EventReportSectionBuilder {
    internal static IReadOnlyList<EventReportSection> Build(
        IReadOnlyList<EventReportProjection> projections,
        IReadOnlyList<EventReportSectionDefinition>? emptyDefinitions = null) {

        if (projections.Count == 0 && emptyDefinitions != null) {
            return emptyDefinitions.Select(static definition => new EventReportSection(
                definition.Name,
                definition.DisplayName,
                definition.Description,
                definition.Kind,
                definition.Columns,
                Array.Empty<EventReportRow>())).ToArray();
        }
        var orderedKeys = new List<string>();
        var groups = new Dictionary<string, List<EventReportProjection>>(StringComparer.OrdinalIgnoreCase);
        var keysByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (EventReportProjection projection in projections) {
            if (keysByName.TryGetValue(projection.Section.Name, out string? existingKey) &&
                !string.Equals(existingKey, projection.Section.Key, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidDataException(
                    $"Report input contains conflicting schema revisions for definition '{projection.Section.Name}'.");
            }
            keysByName[projection.Section.Name] = projection.Section.Key;
            if (!groups.TryGetValue(projection.Section.Key, out List<EventReportProjection>? group)) {
                group = new List<EventReportProjection>();
                groups.Add(projection.Section.Key, group);
                orderedKeys.Add(projection.Section.Key);
            }
            group.Add(projection);
        }

        var sections = new List<EventReportSection>(orderedKeys.Count);
        foreach (string key in orderedKeys) {
            List<EventReportProjection> group = groups[key];
            EventReportSectionDefinition definition = group[0].Section;
            IReadOnlyList<EventReportColumn> columns = definition.Kind == EventReportSectionKind.Generic
                ? EventReportTableProjection.BuildGenericColumns(group.Select(static item => item.Row))
                : definition.Columns;
            sections.Add(new EventReportSection(
                definition.Name,
                definition.DisplayName,
                definition.Description,
                definition.Kind,
                columns,
                group.Select(static item => item.Row).ToArray()));
        }
        return sections;
    }
}
