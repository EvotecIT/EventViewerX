using System.Globalization;
using HtmlForgeX;

namespace EventViewerX.Reporting;

/// <summary>Renders aggregation rows and trends through reusable HtmlForgeX monitoring components.</summary>
public static class EventAggregationHtmlRenderer {
    /// <summary>Renders a self-contained aggregation dashboard.</summary>
    public static string Render(EventAggregationResult result, string? title = null) {
        if (result == null) {
            throw new ArgumentNullException(nameof(result));
        }
        string reportTitle = string.IsNullOrWhiteSpace(title) ? "EventViewerX aggregation" : title!;
        using var document = new Document {
            LibraryMode = LibraryMode.Offline,
            ThemeMode = ThemeMode.System,
            DarkThemeVariant = HfxThemeVariant.DarkCarbon
        };
        document.Head.Title = reportTitle;
        var dashboard = new MonitoringDashboard()
            .Brand("EventViewerX")
            .FooterInfo($"{result.ExecutionMode} · {result.InputRows:N0} source rows")
            .Settings(settings => settings
                .State(state => state.StateId("eventviewerx-aggregation").HashMode(MonitoringDashboardHashMode.Namespaced).End())
                .Theme(theme => theme.Selector().End())
                .End());
        dashboard.AddPage("aggregation", reportTitle, result.Diagnostic ?? "Complete deterministic aggregation",
            TablerIconType.ChartLine, page => BuildPage(page, result), active: true,
            badge: result.Rows.Count.ToString("N0", CultureInfo.InvariantCulture));
        document.Body.Add(dashboard);
        return document.ToString();
    }

    /// <summary>Saves a self-contained aggregation dashboard.</summary>
    public static string Save(EventAggregationResult result, string path, string? title = null) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Output path cannot be empty.", nameof(path));
        }
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory!);
        }
        File.WriteAllText(fullPath, Render(result, title), new UTF8Encoding(false));
        return fullPath;
    }

    private static void BuildPage(MonitoringPage page, EventAggregationResult result) {
        page.AddMetric(metric => metric.Title("Groups").Value(result.Rows.Count.ToString("N0"))
            .Icon(TablerIconType.Stack2).State(result.AggregationComplete ? MonitoringHealthState.Healthy : MonitoringHealthState.Critical)
            .Change(result.AggregationComplete ? "Within configured bounds" : "Rows withheld"));
        page.AddMetric(metric => metric.Title("Source rows").Value(result.InputRows.ToString("N0"))
            .Icon(TablerIconType.ListDetails).State(result.InputCompleteness == EventAggregationInputCompleteness.Complete
                ? MonitoringHealthState.Healthy : MonitoringHealthState.Warning)
            .Change(result.InputCompleteness.ToString()));
        page.AddMetric(metric => metric.Title("Execution").Value(result.ExecutionMode.ToString())
            .Icon(TablerIconType.Database).State(MonitoringHealthState.Healthy).Change("Shared semantic contract"));

        EventAggregationChartData? chart = EventAggregationChartProjection.Create(result);
        if (chart != null) {
            foreach (EventAggregationChartSeries series in chart.Series) {
                page.Panel("Trend · " + series.Name, panel => panel
                    .Subtitle(chart.Measure + (chart.IsTruncated ? " · first 12 deterministic series" : string.Empty))
                    .Content(new MonitoringLineChart()
                        .Settings(settings => settings.AccessibleLabel($"{chart.Measure} trend for {series.Name}").End())
                        .Label(series.Name)
                        .Points(series.Points.Select(static value => value ?? 0d).ToArray())));
            }
        }

        var explorer = new MonitoringRecordExplorer()
            .SavedView("Aggregation")
            .ActiveGroup("Dimensions and measures")
            .Settings(settings => settings.AccessibleLabel("Event aggregation rows").PageSize(25).End());
        string[] columns = new[] { "bucket" }
            .Concat(result.Definition.GroupBy)
            .Concat(result.Definition.Measures.Select(static measure => measure.OutputName!))
            .ToArray();
        explorer.AddColumnGroup("Dimensions and measures", columns);
        explorer.AddColumn("bucket", "Bucket", pinned: true);
        foreach (string group in result.Definition.GroupBy) {
            explorer.AddColumn(group, group);
        }
        foreach (EventAggregationMeasure measure in result.Definition.Measures) {
            explorer.AddColumn(measure.OutputName!, measure.OutputName!);
        }
        for (int index = 0; index < result.Rows.Count; index++) {
            EventAggregationRow row = result.Rows[index];
            explorer.AddRecord("aggregation-" + index.ToString(CultureInfo.InvariantCulture),
                row.BucketLabel ?? "Aggregate", record => {
                    record.Cell("bucket", row.BucketLabel ?? "All events");
                    foreach (string group in result.Definition.GroupBy) {
                        record.Cell(group, Convert.ToString(row.Group[group], CultureInfo.InvariantCulture) ?? string.Empty);
                    }
                    foreach (EventAggregationMeasure measure in result.Definition.Measures) {
                        record.Cell(measure.OutputName!, Convert.ToString(
                            row.Measures[measure.OutputName!], CultureInfo.InvariantCulture) ?? string.Empty);
                    }
                });
        }
        page.Panel("Rows", panel => panel.Content(explorer));
    }
}
