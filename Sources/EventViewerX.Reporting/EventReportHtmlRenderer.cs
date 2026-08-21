using HtmlForgeX;

namespace EventViewerX.Reporting;

/// <summary>Renders an interactive, self-contained HtmlForgeX event report.</summary>
public static class EventReportHtmlRenderer {
    /// <summary>Renders an event report to an HTML string.</summary>
    public static string Render(EventReport report) => Render(report, null);

    /// <summary>Renders an event report to an HTML string using the supplied presentation options.</summary>
    public static string Render(EventReport report, EventReportHtmlOptions? options) {
        if (report == null) {
            throw new ArgumentNullException(nameof(report));
        }
        options ??= new EventReportHtmlOptions();
        using var document = new Document {
            LibraryMode = LibraryMode.Offline,
            ThemeMode = ThemeMode.System,
            DarkThemeVariant = HfxThemeVariant.DarkCarbon
        };
        document.Head.Title = report.Title;
        EventReportPresentationSection[] sections = report.Sections
            .Where(static section => section.Rows.Count > 0)
            .Select(EventReportPresentationProjection.Create)
            .ToArray();
        var dashboard = new MonitoringDashboard()
            .Brand("EventViewerX")
            .FooterInfo($"Generated {report.GeneratedAt:u} · query {report.QueryDuration.TotalSeconds:N2}s")
            .Settings(settings => settings
                .State(state => state.StateId("eventviewerx-report").HashMode(MonitoringDashboardHashMode.Namespaced).PersistState().End())
                .Layout(layout => layout.Spacing(MonitoringDashboardSpacing.Compact).KpiLayout(MonitoringDashboardKpiLayout.Fit).End())
                .Theme(theme => theme.Selector().End())
                .End());

        dashboard.AddPage("overview", report.Title, BuildOverviewSubtitle(report), TablerIconType.Dashboard,
            page => BuildOverviewPage(page, report, sections), active: true, badge: report.Rows.Count.ToString("N0"));
        foreach (EventReportPresentationSection section in sections) {
            string key = CreatePageKey(section.Section.Name);
            dashboard.AddPage(key, section.Section.DisplayName, BuildSectionSubtitle(section.Section),
                section.Section.Kind == EventReportSectionKind.Generic ? TablerIconType.ListDetails : TablerIconType.ReportAnalytics,
                page => BuildEventPage(page, section, options), badge: section.Section.Rows.Count.ToString("N0"), group: "Events");
        }
        dashboard.AddPage("coverage", "Coverage", "Every queried computer and source channel", TablerIconType.Server,
            page => BuildCoveragePage(page, report, options),
            badge: report.Coverage.Count(static item => !item.Succeeded) > 0
                ? report.Coverage.Count(static item => !item.Succeeded).ToString("N0")
                : null,
            group: "Diagnostics");
        document.Body.Add(dashboard);
        return document.ToString();
    }

