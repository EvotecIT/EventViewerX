namespace EventLogParsing.BenchmarkHost;

internal sealed class BenchmarkOptions {
    public required string Engine { get; init; }

    public required string Path { get; init; }

    public required EventViewerX.EventReadMode ReadMode { get; init; }

    public required string ResultPath { get; init; }

    public string? OutputPath { get; init; }

    public EventViewerX.EventExportFormat? OutputFormat { get; init; }

    public required System.Globalization.CultureInfo MessageCulture { get; init; }

    public int MaxEvents { get; init; }

    public EventViewerX.EventType[] Types { get; init; } = Array.Empty<EventViewerX.EventType>();

    public string? ReportFormat { get; init; }

    public static BenchmarkOptions Parse(string[] args) {
        if (args is null) {
            throw new ArgumentNullException(nameof(args));
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++) {
            string argument = args[i];
            if (!argument.StartsWith("--", StringComparison.Ordinal)) {
                throw new ArgumentException($"Unexpected argument '{argument}'.");
            }
            if (i + 1 >= args.Length) {
                throw new ArgumentException($"Argument '{argument}' requires a value.");
            }

            values[argument[2..]] = args[++i];
        }

        string engine = GetRequired(values, "engine");
        if (!string.Equals(engine, "dotnet", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(engine, "propertyselector", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(engine, "eventviewerx", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(engine, "eventviewerxexport", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(engine, "eventviewerxtyped", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(engine, "eventviewerxreport", StringComparison.OrdinalIgnoreCase)) {
            throw new ArgumentException("--engine must be dotnet, propertyselector, eventviewerx, eventviewerxexport, eventviewerxtyped, or eventviewerxreport.");
        }

        string path = System.IO.Path.GetFullPath(GetRequired(values, "path"));
        if (!File.Exists(path)) {
            throw new FileNotFoundException($"The EVTX fixture '{path}' does not exist.", path);
        }

        if (!Enum.TryParse(GetRequired(values, "mode"), ignoreCase: true, out EventViewerX.EventReadMode readMode)) {
            throw new ArgumentException("--mode must be Metadata, Message, StructuredData, StructuredDataAndMessage, or Full.");
        }

        int maxEvents = 0;
        if (values.TryGetValue("max-events", out string? maxText) &&
            (!int.TryParse(maxText, out maxEvents) || maxEvents < 0)) {
            throw new ArgumentException("--max-events must be a non-negative integer.");
        }

        string? outputPath = values.TryGetValue("output-path", out string? suppliedOutputPath)
            ? System.IO.Path.GetFullPath(suppliedOutputPath)
            : null;
        EventViewerX.EventExportFormat? outputFormat = null;
        if (values.TryGetValue("format", out string? formatText)) {
            if (!Enum.TryParse(
                    formatText,
                    ignoreCase: true,
                    out EventViewerX.EventExportFormat parsedFormat)) {
                throw new ArgumentException("--format must be Csv, JsonLines, or Xml.");
            }
            outputFormat = parsedFormat;
        }
        if (string.Equals(engine, "eventviewerxexport", StringComparison.OrdinalIgnoreCase) &&
            (outputPath == null || outputFormat == null)) {
            throw new ArgumentException(
                "The eventviewerxexport engine requires --output-path and --format.");
        }
        EventViewerX.EventType[] types = values.TryGetValue("type", out string? typeText)
            ? typeText.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(static value => Enum.TryParse(value.Trim(), true, out EventViewerX.EventType type) && Enum.IsDefined(type)
                    ? type
                    : throw new ArgumentException($"Unknown event type '{value}'."))
                .Distinct()
                .ToArray()
            : Array.Empty<EventViewerX.EventType>();
        string? reportFormat = values.TryGetValue("report-format", out string? suppliedReportFormat)
            ? suppliedReportFormat.Trim()
            : null;
        if (string.Equals(engine, "eventviewerxreport", StringComparison.OrdinalIgnoreCase) &&
            (outputPath == null || reportFormat is not ("Html" or "Excel" or "Email" or "All"))) {
            throw new ArgumentException("The eventviewerxreport engine requires --output-path and --report-format Html, Excel, Email, or All.");
        }

        string cultureName = values.TryGetValue("culture", out string? suppliedCulture)
            ? suppliedCulture
            : "en-US";
        return new BenchmarkOptions {
            Engine = engine,
            Path = path,
            ReadMode = readMode,
            ResultPath = System.IO.Path.GetFullPath(GetRequired(values, "result")),
            OutputPath = outputPath,
            OutputFormat = outputFormat,
            MessageCulture = System.Globalization.CultureInfo.GetCultureInfo(cultureName),
            MaxEvents = maxEvents,
            Types = types,
            ReportFormat = reportFormat
        };
    }

    private static string GetRequired(IReadOnlyDictionary<string, string> values, string name) {
        if (!values.TryGetValue(name, out string? value) || string.IsNullOrWhiteSpace(value)) {
            throw new ArgumentException($"--{name} is required.");
        }

        return value.Trim();
    }
}
