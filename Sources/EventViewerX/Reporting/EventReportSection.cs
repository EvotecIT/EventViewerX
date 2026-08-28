namespace EventViewerX.Reporting;

/// <summary>A homogeneous report table whose rows share one domain-specific schema.</summary>
public sealed class EventReportSection {
    internal EventReportSection(
        string name,
        string displayName,
        string description,
        EventReportSectionKind kind,
        IReadOnlyList<EventReportColumn> columns,
        IReadOnlyList<EventReportRow> rows) {

        Name = name;
        DisplayName = displayName;
        Description = description;
        Kind = kind;
        Columns = columns;
        Rows = rows;
    }

    /// <summary>Stable built-in or custom definition name.</summary>
    public string Name { get; }
    /// <summary>Human-friendly section title.</summary>
    public string DisplayName { get; }
    /// <summary>Definition purpose shown by report renderers.</summary>
    public string Description { get; }
    /// <summary>Presentation contract for this section.</summary>
    public EventReportSectionKind Kind { get; }
    /// <summary>Visible domain columns in their definition order.</summary>
    public IReadOnlyList<EventReportColumn> Columns { get; }
    /// <summary>Rows sharing this section's schema.</summary>
    public IReadOnlyList<EventReportRow> Rows { get; }
}