    private static void BuildOverviewPage(
        MonitoringPage page,
        EventReport report,
        IReadOnlyList<EventReportPresentationSection> sections) {

        int failures = report.Coverage.Count(static item => !item.Succeeded);
        page.AddMetric(metric => metric.Title("Events").Value(report.Rows.Count.ToString("N0"))
            .Icon(TablerIconType.ListDetails).State(MonitoringHealthState.Healthy)
            .Change($"{sections.Count:N0} populated type{(sections.Count == 1 ? string.Empty : "s")}"));
        page.AddMetric(metric => metric.Title("Sources").Value(report.Coverage.Count.ToString("N0"))
            .Icon(TablerIconType.Server).State(failures == 0 ? MonitoringHealthState.Healthy : MonitoringHealthState.Warning)
            .Change(failures == 0 ? "All sources responded" : $"{failures:N0} failed"));
        page.AddMetric(metric => metric.Title("Query time").Value($"{report.QueryDuration.TotalSeconds:N2}s")
            .Icon(TablerIconType.Bolt).State(MonitoringHealthState.Healthy)
            .Change($"{report.EventsScanned:N0} candidates scanned"));
        page.AddMetric(metric => metric.Title("Result limit").Value(report.ScanLimitReached ? "Reached" : "Complete")
            .Icon(report.ScanLimitReached ? TablerIconType.AlertTriangle : TablerIconType.ShieldCheck)
            .State(report.ScanLimitReached ? MonitoringHealthState.Warning : MonitoringHealthState.Healthy)
            .Change(report.ScanLimitReached ? "More matching candidates may exist" : "No truncation detected"));

        page.Grid(grid => {
            var breakdown = new MonitoringConnectionBreakdown()
                .Settings(settings => settings.AccessibleLabel("Events by report type").Viewport("25rem").End());
            foreach (EventReportPresentationSection section in sections
                         .OrderByDescending(static section => section.Section.Rows.Count)
                         .Take(12)) {
                breakdown.AddCard(section.Section.DisplayName, section.Section.Rows.Count.ToString("N0"),
                    section.Section.Kind == EventReportSectionKind.Generic ? "Generic event view" : "Typed domain view",
                    section.Section.Kind == EventReportSectionKind.Generic ? TablerIconType.ListDetails : TablerIconType.ReportAnalytics,
                    MonitoringHealthState.Healthy);
            }
            grid.Panel("Report contents", panel => panel
                .Subtitle("Open a section from the sidebar to search, filter, choose columns, and inspect a record")
                .Content(breakdown), 7);

            var timeline = new MonitoringTimeline()
                .Settings(settings => settings.AccessibleLabel("Most recent report events").PageSize(8).End());
            foreach ((EventReportRow row, EventReportPresentationSection section, Dictionary<string, object?> projected) in
                     LatestRows(report, sections).Take(12)) {
                timeline.AddEvent(row.TimeCreated.ToString("yyyy-MM-dd HH:mm:ss"),
                    EventReportPresentationProjection.BuildTitle(section, projected, row),
                    EventReportPresentationProjection.BuildSummary(section, projected),
                    ResolveState(row.Level));
            }
            grid.Panel("Recent activity", panel => panel
                .Subtitle("Newest matching records across the selected report types")
                .Content(timeline), 5);
        });
    }

    private static void BuildEventPage(MonitoringPage page, EventReportPresentationSection section, EventReportHtmlOptions options) {
        DateTime? first = section.Section.Rows.Count == 0 ? null : section.Section.Rows.Min(static row => row.TimeCreated);
        DateTime? last = section.Section.Rows.Count == 0 ? null : section.Section.Rows.Max(static row => row.TimeCreated);
        page.AddMetric(metric => metric.Title("Matching events").Value(section.Section.Rows.Count.ToString("N0"))
            .Icon(TablerIconType.ListDetails).State(MonitoringHealthState.Healthy)
            .Change(section.Section.Kind == EventReportSectionKind.Generic ? "Generic event records" : "Typed domain records"));
        page.AddMetric(metric => metric.Title("First event").Value(first?.ToString("yyyy-MM-dd HH:mm") ?? "None")
            .Icon(TablerIconType.Clock).State(MonitoringHealthState.Healthy).Change("Oldest match"));
        page.AddMetric(metric => metric.Title("Latest event").Value(last?.ToString("yyyy-MM-dd HH:mm") ?? "None")
            .Icon(TablerIconType.Activity).State(MonitoringHealthState.Healthy).Change("Newest match"));
        page.AddMetric(metric => metric.Title("Visible fields").Value(section.PrimaryColumns.Count.ToString("N0"))
            .Icon(TablerIconType.Table).State(MonitoringHealthState.Healthy)
            .Change($"{section.Columns.Count:N0} populated fields available"));

        var explorer = new MonitoringRecordExplorer()
            .SavedView(section.Section.Kind == EventReportSectionKind.Generic ? "Event essentials" : "Domain essentials")
            .ActiveGroup("Primary fields")
            .Settings(settings => settings
                .AccessibleLabel($"{section.Section.DisplayName} records")
                .DrawerLabel($"Selected {section.Section.DisplayName} record")
                .PaginationLabel($"{section.Section.DisplayName} pages")
                .PageSize(25)
                .DrawerPlacement(options.RecordDrawerPlacement)
                .InlineDetails(false)
                .RawDrawerPanel(false)
                .ExportDrawerPanel(false)
                .End());
        explorer.AddColumnGroup("Primary fields", section.PrimaryColumns.Select(static column => column.Name));
        explorer.AddColumnGroup("Additional fields", section.Columns.Where(static column => !column.IsPrimary).Select(static column => column.Name));
        foreach (EventReportPresentationColumn column in section.Columns) {
            explorer.AddColumn(column.Name, column.DisplayName,
                pinned: ReferenceEquals(column, section.PrimaryColumns.FirstOrDefault()),
                visible: column.IsPrimary);
        }

        for (int index = 0; index < section.Rows.Count; index++) {
            Dictionary<string, object?> projected = section.Rows[index];
            EventReportRow source = section.Section.Rows[index];
            string key = $"{section.Section.Name}-{source.RecordId?.ToString() ?? index.ToString()}";
            explorer.AddRecord(key, EventReportPresentationProjection.BuildTitle(section, projected, source), record => {
                foreach (EventReportPresentationColumn column in section.Columns) {
                    string value = projected.TryGetValue(column.Name, out object? raw)
                        ? EventReportPresentationProjection.FormatValue(raw)
                        : string.Empty;
                    record.Cell(column.Name, value, BuildSortValue(raw));
                }
                AddProvenance(record, source);
                record.Tag(section.Section.DisplayName);
                if (!string.IsNullOrWhiteSpace(source.SourceComputer)) {
                    record.Tag(source.SourceComputer);
                }
            });
        }

        page.Panel("Records", panel => panel
            .Subtitle("Search all values, filter individual fields, choose columns, or select a row for provenance")
            .Content(explorer));
    }

