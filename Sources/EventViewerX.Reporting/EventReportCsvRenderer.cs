using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using OfficeIMO.CSV;

namespace EventViewerX.Reporting;

/// <summary>Writes homogeneous typed CSV files and deterministic multi-schema ZIP bundles.</summary>
public static class EventReportCsvRenderer {
    /// <summary>
    /// Saves one homogeneous section as CSV, or saves multiple typed sections as a ZIP bundle.
    /// A multi-section report requires a .zip destination so unrelated schemas never share one table.
    /// </summary>
    public static string Save(
        EventReport report,
        string path,
        EventReportCsvOptions? options = null) {

        if (report == null) {
            throw new ArgumentNullException(nameof(report));
        }
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Output path cannot be empty.", nameof(path));
        }
        options ??= new EventReportCsvOptions();
        if (report.Sections.Count == 0) {
            throw new InvalidOperationException("The report does not contain a report section to export.");
        }
        string fullPath = Path.GetFullPath(path);
        string extension = Path.GetExtension(fullPath);
        bool bundle = string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase);
        if (!bundle && !string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase)) {
            throw new ArgumentException("CsvPath must end in .csv for one schema or .zip for a report bundle.", nameof(path));
        }
        if (report.Sections.Count > 1 && !bundle) {
            throw new ArgumentException(
                "Reports with multiple typed schemas require a .zip CsvPath. Each definition is written to its own CSV file.",
                nameof(path));
        }
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory!);
        }
        return bundle
            ? SaveBundle(report, fullPath, options)
            : SaveSingle(report.Sections[0], fullPath, options);
    }

    private static string SaveSingle(
        EventReportSection section,
        string fullPath,
        EventReportCsvOptions options) {

        string temporaryPath = CreateTemporaryPath(fullPath);
        try {
            using (CsvRowWriter writer = CsvRowWriter.CreateFile(
                       temporaryPath,
                       options.CreateSaveOptions())) {
                WriteSection(writer, section);
            }
            MoveIntoPlace(temporaryPath, fullPath);
            return fullPath;
        } finally {
            TryDelete(temporaryPath);
        }
    }

    private static string SaveBundle(
        EventReport report,
        string fullPath,
        EventReportCsvOptions options) {

        string temporaryPath = CreateTemporaryPath(fullPath);
        try {
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (options.IncludeProvenance &&
                report.Sections.Any(static section => section.Kind != EventReportSectionKind.Generic)) {
                usedNames.Add("event-provenance.csv");
            }
            if (options.IncludeCoverage) {
                usedNames.Add("coverage.csv");
            }
            var sectionFiles = new List<(EventReportSection Section, string FileName)>();
            foreach (EventReportSection section in report.Sections) {
                string fileName = CreateUniqueFileName(section.Name, usedNames);
                sectionFiles.Add((section, fileName));
            }
            using (FileStream stream = new(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.ReadWrite,
                       FileShare.None))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false)) {
                foreach ((EventReportSection section, string fileName) in sectionFiles) {
                    WriteEntry(archive, fileName, writer => WriteSection(writer, section), options);
                }
                if (options.IncludeProvenance &&
                    report.Sections.Any(static section => section.Kind != EventReportSectionKind.Generic)) {
                    WriteDictionaries(
                        archive,
                        "event-provenance.csv",
                        EventReportTableProjection.ProjectProvenance(report.Rows),
                        new[] {
                            "Time Created", "Type", "Event ID", "Level", "Source Computer",
                            "Source Log", "Provider", "Record ID", "Message", "Raw Values", "Container Log",
                            "Collector Computer"
                        },
                        options);
                }
                if (options.IncludeCoverage) {
                    List<Dictionary<string, object?>> coverage = report.Coverage
                        .Select(static item => new Dictionary<string, object?> {
                            ["Succeeded"] = item.Succeeded,
                            ["Status"] = item.Status,
                            ["Machine Name"] = item.MachineName,
                            ["Log Name"] = item.LogName,
                            ["Detail"] = item.Detail
                        })
                        .ToList();
                    WriteDictionaries(
                        archive,
                        "coverage.csv",
                        coverage,
                        new[] { "Succeeded", "Status", "Machine Name", "Log Name", "Detail" },
                        options);
                }
                ZipArchiveEntry manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
                using Stream manifestStream = manifestEntry.Open();
                using var manifestWriter = new StreamWriter(
                    manifestStream,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                manifestWriter.Write(JsonSerializer.Serialize(new {
                    report.Title,
                    report.GeneratedAt,
                    QueryDurationMilliseconds = report.QueryDuration.TotalMilliseconds,
                    EventCount = report.Rows.Count,
                    report.EventsScanned,
                    report.ScanLimitReached,
                    report.CompletenessDiagnostic,
                    Sections = sectionFiles.Select(static item => new {
                        item.Section.Name,
                        item.Section.DisplayName,
                        Kind = item.Section.Kind.ToString(),
                        RowCount = item.Section.Rows.Count,
                        item.FileName
                    })
                }));
            }
            MoveIntoPlace(temporaryPath, fullPath);
            return fullPath;
        } finally {
            TryDelete(temporaryPath);
        }
    }

    private static void WriteSection(CsvRowWriter writer, EventReportSection section) {
        List<Dictionary<string, object?>> rows = EventReportTableProjection.Project(section);
        IReadOnlyDictionary<string, string> displayNames =
            EventReportTableProjection.CreateUniqueDisplayNames(section.Columns);
        EventReportColumn[] rawColumns = section.Columns.Where(column => section.Rows.Any(row =>
            row.NormalizedValues.TryGetValue(column.Name, out EventNormalizedValue? value) &&
            value.Outcome != EventNormalizationOutcome.Unchanged)).ToArray();
        var usedHeaders = new HashSet<string>(displayNames.Values, StringComparer.OrdinalIgnoreCase);
        IReadOnlyDictionary<string, string> rawHeaders = rawColumns.ToDictionary(
            static column => column.Name,
            column => CreateUniqueHeader(displayNames[column.Name] + " Raw", usedHeaders),
            StringComparer.OrdinalIgnoreCase);
        string[] headers = section.Columns.Select(column => displayNames[column.Name])
            .Concat(rawColumns.Select(column => rawHeaders[column.Name]))
            .ToArray();
        writer.WriteRows(headers, rows.Select((row, index) =>
            section.Columns.Select(column => row.TryGetValue(column.Name, out object? value) ? value : null)
                .Concat(rawColumns.Select(column => section.Rows[index].Values.TryGetValue(
                    column.Name,
                    out object? raw) ? raw : null))
                .ToArray()));
    }

    private static string CreateUniqueHeader(string requested, ISet<string> usedHeaders) {
        string candidate = requested;
        int suffix = 2;
        while (!usedHeaders.Add(candidate)) {
            candidate = requested + " (" + suffix.ToString(CultureInfo.InvariantCulture) + ")";
            suffix++;
        }
        return candidate;
    }

    private static void WriteDictionaries(
        ZipArchive archive,
        string fileName,
        IReadOnlyList<Dictionary<string, object?>> rows,
        IReadOnlyList<string> columns,
        EventReportCsvOptions options) {

        WriteEntry(archive, fileName, writer => writer.WriteRows(
            columns,
            rows.Select(row => columns
                .Select(column => row.TryGetValue(column, out object? value) ? value : null)
                .ToArray())), options);
    }

    private static void WriteEntry(
        ZipArchive archive,
        string fileName,
        Action<CsvRowWriter> write,
        EventReportCsvOptions options) {

        ZipArchiveEntry entry = archive.CreateEntry(fileName, CompressionLevel.Optimal);
        using Stream stream = entry.Open();
        using var textWriter = new StreamWriter(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            64 * 1024,
            leaveOpen: false);
        using var csvWriter = new CsvRowWriter(textWriter, options.CreateSaveOptions(), leaveOpen: true);
        write(csvWriter);
    }

    private static string CreateUniqueFileName(string name, ISet<string> usedNames) {
        string clean = new string((string.IsNullOrWhiteSpace(name) ? "events" : name.Trim())
            .Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '-' : character)
            .ToArray()).Trim(' ', '.', '-');
        if (clean.Length == 0) {
            clean = "events";
        }
        string candidate = clean + ".csv";
        int suffix = 2;
        while (!usedNames.Add(candidate)) {
            candidate = clean + "-" + suffix.ToString(CultureInfo.InvariantCulture) + ".csv";
            suffix++;
        }
        return candidate;
    }

    private static string CreateTemporaryPath(string destination) =>
        Path.Combine(
            Path.GetDirectoryName(destination) ?? Directory.GetCurrentDirectory(),
            "." + Path.GetFileName(destination) + "." + Guid.NewGuid().ToString("N") + ".tmp");

    private static void MoveIntoPlace(string temporaryPath, string destination) {
        if (File.Exists(destination)) {
            File.Replace(temporaryPath, destination, null);
        } else {
            File.Move(temporaryPath, destination);
        }
    }

    private static void TryDelete(string path) {
        try {
            if (File.Exists(path)) {
                File.Delete(path);
            }
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
        }
    }
}
