using System.Globalization;
using OfficeIMO.Data;
using OfficeIMO.Excel;
using OfficeIMO.Excel.Fluent;

namespace EventViewerX.Reporting;

/// <summary>Renders aggregation rows and an embedded trend chart through OfficeIMO.Excel.</summary>
public static class EventAggregationExcelRenderer {
    /// <summary>Saves an aggregation workbook with completeness metadata, rows, and a chart for numeric measures.</summary>
    public static string Save(EventAggregationResult result, string path, string? title = null) {
        if (result == null) {
            throw new ArgumentNullException(nameof(result));
        }
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Output path cannot be empty.", nameof(path));
        }
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory!);
        }
        string reportTitle = string.IsNullOrWhiteSpace(title) ? "EventViewerX aggregation" : title!;
        using var document = ExcelDocument.Create(fullPath);
        document.AsFluent().Info(info => info
            .Title(reportTitle)
            .Author("EventViewerX")
            .Company("Evotec")
            .Application("EventViewerX.Reporting")
            .Keywords("windows,event log,aggregation,trend"))
            .End();
        var sheet = new SheetComposer(document, "Aggregation");
        sheet.Title(reportTitle, result.Diagnostic ?? $"{result.ExecutionMode} over {result.InputRows:N0} source rows")
            .KpiRow(new (string, object?)[] {
                ("Groups", result.Rows.Count),
                ("Source rows", result.InputRows),
                ("Input", result.InputCompleteness.ToString()),
                ("Aggregation", result.AggregationComplete ? "Complete" : "Incomplete"),
                ("Execution", result.ExecutionMode.ToString())
            }, perRow: 3);

        var usedColumnNames = new HashSet<string>(
            new[] { "Bucket", "Bucket Start UTC", "Bucket End UTC" },
            StringComparer.OrdinalIgnoreCase);
        KeyValuePair<string, string>[] groupColumns = result.Definition.GroupBy
            .Select(field => new KeyValuePair<string, string>(
                field,
                CreateUniqueColumnName(field, "Group", usedColumnNames)))
            .ToArray();
        (EventAggregationMeasure Measure, string Name)[] measureColumns = result.Definition.Measures
            .Select(measure => (
                measure,
                CreateUniqueColumnName(measure.OutputName!, "Measure", usedColumnNames)))
            .ToArray();

        List<Dictionary<string, object?>> rows = result.Rows.Select(row => {
            var item = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) {
                ["Bucket"] = row.BucketLabel ?? "All events",
                ["Bucket Start UTC"] = row.BucketStartUtc,
                ["Bucket End UTC"] = row.BucketEndUtc
            };
            foreach (KeyValuePair<string, string> binding in groupColumns) {
                item[binding.Value] = row.Group[binding.Key];
            }
            foreach ((EventAggregationMeasure measure, string name) in measureColumns) {
                item[name] = row.Measures[measure.OutputName!];
            }
            return item;
        }).ToList();
        string[] columns = new[] { "Bucket", "Bucket Start UTC", "Bucket End UTC" }
            .Concat(groupColumns.Select(static binding => binding.Value))
            .Concat(measureColumns.Select(static binding => binding.Name))
            .ToArray();
        string range = sheet.TableFrom(rows, "Aggregation rows", configure: options => {
            options.HeaderCase = HeaderCase.Raw;
            options.NullPolicy = NullPolicy.EmptyString;
            options.Columns = columns;
        }, style: ExcelTableStyle.TableStyleLight9, visuals: visuals => {
            visuals.NumericColumnFormats["Bucket Start UTC"] = "yyyy-mm-dd hh:mm:ss";
            visuals.NumericColumnFormats["Bucket End UTC"] = "yyyy-mm-dd hh:mm:ss";
            foreach ((EventAggregationMeasure measure, string name) in measureColumns) {
                visuals.NumericColumnFormats[name] = measure.Operation == EventAggregationOperation.Rate
                    ? "0.0000"
                    : measure.Operation is EventAggregationOperation.Count or EventAggregationOperation.DistinctCount
                        ? "#,##0"
                        : "yyyy-mm-dd hh:mm:ss";
            }
        });
        sheet.ApplyColumnSizing(range, options => {
            options.WidthByHeader["Bucket"] = 25;
            options.WidthByHeader["Bucket Start UTC"] = 21;
            options.WidthByHeader["Bucket End UTC"] = 21;
        });

        EventAggregationChartData? chart = EventAggregationChartProjection.Create(result);
        if (chart != null) {
            var usedChartColumns = new HashSet<string>(new[] { "Bucket" }, StringComparer.OrdinalIgnoreCase);
            KeyValuePair<EventAggregationChartSeries, string>[] chartColumns = chart.Series
                .Select(series => new KeyValuePair<EventAggregationChartSeries, string>(
                    series,
                    CreateUniqueColumnName(series.Name, "Series", usedChartColumns)))
                .ToArray();
            var chartRows = chart.Categories.Select((category, index) => {
                var item = new Dictionary<string, object?> { ["Bucket"] = category };
                foreach (KeyValuePair<EventAggregationChartSeries, string> binding in chartColumns) {
                    item[binding.Value] = binding.Key.Points[index];
                }
                return item;
            }).ToList();
            string chartRange = sheet.TableFrom(chartRows, "Chart data", configure: options => {
                options.HeaderCase = HeaderCase.Raw;
                options.Columns = new[] { "Bucket" }.Concat(chartColumns.Select(static binding => binding.Value)).ToArray();
            }, style: ExcelTableStyle.TableStyleLight9);
            sheet.Sheet.AddRevenueTrendChart(chartRange, row: 10, column: Math.Max(6, columns.Length + 2),
                    title: chart.Measure + " trend" + (chart.IsTruncated ? " (first 12 series)" : string.Empty), widthPixels: 700, heightPixels: 360)
                .SetCategoryAxisLabelRotation(-35)
                .SetValueAxisGridlines(showMajor: true, showMinor: false, lineColor: "E5E7EB", lineWidthPoints: 0.5);
        }
        sheet.PrintDefaults(showGridlines: false, fitToWidth: 1).Finish(autoFitColumns: false);
        document.Save();
        return fullPath;
    }

    private static string CreateUniqueColumnName(
        string requested,
        string prefix,
        ISet<string> usedNames) {

        if (usedNames.Add(requested)) {
            return requested;
        }
        string root = prefix + "." + requested;
        string candidate = root;
        int suffix = 2;
        while (!usedNames.Add(candidate)) {
            candidate = root + "." + suffix.ToString(CultureInfo.InvariantCulture);
            suffix++;
        }
        return candidate;
    }
}