    private static void BuildCoveragePage(MonitoringPage page, EventReport report, EventReportHtmlOptions options) {
        int healthy = report.Coverage.Count(static item => item.Succeeded);
        int failed = report.Coverage.Count - healthy;
        page.AddMetric(metric => metric.Title("Healthy sources").Value(healthy.ToString("N0"))
            .Icon(TablerIconType.ShieldCheck).State(MonitoringHealthState.Healthy).Change("Query completed"));
        page.AddMetric(metric => metric.Title("Failed sources").Value(failed.ToString("N0"))
            .Icon(TablerIconType.AlertTriangle).State(failed == 0 ? MonitoringHealthState.Healthy : MonitoringHealthState.Critical)
            .Change(failed == 0 ? "No isolated failures" : "Review source details"));

        var explorer = new MonitoringRecordExplorer()
            .SavedView("Source health")
            .ActiveGroup("Coverage")
            .Settings(settings => settings
                .AccessibleLabel("Queried event sources")
                .PageSize(25)
                .DrawerPlacement(options.RecordDrawerPlacement)
                .InlineDetails(false)
                .End())
            .AddColumnGroup("Coverage", "status", "machine", "log", "detail")
            .AddColumn("status", "Status", pinned: true)
            .AddColumn("machine", "Machine")
            .AddColumn("log", "Log or path")
            .AddColumn("detail", "Detail");
        for (int index = 0; index < report.Coverage.Count; index++) {
            EventReportCoverage item = report.Coverage[index];
            explorer.AddRecord($"coverage-{index}", $"{item.MachineName} · {item.LogName}", record => record
                .StateCell("status", item.Status.ToString(), item.Succeeded ? MonitoringHealthState.Healthy : MonitoringHealthState.Critical)
                .Cell("machine", item.MachineName)
                .Cell("log", item.LogName)
                .Cell("detail", EventReportPresentationProjection.FormatValue(item.Detail)));
        }
        page.Panel("Source matrix", panel => panel
            .Subtitle("Failures are isolated so healthy sources still contribute data")
            .Content(explorer));
    }

