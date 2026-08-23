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

        List<Dictionary<string, object?>> rows = result.Rows.Select(row => {
            var item = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) {
                ["Bucket"] = row.BucketLabel ?? "All events",
                ["Bucket Start UTC"] = row.BucketStartUtc,
                ["Bucket End UTC"] = row.BucketEndUtc
            };
            foreach (KeyValuePair<string, object?> dimension in row.Group) {
                item[dimension.Key] = dimension.Value;
            }
            foreach (KeyValuePair<string, object?> measure in row.Measures) {
                item[measure.Key] = measure.Value;
            }
            return item;
        }).ToList();
        string[] columns = new[] { "Bucket", "Bucket Start UTC", "Bucket End UTC" }
            .Concat(result.Definition.GroupBy)
            .Concat(result.Definition.Measures.Select(static measure => measure.OutputName!))
            .ToArray();
        string range = sheet.TableFrom(rows, "Aggregation rows", configure: options => {
            options.HeaderCase = HeaderCase.Raw;
            options.NullPolicy = NullPolicy.EmptyString;
            options.Columns = columns;
        }, style: ExcelTableStyle.TableStyleLight9, visuals: visuals => {
            visuals.NumericColumnFormats["Bucket Start UTC"] = "yyyy-mm-dd hh:mm:ss";
            visuals.NumericColumnFormats["Bucket End UTC"] = "yyyy-mm-dd hh:mm:ss";
            foreach (EventAggregationMeasure measure in result.Definition.Measures) {
                visuals.NumericColumnFormats[measure.OutputName!] = measure.Operation == EventAggregationOperation.Rate
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
            var chartRows = chart.Categories.Select((category, index) => {
                var item = new Dictionary<string, object?> { ["Bucket"] = category };
                foreach (EventAggregationChartSeries series in chart.Series) {
                    item[series.Name] = series.Points[index];
                }
                return item;
            }).ToList();
            string chartRange = sheet.TableFrom(chartRows, "Chart data", configure: options => {
                options.HeaderCase = HeaderCase.Raw;
                options.Columns = new[] { "Bucket" }.Concat(chart.Series.Select(static series => series.Name)).ToArray();
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
}
