using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
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
ExternalCommandIdentity? commandIdentityBefore;
try {
    commandIdentityBefore = ExternalCommandIdentity.Create(options.EvtxDumpPath);
} catch (Exception exception) when (
    exception is FileNotFoundException or IOException or UnauthorizedAccessException) {
    Console.Error.WriteLine(exception.Message);
    return 66;
}
FixtureIdentity fixtureBefore = ReadFixtureIdentity(options.Path);
var portableMeasurements = new List<MeasurementSummary>(options.Iterations);
var commandMeasurements = new List<MeasurementSummary>(options.Iterations);
var windowsMeasurements = new List<MeasurementSummary>(options.Iterations);
var fidelities = new List<Fidelity>(options.Iterations);
var commandFidelities = new List<Fidelity>(options.Iterations);
string? windowsError = null;

Func<Action<SavedEventReadDiagnostic>?, EventObject[]> portableAction = diagnosticHandler =>
    Read(new EvtxSavedEventReader(), diagnosticHandler);
Func<Action<SavedEventReadDiagnostic>?, EventObject[]>? commandAction =
    commandIdentityBefore?.ResolvedPath == null
        ? null
        : diagnosticHandler => Read(
            new EvtxDumpSavedEventReader(commandIdentityBefore.ResolvedPath),
            diagnosticHandler);
ExternalCommandIdentity? commandIdentityAfter = null;
string? commandIdentityError = null;
bool? commandIdentityStable = commandAction == null ? null : true;

for (int iteration = 0; iteration < options.WarmupIterations; iteration++) {
    _ = portableAction(null);
    if (commandAction != null) {
        _ = commandAction(null);
        CheckCommandIdentity();
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
        CheckCommandIdentity();
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
FixtureIdentity? fixtureAfter = null;
string? fixtureIntegrityError = null;
try {
    fixtureAfter = ReadFixtureIdentity(options.Path);
} catch (Exception exception) when (
    exception is IOException or UnauthorizedAccessException) {
    fixtureIntegrityError = exception.GetType().Name + ": " + exception.Message;
}
bool fixtureStable = fixtureAfter != null && fixtureBefore == fixtureAfter;
CheckCommandIdentity();
bool windowsReferenceIncomplete = OperatingSystem.IsWindows() &&
                                  windowsMeasurements.Count > 0 &&
                                  windowsMeasurements.Count != options.Iterations;
PerformanceBudgetResult budget = PerformanceBudgetEvaluator.Evaluate(
    options,
    portableAggregate.Median,
    fidelityAggregate);

var output = new {
    options.Path,
    FileBytes = fixtureBefore.Bytes,
    FileSha256 = fixtureBefore.Sha256,
    FixtureIntegrity = new {
        Before = fixtureBefore,
        After = fixtureAfter,
        Stable = fixtureStable,
        Error = fixtureIntegrityError
    },
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
    EvtxDumpExecutable = commandIdentityBefore,
    EvtxDumpExecutableIntegrity = new {
        Before = commandIdentityBefore,
        After = commandIdentityAfter,
        Stable = commandIdentityStable,
        Error = commandIdentityError
    },
    Portable = portableAggregate,
    EvtxDump = commandAggregate,
    Windows = windowsAggregate,
    WindowsReferenceComplete = OperatingSystem.IsWindows()
        ? windowsMeasurements.Count == options.Iterations
        : (bool?)null,
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
if (options.OutputPath != null) {
    string outputPath = Path.GetFullPath(options.OutputPath!);
    string? directory = Path.GetDirectoryName(outputPath);
    if (!string.IsNullOrEmpty(directory)) {
        Directory.CreateDirectory(directory);
    }
    using var stream = new FileStream(
        outputPath,
        FileMode.CreateNew,
        FileAccess.Write,
        FileShare.None);
    using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    writer.Write(json);
    writer.Write(Environment.NewLine);
}

if (portableMeasurements.Any(static measurement => measurement.Count == 0)) {
    return 2;
}
if (commandAction != null && commandMeasurements.Any(static measurement => measurement.Count == 0)) {
    return 4;
}
if (!fixtureStable) {
    return 8;
}
if (commandIdentityStable == false) {
    return 9;
}
if (windowsReferenceIncomplete) {
    return 7;
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

void CheckCommandIdentity() {
    if (commandIdentityBefore == null || commandIdentityStable == false) {
        return;
    }
    try {
        commandIdentityAfter = ExternalCommandIdentity.Refresh(commandIdentityBefore);
        commandIdentityStable = commandIdentityAfter == commandIdentityBefore;
    } catch (Exception exception) when (
        exception is FileNotFoundException or IOException or UnauthorizedAccessException) {
        commandIdentityError = exception.GetType().Name + ": " + exception.Message;
        commandIdentityStable = false;
    }
}

static FixtureIdentity ReadFixtureIdentity(string path) {
    using FileStream stream = File.Open(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.ReadWrite | FileShare.Delete);
    long bytes = stream.Length;
    return new FixtureIdentity(bytes, Convert.ToHexString(SHA256.HashData(stream)));
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

internal sealed record FixtureIdentity(long Bytes, string Sha256);
