using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Text;
using EventViewerX;
using EventViewerX.Reporting;

namespace EventLogParsing.BenchmarkHost;

internal static class EventEnumerationRunner {
    private static readonly string[] MetadataPropertyPaths = {
        "Event/System/EventID",
        "Event/System/EventRecordID",
        "Event/System/TimeCreated/@SystemTime",
        "Event/System/Provider/@Name",
        "Event/System/Computer",
        "Event/System/Channel",
        "Event/System/Level",
        "Event/System/Keywords",
        "Event/System/Task",
        "Event/System/Opcode",
        "Event/System/Execution/@ProcessID",
        "Event/System/Execution/@ThreadID",
        "Event/System/EventID/@Qualifiers",
        "Event/System/Provider/@Guid",
        "Event/System/Correlation/@ActivityID",
        "Event/System/Correlation/@RelatedActivityID",
        "Event/System/Security/@UserID",
        "Event/System/Version"
    };

    public static BenchmarkResult Run(BenchmarkOptions options) {
        ArgumentNullException.ThrowIfNull(options);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: false);
        int gen0Before = GC.CollectionCount(0);
        int gen1Before = GC.CollectionCount(1);
        int gen2Before = GC.CollectionCount(2);
        var accumulator = new EventAccumulator();
        var stopwatch = Stopwatch.StartNew();
        ExportMeasurement? exportResult = null;

        if (string.Equals(options.Engine, "eventviewerxreport", StringComparison.OrdinalIgnoreCase)) {
            exportResult = RunEventViewerXReport(options, accumulator);
        } else if (string.Equals(options.Engine, "eventviewerxtyped", StringComparison.OrdinalIgnoreCase)) {
            RunEventViewerXTyped(options, accumulator);
        } else if (string.Equals(options.Engine, "eventviewerxexport", StringComparison.OrdinalIgnoreCase)) {
            exportResult = RunEventViewerXExport(options);
        } else if (string.Equals(options.Engine, "eventviewerx", StringComparison.OrdinalIgnoreCase)) {
            RunEventViewerX(options, accumulator);
        } else if (string.Equals(options.Engine, "propertyselector", StringComparison.OrdinalIgnoreCase)) {
            RunPropertySelector(options, accumulator);
        } else if (options.OutputFormat == EventExportFormat.Xml) {
            exportResult = RawXmlExport.Run(options);
        } else {
            RunDotNet(options, accumulator);
        }

        stopwatch.Stop();
        using Process process = Process.GetCurrentProcess();
        string productVersion = options.Engine.StartsWith("eventviewerx", StringComparison.OrdinalIgnoreCase)
            ? typeof(EventLogEngine).Assembly.GetName().Version?.ToString() ?? string.Empty
            : typeof(EventLogReader).Assembly.GetName().Version?.ToString() ?? string.Empty;

