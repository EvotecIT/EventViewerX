using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using EventViewerX;
using EventViewerX.Evtx;

BenchmarkOptions options;
try {
    options = BenchmarkOptions.Parse(args);
} catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException) {
    Console.Error.WriteLine(exception.Message);
    Console.Error.WriteLine(BenchmarkOptions.Usage);
    return 64;
}

var diagnostics = new List<SavedEventReadDiagnostic>();
var commandDiagnostics = new List<SavedEventReadDiagnostic>();
var portableMeasurements = new List<MeasurementSummary>(options.Iterations);
var commandMeasurements = new List<MeasurementSummary>(options.Iterations);
var windowsMeasurements = new List<MeasurementSummary>(options.Iterations);
var fidelities = new List<Fidelity>(options.Iterations);
var commandFidelities = new List<Fidelity>(options.Iterations);
string? windowsError = null;

Func<Action<SavedEventReadDiagnostic>?, EventObject[]> portableAction = diagnosticHandler =>
    Read(new EvtxSavedEventReader(), diagnosticHandler);
Func<Action<SavedEventReadDiagnostic>?, EventObject[]>? commandAction =
    string.IsNullOrWhiteSpace(options.EvtxDumpPath)
        ? null
        : diagnosticHandler => Read(
            new EvtxDumpSavedEventReader(options.EvtxDumpPath!),
            diagnosticHandler);

for (int iteration = 0; iteration < options.WarmupIterations; iteration++) {
    _ = portableAction(null);
    if (commandAction != null) {
        _ = commandAction(null);
    }
    if (OperatingSystem.IsWindows()) {
        try {
            _ = ReadWindows();
        } catch (Exception exception) {
            windowsError ??= exception.GetType().Name + ": " + exception.Message;
        }
    }
}

for (int iteration = 0; iteration < options.Iterations; iteration++) {
    Measurement? windows = null;
    if (OperatingSystem.IsWindows() && iteration % 2 == 1) {
        windows = TryMeasureWindows();
    }
    Measurement portable = MeasurementRunner.Measure(() => portableAction(
        iteration == 0 ? diagnostics.Add : null));
    portableMeasurements.Add(portable.Summary);

    Measurement? command = commandAction == null
        ? null
        : MeasurementRunner.Measure(() => commandAction(
            iteration == 0 ? commandDiagnostics.Add : null));
    if (command != null) {
        commandMeasurements.Add(command.Summary);
    }

    if (OperatingSystem.IsWindows()) {
        windows ??= TryMeasureWindows();
        if (windows != null) {
            windowsMeasurements.Add(windows.Summary);
            fidelities.Add(FidelityComparer.Compare(windows.Events, portable.Events));
            if (command != null) {
                commandFidelities.Add(FidelityComparer.Compare(windows.Events, command.Events));
            }
        }
    }
}

MeasurementAggregate portableAggregate = MeasurementAggregate.Create(portableMeasurements);
MeasurementAggregate? commandAggregate = commandMeasurements.Count == 0
    ? null
    : MeasurementAggregate.Create(commandMeasurements);
MeasurementAggregate? windowsAggregate = windowsMeasurements.Count == 0
    ? null
    : MeasurementAggregate.Create(windowsMeasurements);
FidelityAggregate? fidelityAggregate = fidelities.Count == 0
    ? null
    : FidelityAggregate.Create(fidelities);
FidelityAggregate? commandFidelityAggregate = commandFidelities.Count == 0
    ? null
    : FidelityAggregate.Create(commandFidelities);
PerformanceBudgetResult budget = PerformanceBudgetEvaluator.Evaluate(
    options,
    portableAggregate.Median,
    fidelityAggregate);