    private static IEnumerable<(EventReportRow Row, EventReportPresentationSection Section, Dictionary<string, object?> Projected)> LatestRows(
        EventReport report,
        IReadOnlyList<EventReportPresentationSection> sections) {

        var sectionByName = sections.ToDictionary(static section => section.Section.Name, StringComparer.OrdinalIgnoreCase);
        foreach (EventReportRow row in report.Rows.OrderByDescending(static row => row.TimeCreated)) {
            if (!sectionByName.TryGetValue(row.Type, out EventReportPresentationSection? section)) {
                section = sections.FirstOrDefault(candidate => candidate.Section.Rows.Contains(row));
            }
            if (section == null) {
                continue;
            }
            int index = FindRowIndex(section.Section.Rows, row);
            if (index >= 0 && index < section.Rows.Count) {
                yield return (row, section, section.Rows[index]);
            }
        }
    }

    private static void AddProvenance(MonitoringExplorerRecord record, EventReportRow row) {
        AddDetail(record, "Event ID", row.EventId.ToString(), "Provenance");
        AddDetail(record, "Level", row.Level, "Provenance");
        AddDetail(record, "Source computer", row.SourceComputer, "Provenance");
        AddDetail(record, "Source log", row.SourceLog, "Provenance");
        AddDetail(record, "Provider", row.Provider, "Provenance");
        AddDetail(record, "Record ID", row.RecordId?.ToString(), "Provenance");
        AddDetail(record, "Container log", row.ContainerLog, "Provenance");
        AddDetail(record, "Collector computer", row.CollectorComputer, "Provenance");
        string message = EventReportPresentationProjection.FormatValue(row.Message);
        if (!string.IsNullOrWhiteSpace(message)) {
            record.Detail("Message excerpt", message.Length <= 320 ? message : message.Substring(0, 319) + "…", "Message");
        }
    }

    private static int FindRowIndex(IReadOnlyList<EventReportRow> rows, EventReportRow row) {
        for (int index = 0; index < rows.Count; index++) {
            if (ReferenceEquals(rows[index], row)) {
                return index;
            }
        }
        return -1;
    }

    private static void AddDetail(MonitoringExplorerRecord record, string label, string? value, string? group = null) {
        string formatted = EventReportPresentationProjection.FormatValue(value);
        if (!string.IsNullOrWhiteSpace(formatted)) {
            record.Detail(label, formatted, group);
        }
    }

    private static string BuildOverviewSubtitle(EventReport report) =>
        $"{report.Rows.Count:N0} matching events across {report.Coverage.Count:N0} queried sources";

    private static string BuildSectionSubtitle(EventReportSection section) =>
        string.IsNullOrWhiteSpace(section.Description)
            ? $"{section.Rows.Count:N0} matching event{(section.Rows.Count == 1 ? string.Empty : "s")}"
            : section.Description;

    private static string CreatePageKey(string value) {
        var result = new StringBuilder(value.Length);
        foreach (char character in value) {
            result.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-');
        }
        return result.ToString().Trim('-');
    }

    private static string? BuildSortValue(object? value) => value switch {
        DateTime date => date.ToUniversalTime().Ticks.ToString("D19"),
        DateTimeOffset date => date.UtcTicks.ToString("D19"),
        IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => null
    };

    private static MonitoringHealthState ResolveState(string? level) => level?.ToLowerInvariant() switch {
        "critical" or "error" => MonitoringHealthState.Critical,
        "warning" => MonitoringHealthState.Warning,
        _ => MonitoringHealthState.Healthy
    };

    /// <summary>Writes a self-contained HTML report.</summary>
    public static string Save(EventReport report, string path, bool open = false) => Save(report, path, null, open);

    /// <summary>Writes a self-contained HTML report using the supplied presentation options.</summary>
    public static string Save(EventReport report, string path, EventReportHtmlOptions? options, bool open = false) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Output path cannot be empty.", nameof(path));
        }
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory!);
        }
        File.WriteAllText(fullPath, Render(report, options), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        if (open) {
            Process.Start(new ProcessStartInfo(fullPath) { UseShellExecute = true });
        }
        return fullPath;
    }
}