        return new BenchmarkResult {
            Engine = options.Engine,
            ReadMode = options.ReadMode.ToString(),
            FixturePath = options.Path,
            RuntimeVersion = Environment.Version.ToString(),
            ProductVersion = productVersion,
            Count = exportResult?.EventCount ?? accumulator.Count,
            IdSum = accumulator.IdSum,
            RecordIdSum = accumulator.RecordIdSum,
            TimeTicksXor = accumulator.TimeTicksXor,
            OrderSignature = accumulator.OrderSignature,
            FirstRecordId = accumulator.FirstRecordId,
            LastRecordId = accumulator.LastRecordId,
            MetadataTouch = accumulator.MetadataTouch,
            MessageCharacters = accumulator.MessageCharacters,
            XmlCharacters = accumulator.XmlCharacters,
            PropertyCount = accumulator.PropertyCount,
            StructuredFieldCount = accumulator.StructuredFieldCount,
            MessageFieldCount = accumulator.MessageFieldCount,
            AttachmentBytes = accumulator.AttachmentBytes,
            AllocatedBytes = GC.GetTotalAllocatedBytes(precise: false) - allocatedBefore,
            PeakWorkingSetBytes = process.PeakWorkingSet64,
            Gen0Collections = GC.CollectionCount(0) - gen0Before,
            Gen1Collections = GC.CollectionCount(1) - gen1Before,
            Gen2Collections = GC.CollectionCount(2) - gen2Before,
            ElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
            OutputPath = exportResult?.Path ?? options.OutputPath,
            OutputBytes = exportResult?.Bytes ?? 0,
            OutputSha256 = exportResult?.Sha256
        };
    }

    private static void RunEventViewerX(BenchmarkOptions options, EventAccumulator accumulator) {
        var query = new EventLogFileQuery(options.Path) {
            MaxEvents = options.MaxEvents,
            Oldest = true,
            ReadMode = options.ReadMode,
            MessageCulture = options.MessageCulture
        };
        foreach (EventObject eventObject in EventLogEngine.ReadFile(query)) {
            accumulator.Add(eventObject, options.ReadMode);
        }
    }

    private static void RunEventViewerXTyped(BenchmarkOptions options, EventAccumulator accumulator) {
        foreach (EventTypeRecord record in ReadTypedFileAsync(options).GetAwaiter().GetResult()) {
            accumulator.Add(EventReportEngine.CreateRow(record));
        }
    }

    private static ExportMeasurement RunEventViewerXReport(BenchmarkOptions options, EventAccumulator accumulator) {
        EventReport report;
        if (options.Types.Length > 0) {
            EventReportRequest request = EventReportRequest.ForTypes(options.Types);
            request.Paths = new[] { options.Path };
            request.MaxEvents = options.MaxEvents;
            request.Oldest = true;
            request.Title = "EventViewerX benchmark";
            report = EventReportEngine.QueryAsync(request).GetAwaiter().GetResult();
        } else {
            var request = EventReportRequest.ForFiles(options.Path);
            request.MaxEvents = options.MaxEvents;
            request.Oldest = true;
            request.Title = "EventViewerX benchmark";
            report = EventReportEngine.QueryAsync(request).GetAwaiter().GetResult();
        }
        foreach (EventReportRow row in report.Rows) {
            accumulator.Add(row);
        }
        string output = options.OutputPath!;
        string? directory = Path.GetDirectoryName(output);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }
        long bytes = 0;
        switch (options.ReportFormat) {
            case "Html":
                EventReportHtmlRenderer.Save(report, output);
                bytes = new FileInfo(output).Length;
                break;
            case "Excel":
                EventReportExcelRenderer.Save(report, output);
                bytes = new FileInfo(output).Length;
                break;
            case "Email": {
                EventEmailPackage email = EventReportEmailRenderer.RenderAsync(report).GetAwaiter().GetResult();
                File.WriteAllText(output, email.Html, new UTF8Encoding(false));
                string plainTextPath = Path.ChangeExtension(output, ".txt");
                File.WriteAllText(plainTextPath, email.PlainText, new UTF8Encoding(false));
                bytes = new FileInfo(output).Length + new FileInfo(plainTextPath).Length;
                break;
            }
            case "All": {
                string htmlPath = Path.ChangeExtension(output, ".html");
                string excelPath = Path.ChangeExtension(output, ".xlsx");
                string emailPath = Path.ChangeExtension(output, ".email.html");
                EventReportHtmlRenderer.Save(report, htmlPath);
                EventReportExcelRenderer.Save(report, excelPath);
                EventEmailPackage email = EventReportEmailRenderer.RenderAsync(report).GetAwaiter().GetResult();
                File.WriteAllText(emailPath, email.Html, new UTF8Encoding(false));
                string plainTextPath = Path.ChangeExtension(output, ".email.txt");
                File.WriteAllText(plainTextPath, email.PlainText, new UTF8Encoding(false));
                bytes = new[] { htmlPath, excelPath, emailPath, plainTextPath }.Sum(static path => new FileInfo(path).Length);
                break;
            }
            default:
                throw new InvalidOperationException($"Unsupported report format '{options.ReportFormat}'.");
        }
        return new ExportMeasurement(output, report.Rows.Count, bytes, null);
    }

    private static async Task<IReadOnlyList<EventTypeRecord>> ReadTypedFileAsync(BenchmarkOptions options) {
        EventType[] types = options.Types.Length > 0
            ? options.Types
            : EventTypeCatalog.GetDefinitions().Where(static definition => !definition.IsComposite)
                .Select(static definition => definition.Type).ToArray();
        var query = new EventTypeQuery(types) {
            Paths = new[] { options.Path },
            MaxEvents = options.MaxEvents,
            Oldest = true,
            ReadMode = EventReadMode.StructuredDataAndMessage,
            MessageCulture = options.MessageCulture
        };
        var result = new List<EventTypeRecord>();
        await foreach (EventTypeRecord record in EventTypeEngine.ReadAsync(query)) {
            result.Add(record);
        }
        return result;
    }

    private static ExportMeasurement RunEventViewerXExport(BenchmarkOptions options) {
        var query = new EventLogFileQuery(options.Path) {
            MaxEvents = options.MaxEvents,
            Oldest = true,
            ReadMode = options.ReadMode,
            MessageCulture = options.MessageCulture
        };
        EventExportResult result = EventLogExporter.ExportFile(
            query,
            options.OutputPath!,
            options.OutputFormat!.Value,
            cancellationToken: default,
            computeSha256: false);
        return new ExportMeasurement(
            result.Path,
            result.EventCount,
            result.Bytes,
            result.Sha256);
    }

    private static void RunDotNet(BenchmarkOptions options, EventAccumulator accumulator) {
        var query = new EventLogQuery(options.Path, PathType.FilePath, "*") {
            ReverseDirection = false,
            TolerateQueryErrors = false
        };

        StreamWriter? csvWriter = null;
        if (options.OutputPath is not null) {
            string? outputDirectory = Path.GetDirectoryName(options.OutputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory)) {
                Directory.CreateDirectory(outputDirectory);
            }

            csvWriter = new StreamWriter(options.OutputPath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) {
                NewLine = "\r\n"
            };
            csvWriter.WriteLine("\"TimeCreated\",\"RecordId\",\"Id\",\"ProviderName\",\"MachineName\"");
        }

        using var reader = new EventLogReader(query);
        try {
            while (options.MaxEvents == 0 || accumulator.Count < options.MaxEvents) {
                using EventRecord? record = reader.ReadEvent();
                if (record is null) {
                    break;
                }

                accumulator.Add(record, options.ReadMode);
                if (csvWriter is not null) {
                    WriteCsvRow(csvWriter, record);
                }
            }
        } finally {
            csvWriter?.Dispose();
        }
    }

    private static void WriteCsvRow(TextWriter writer, EventRecord record) {
        WriteCsvField(writer, record.TimeCreated);
        writer.Write(',');
        WriteCsvField(writer, record.RecordId);
        writer.Write(',');
        WriteCsvField(writer, record.Id);
        writer.Write(',');
        WriteCsvField(writer, record.ProviderName);
        writer.Write(',');
        WriteCsvField(writer, record.MachineName);
        writer.WriteLine();
    }

    private static void WriteCsvField(TextWriter writer, object? value) {
        string text = Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;
        writer.Write('"');
        foreach (char character in text) {
            if (character == '"') {
                writer.Write('"');
            }
            writer.Write(character);
        }
        writer.Write('"');
    }

    private static void RunPropertySelector(BenchmarkOptions options, EventAccumulator accumulator) {
        var query = new EventLogQuery(options.Path, PathType.FilePath, "*") {
            ReverseDirection = false,
            TolerateQueryErrors = false
        };

        using var selector = new EventLogPropertySelector(MetadataPropertyPaths);
        using var reader = new EventLogReader(query);
        while (options.MaxEvents == 0 || accumulator.Count < options.MaxEvents) {
            using EventRecord? record = reader.ReadEvent();
            if (record is null) {
                break;
            }
            if (record is not EventLogRecord logRecord) {
                throw new InvalidOperationException("Property selection requires EventLogRecord instances.");
            }

            accumulator.AddSelected(logRecord.GetPropertyValues(selector));
        }
    }
}
