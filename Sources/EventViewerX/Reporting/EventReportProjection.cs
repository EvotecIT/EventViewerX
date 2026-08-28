namespace EventViewerX.Reporting;

internal sealed class EventReportProjection {
    internal EventReportProjection(EventReportRow row, EventReportSectionDefinition section) {
        Row = row;
        Section = section;
    }

    internal EventReportRow Row { get; }
    internal EventReportSectionDefinition Section { get; }
}

internal sealed class EventReportSectionDefinition {
    internal EventReportSectionDefinition(
        string key,
        string name,
        string displayName,
        string description,
        EventReportSectionKind kind,
        IReadOnlyList<EventReportColumn> columns) {

        Key = key;
        Name = name;
        DisplayName = displayName;
        Description = description;
        Kind = kind;
        Columns = columns;
    }

    internal string Key { get; }
    internal string Name { get; }
    internal string DisplayName { get; }
    internal string Description { get; }
    internal EventReportSectionKind Kind { get; }
    internal IReadOnlyList<EventReportColumn> Columns { get; }
}