var fixture = new FileInfo(options.Path);
var output = new {
    options.Path,
    FileBytes = fixture.Length,
    FileSha256 = ComputeSha256(options.Path),
    options.MaximumEvents,
    ReadMode = options.ReadMode.ToString(),
    options.WarmupIterations,
    options.Iterations,
    options.MinimumIdentityMatchRatio,
    options.MinimumExactTimestampRatio,
    Runtime = RuntimeInformation.FrameworkDescription,
    OperatingSystem = RuntimeInformation.OSDescription,
    Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
    EventViewerXEvtxVersion = AssemblyVersion(typeof(EvtxSavedEventReader).Assembly),
    ParserDependencyVersion = AssemblyVersion(typeof(evtx.EventLog).Assembly),
    Portable = portableAggregate,
    EvtxDump = commandAggregate,
    Windows = windowsAggregate,
    WindowsError = windowsError,
    Fidelity = fidelityAggregate,
    EvtxDumpFidelity = commandFidelityAggregate,
    PerformanceBudget = budget,
    Diagnostics = ProjectDiagnostics(diagnostics),
    EvtxDumpDiagnostics = ProjectDiagnostics(commandDiagnostics)
};
var serializerOptions = new JsonSerializerOptions { WriteIndented = true };
string json = JsonSerializer.Serialize(output, serializerOptions);
Console.WriteLine(json);
if (!string.IsNullOrWhiteSpace(options.OutputPath)) {
    string outputPath = Path.GetFullPath(options.OutputPath!);
    string? directory = Path.GetDirectoryName(outputPath);
    if (!string.IsNullOrEmpty(directory)) {
        Directory.CreateDirectory(directory);
    }
    File.WriteAllText(outputPath, json + Environment.NewLine);
}

if (portableMeasurements.Any(static measurement => measurement.Count == 0)) {
    return 2;
}
if (options.MinimumExactTimestampRatio > 0 && fidelityAggregate == null) {
    return 6;
}
if (fidelityAggregate != null &&
    (fidelityAggregate.MinimumIdentityMatchRatio < options.MinimumIdentityMatchRatio ||
     fidelityAggregate.MinimumExactTimestampMatchRatio < options.MinimumExactTimestampRatio ||
     fidelityAggregate.MaximumMissingPortableRecords > 0 ||
     fidelityAggregate.MaximumExtraPortableRecords > 0)) {
    return 3;
}
if (commandFidelityAggregate != null &&
    (commandFidelityAggregate.MinimumIdentityMatchRatio < options.MinimumIdentityMatchRatio ||
     commandFidelityAggregate.MinimumExactTimestampMatchRatio < options.MinimumExactTimestampRatio ||
     commandFidelityAggregate.MaximumMissingPortableRecords > 0 ||
     commandFidelityAggregate.MaximumExtraPortableRecords > 0)) {
    return 4;
}
return budget.Passed ? 0 : 5;

EventObject[] Read(ISavedEventReader reader, Action<SavedEventReadDiagnostic>? diagnosticHandler) =>
    EventLogEngine.ReadFile(new EventLogFileQuery(options.Path) {
        Oldest = true,
        ReadMode = options.ReadMode,
        MaxEvents = options.MaximumEvents,
        SavedEventReader = reader,
        SavedEventDiagnosticHandler = diagnosticHandler
    }).ToArray();

EventObject[] ReadWindows() => EventLogEngine.ReadFile(new EventLogFileQuery(options.Path) {
    Oldest = true,
    ReadMode = options.ReadMode,
    MaxEvents = options.MaximumEvents
}).ToArray();

Measurement? TryMeasureWindows() {
    try {
        return MeasurementRunner.Measure(ReadWindows);
    } catch (Exception exception) {
        windowsError ??= exception.GetType().Name + ": " + exception.Message;
        return null;
    }
}

static string ComputeSha256(string path) {
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream));
}

static string AssemblyVersion(Assembly assembly) =>
    assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
    assembly.GetName().Version?.ToString() ??
    string.Empty;

static IEnumerable<object> ProjectDiagnostics(IEnumerable<SavedEventReadDiagnostic> diagnostics) =>
    diagnostics.Select(static item => new {
        item.Code,
        Severity = item.Severity.ToString(),
        item.Recovered,
        item.AffectsCompleteness,
        item.FileOffset,
        item.Message
    });
