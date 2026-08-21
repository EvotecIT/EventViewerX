using System.Globalization;
using OfficeIMO.Data;
using OfficeIMO.Drawing;
using OfficeIMO.Excel;
using OfficeIMO.Excel.Fluent;

namespace EventViewerX.Reporting;

/// <summary>Renders a polished OfficeIMO.Excel workbook from an event report snapshot.</summary>
public static class EventReportExcelRenderer {
    private static readonly IReadOnlyDictionary<string, double> WrappedColumnWidths =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) {
            ["Action"] = 42,
            ["Object Affected"] = 34,
            ["Account Name"] = 36,
            ["Service Name"] = 28,
            ["Privileges"] = 34,
            ["Privileges Translated"] = 40,
            ["Attribute Value"] = 42,
            ["Details"] = 46,
            ["Message"] = 56
        };

    /// <summary>Writes Summary, Events, and Coverage worksheets without re-querying event sources.</summary>
    public static string Save(EventReport report, string path) {
        if (report == null) {
            throw new ArgumentNullException(nameof(report));
        }
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Output path cannot be empty.", nameof(path));
        }
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory!);
        }
        using var document = ExcelDocument.Create(fullPath);
        document.AsFluent().Info(info => info
            .Title(report.Title)
            .Author("EventViewerX")
            .Company("Evotec")
            .Application("EventViewerX.Reporting")
            .Keywords("windows,event log,security,report"))
            .End();

        DateTime? firstEvent = report.Rows.Count == 0 ? null : report.Rows.Min(static row => row.TimeCreated);
        DateTime? lastEvent = report.Rows.Count == 0 ? null : report.Rows.Max(static row => row.TimeCreated);

        var summary = new SheetComposer(document, "Summary");
        summary.Title(report.Title, $"Generated {report.GeneratedAt:u}")
            .KpiRow(new (string, object?)[] {
                ("Events", report.Rows.Count),
                ("Sources", report.Coverage.Count),
                ("Failures", report.Coverage.Count(static item => !item.Succeeded)),
                ("Query Seconds", Math.Round(report.QueryDuration.TotalSeconds, 3)),
                ("Candidates", report.EventsScanned),
                ("Limit Reached", report.ScanLimitReached ? "Yes" : "No")
            }, perRow: 3)
            .Section("Report scope")
            .PropertiesGrid(new (string, object?)[] {
                ("First event", firstEvent?.ToString("u") ?? "No matching events"),
                ("Last event", lastEvent?.ToString("u") ?? "No matching events"),
                ("Healthy sources", report.Coverage.Count(static item => item.Succeeded)),
                ("Failed sources", report.Coverage.Count(static item => !item.Succeeded))
            }, columns: 2);
        List<Dictionary<string, object?>> typeRows = report.Sections
                .OrderByDescending(static section => section.Rows.Count)
                .ThenBy(static section => section.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .Select(static section => new Dictionary<string, object?> {
                    ["Type"] = section.DisplayName,
                    ["Events"] = section.Rows.Count
                })
                .ToList();
        string typesRange = summary.TableFrom(typeRows, "Top event types",
            configure: options => {
                options.HeaderCase = HeaderCase.Title;
                options.Columns = new[] { "Type", "Events" };
            },
            style: ExcelTableStyle.TableStyleLight9,
            visuals: visuals => {
                visuals.NumericColumnFormats["Events"] = "#,##0";
                visuals.DataBars["Events"] = OfficeColor.ParseHex("#2563EB");
            });
        summary.ApplyColumnSizing(typesRange, options => {
            options.WidthByHeader["Type"] = 32;
            options.WidthByHeader["Events"] = 24;
        });
        summary.Sheet.SetColumnWidth(3, 20);
        summary.Sheet.SetColumnWidth(4, 24);
        if (typeRows.Count > 0) {
            summary.Sheet.AddTopNBarChart(typesRange, row: 10, column: 6,
                    title: "Most frequent event types", widthPixels: 600, heightPixels: 330)
                .HideLegend()
                .SetTitleTextStyle(fontSizePoints: 12, bold: true, color: "1F2937")
                .SetCategoryAxisLabelTextStyle(fontSizePoints: 8, color: "374151")
                .SetValueAxisGridlines(showMajor: true, showMinor: false, lineColor: "E5E7EB", lineWidthPoints: 0.5)
                .SetValueAxisNumberFormat("#,##0", sourceLinked: false);
        }
        summary.PrintDefaults(showGridlines: false).Finish(autoFitColumns: false);

        AddEventSheets(document, report);

        var coverage = new SheetComposer(document, "Coverage");
        coverage.Title("Coverage", "Queried source matrix and isolated failures");
        string coverageRange = coverage.TableFrom(report.Coverage, "Coverage",
            configure: options => {
                options.HeaderCase = HeaderCase.Title;
                options.Columns = new[] { "Succeeded", "Status", "MachineName", "LogName", "Detail" };
            },
            style: ExcelTableStyle.TableStyleLight9,
            visuals: visuals => {
                visuals.TextBackgrounds["Status"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                    ["Succeeded"] = "#DCFCE7",
                    ["Supplied"] = "#DBEAFE",
                    ["AccessDenied"] = "#FEE2E2",
                    ["Timeout"] = "#FEF3C7",
                    ["HostUnavailable"] = "#FEE2E2",
                    ["EventLogError"] = "#FEE2E2"
                };
                visuals.BoldByText["Status"] = new HashSet<string>(new[] {
                    "AccessDenied", "Timeout", "HostUnavailable", "EventLogError"
                }, StringComparer.OrdinalIgnoreCase);
            });
        coverage.ApplyColumnSizing(coverageRange, options => {
            options.WidthByHeader["Succeeded"] = 12;
            options.WidthByHeader["Status"] = 18;
            options.WidthByHeader["Machine Name"] = 24;
            options.WidthByHeader["Log Name"] = 42;
            options.WidthByHeader["Detail"] = 60;
            options.WrapHeaders.UnionWith(new[] { "Log Name", "Detail" });
        }).PrintDefaults(showGridlines: false, fitToWidth: 1).Finish(autoFitColumns: false);
        document.Save();
        return fullPath;
    }

    private static void AddEventSheets(ExcelDocument document, EventReport report) {
        var sheetNames = new HashSet<string>(new[] { "Summary", "Coverage", "Event Provenance" },
            StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < report.Sections.Count; index++) {
            EventReportSection section = report.Sections[index];
            string preferredName = section.Kind == EventReportSectionKind.Generic && report.Sections.Count == 1
                ? "Events"
                : section.DisplayName;
            string sheetName = CreateWorksheetName(preferredName, sheetNames);
            var events = new SheetComposer(document, sheetName);
            string subtitle = string.IsNullOrWhiteSpace(section.Description)
                ? $"{section.Rows.Count:N0} matching events"
                : section.Description;
            events.Title(section.DisplayName, subtitle);
            EventReportPresentationSection presentation = EventReportPresentationProjection.Create(section);
            List<Dictionary<string, object?>> eventRows = presentation.Rows
                .Select(row => presentation.Columns.ToDictionary(
                    static column => column.DisplayName,
                    column => row.TryGetValue(column.Name, out object? value) ? value : null,
                    StringComparer.OrdinalIgnoreCase))
                .ToList();
            string[] eventColumns = presentation.Columns.Select(static column => column.DisplayName).ToArray();
            string tableTitle = section.Kind == EventReportSectionKind.Generic
                ? "Events"
                : $"{section.DisplayName} events";
            string eventsRange = events.TableFrom(eventRows, tableTitle,
                configure: options => {
                    options.HeaderCase = HeaderCase.Raw;
                    options.NullPolicy = NullPolicy.EmptyString;
                    options.Columns = eventColumns;
                },
                style: ExcelTableStyle.TableStyleLight9,
                visuals: visuals => {
                    foreach (EventReportColumn column in section.Columns.Where(static column =>
                                 column.ValueType == typeof(DateTime) || column.ValueType == typeof(DateTime?) ||
                                 column.ValueType == typeof(DateTimeOffset) || column.ValueType == typeof(DateTimeOffset?))) {
                        EventReportPresentationColumn? presentationColumn = presentation.Columns.FirstOrDefault(candidate =>
                            string.Equals(candidate.Name, column.Name, StringComparison.OrdinalIgnoreCase));
                        if (presentationColumn != null) {
                            visuals.NumericColumnFormats[presentationColumn.DisplayName] = "yyyy-mm-dd hh:mm:ss";
                        }
                    }
                    visuals.NumericColumnFormats["Event ID"] = "0";
                    visuals.NumericColumnFormats["Record ID"] = "0";
                    visuals.AutoFormatDynamicCollections = false;
                    visuals.TextBackgrounds["Level"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                        ["Critical"] = "#FECACA",
                        ["Error"] = "#FEE2E2",
                        ["Warning"] = "#FEF3C7",
                        ["Information"] = "#DBEAFE",
                        ["Verbose"] = "#EDE9FE"
                    };
                    visuals.BoldByText["Level"] = new HashSet<string>(new[] { "Critical", "Error" }, StringComparer.OrdinalIgnoreCase);
                });
            events.ApplyColumnSizing(eventsRange, options => ConfigureEventColumnSizing(options, eventColumns));
            (int headerRow, int lastRow) = GetRowBounds(eventsRange);
            ApplyWrappedColumns(events.Sheet, eventColumns, headerRow, lastRow);
            for (int row = headerRow + 1; row <= lastRow; row++) {
                events.Sheet.SetRowHeight(row,
                    EstimateRowHeight(eventRows[row - headerRow - 1], eventColumns));
            }
            events.PrintDefaults(showGridlines: false, fitToWidth: 1).Finish(autoFitColumns: false);
        }

        if (report.Sections.Any(static section => section.Kind != EventReportSectionKind.Generic)) {
            AddProvenanceSheet(document, report);
        }
    }

    private static void AddProvenanceSheet(ExcelDocument document, EventReport report) {
        var provenance = new SheetComposer(document, "Event Provenance");
        provenance.Title("Event provenance", "Technical Windows Event Log context for every typed report row");
        List<Dictionary<string, object?>> rows = EventReportTableProjection.ProjectProvenance(report.Rows);
        string[] columns = {
            "Time Created", "Type", "Event ID", "Level", "Source Computer", "Source Log",
            "Provider", "Record ID", "Message", "Container Log", "Collector Computer"
        };
        string range = provenance.TableFrom(rows, "Event provenance",
            configure: options => {
                options.HeaderCase = HeaderCase.Raw;
                options.NullPolicy = NullPolicy.EmptyString;
                options.Columns = columns;
            },
            style: ExcelTableStyle.TableStyleLight9,
            visuals: visuals => {
                visuals.NumericColumnFormats["Time Created"] = "yyyy-mm-dd hh:mm:ss";
                visuals.NumericColumnFormats["Event ID"] = "0";
                visuals.NumericColumnFormats["Record ID"] = "0";
                visuals.AutoFormatDynamicCollections = false;
            });
        provenance.ApplyColumnSizing(range, options => ConfigureEventColumnSizing(options, columns));
        (int headerRow, int lastRow) = GetRowBounds(range);
        ApplyWrappedColumns(provenance.Sheet, columns, headerRow, lastRow);
        for (int row = headerRow + 1; row <= lastRow; row++) {
            provenance.Sheet.SetRowHeight(row,
                EstimateRowHeight(rows[row - headerRow - 1], columns));
        }
        provenance.PrintDefaults(showGridlines: false, fitToWidth: 1).Finish(autoFitColumns: false);
    }

    private static void ConfigureEventColumnSizing(ColumnSizingOptions options, IEnumerable<string> columns) {
        options.MediumWidth = 20;
        options.WidthByHeader["Time Created"] = 20;
        options.WidthByHeader["When"] = 20;
        options.WidthByHeader["Event ID"] = 10;
        options.WidthByHeader["Level"] = 13;
        options.WidthByHeader["Computer"] = 24;
        options.WidthByHeader["Source Computer"] = 24;
        options.WidthByHeader["Source Log"] = 24;
        options.WidthByHeader["Provider"] = 28;
        options.WidthByHeader["Record ID"] = 13;
        options.WidthByHeader["Who"] = 24;
        options.WidthByHeader["Action"] = 42;
        options.WidthByHeader["Object Affected"] = 34;
        options.WidthByHeader["IP Address"] = 22;
        options.WidthByHeader["IP Port"] = 12;
        options.WidthByHeader["Account Name"] = 36;
        options.WidthByHeader["Service Name"] = 28;
        options.WidthByHeader["Privileges"] = 34;
        options.WidthByHeader["Privileges Translated"] = 40;
        options.WidthByHeader["Encryption Type"] = 28;
        options.WidthByHeader["Attribute Value"] = 42;
        options.WidthByHeader["Details"] = 46;
        options.WidthByHeader["Message"] = 56;
        options.WidthByHeader["Container Log"] = 38;
        options.WidthByHeader["Collector Computer"] = 28;
        foreach (string column in columns.Where(static column => column.Length > 18)) {
            options.WrapHeaders.Add(column);
        }
    }

    private static void ApplyWrappedColumns(
        ExcelSheet sheet,
        IReadOnlyList<string> columns,
        int headerRow,
        int lastRow) {

        for (int index = 0; index < columns.Count; index++) {
            if (!WrappedColumnWidths.TryGetValue(columns[index], out double width)) {
                continue;
            }
            sheet.WrapCells(headerRow + 1, lastRow, index + 1, width);
        }
    }

    private static double EstimateRowHeight(
        IReadOnlyDictionary<string, object?> row,
        IReadOnlyList<string> columns) {

        int maximumLines = 1;
        foreach (string column in columns) {
            if (!WrappedColumnWidths.TryGetValue(column, out double width) ||
                !row.TryGetValue(column, out object? value) || value == null) {
                continue;
            }
            string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            int charactersPerLine = Math.Max(12, (int)Math.Floor(width * 1.25));
            int lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
                .Sum(part => Math.Max(1, (int)Math.Ceiling((double)part.Length / charactersPerLine)));
            maximumLines = Math.Max(maximumLines, lines);
        }
        return Math.Min(180, Math.Max(24, maximumLines * 15 + 6));
    }

    private static string CreateWorksheetName(string value, ISet<string> used) {
        char[] invalid = { '[', ']', ':', '*', '?', '/', '\\' };
        string cleaned = new(value.Select(character => invalid.Contains(character) ? ' ' : character).ToArray());
        cleaned = string.Join(" ", cleaned.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(cleaned)) {
            cleaned = "Events";
        }
        string baseName = cleaned.Length <= 31 ? cleaned : cleaned.Substring(0, 31).TrimEnd();
        string candidate = baseName;
        int suffix = 2;
        while (!used.Add(candidate)) {
            string marker = $" {suffix++}";
            int maximumBase = 31 - marker.Length;
            candidate = (baseName.Length <= maximumBase ? baseName : baseName.Substring(0, maximumBase).TrimEnd()) + marker;
        }
        return candidate;
    }

    private static (int HeaderRow, int LastRow) GetRowBounds(string range) {
        string[] cells = range.Split(':');
        if (cells.Length != 2 || !TryGetRow(cells[0], out int first) || !TryGetRow(cells[1], out int last)) {
            throw new InvalidDataException($"OfficeIMO returned an invalid table range '{range}'.");
        }
        return (first, last);
    }

    private static bool TryGetRow(string cell, out int row) {
        row = 0;
        int index = 0;
        while (index < cell.Length && !char.IsDigit(cell[index])) {
            index++;
        }
        return index < cell.Length && int.TryParse(cell.Substring(index), out row);
    }
}
