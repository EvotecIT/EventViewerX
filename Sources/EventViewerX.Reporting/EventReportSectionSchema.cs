namespace EventViewerX.Reporting;

/// <summary>Serializable homogeneous report schema used by storage and transport adapters.</summary>
public sealed class EventReportSectionSchema {
    /// <summary>Stable definition name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Human-friendly section name.</summary>
    public string DisplayName { get; set; } = string.Empty;
    /// <summary>Definition purpose.</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>Generic, built-in typed, or custom definition.</summary>
    public EventReportSectionKind Kind { get; set; }
    /// <summary>Stable homogeneous columns.</summary>
    public IReadOnlyList<EventReportColumnSchema> Columns { get; set; } = Array.Empty<EventReportColumnSchema>();

    /// <summary>Creates a detached schema from a report section.</summary>
    public static EventReportSectionSchema FromSection(EventReportSection section) {
        if (section == null) {
            throw new ArgumentNullException(nameof(section));
        }
        return new EventReportSectionSchema {
            Name = section.Name,
            DisplayName = section.DisplayName,
            Description = section.Description,
            Kind = section.Kind,
            Columns = section.Columns.Select(static column => new EventReportColumnSchema {
                Name = column.Name,
                DisplayName = column.DisplayName,
                ValueTypeName = EventReportColumnSchema.GetStableTypeName(column.ValueType),
                Aliases = column.Aliases.ToArray()
            }).ToArray()
        };
    }

    /// <summary>Creates the authoritative report schema for one built-in leaf event type.</summary>
    public static EventReportSectionSchema FromType(EventType type) =>
        FromDefinition(EventReportProjectionFactory.Create(type));

    /// <summary>Creates the authoritative report schema for one validated custom event definition.</summary>
    public static EventReportSectionSchema FromDefinition(EventDefinition definition) =>
        FromDefinition(EventReportProjectionFactory.Create(definition));

    /// <summary>Creates the stable native-metadata schema used by an empty generic report.</summary>
    public static EventReportSectionSchema CreateGeneric() =>
        FromDefinition(EventReportProjectionFactory.CreateGenericDefinition());

    private static EventReportSectionSchema FromDefinition(EventReportSectionDefinition definition) => new() {
        Name = definition.Name,
        DisplayName = definition.DisplayName,
        Description = definition.Description,
        Kind = definition.Kind,
        Columns = definition.Columns.Select(static column => new EventReportColumnSchema {
            Name = column.Name,
            DisplayName = column.DisplayName,
            ValueTypeName = EventReportColumnSchema.GetStableTypeName(column.ValueType),
            Aliases = column.Aliases.ToArray()
        }).ToArray()
    };
}
