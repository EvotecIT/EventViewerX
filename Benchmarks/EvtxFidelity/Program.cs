using System.Diagnostics;
using System.Text.Json;
using EventViewerX;
using EventViewerX.Evtx;

if (args.Length == 0 || !File.Exists(args[0])) {
    Console.Error.WriteLine("Usage: EventViewerX.EvtxFidelity <fixture.evtx> [maximum-events]");
    return 64;
}

string path = Path.GetFullPath(args[0]);
long maximum = args.Length > 1 ? long.Parse(args[1]) : 0;
var diagnostics = new List<SavedEventReadDiagnostic>();
Measurement portable = Measure(() => EventLogEngine.ReadFile(new EventLogFileQuery(path) {
    Oldest = true,
    ReadMode = EventReadMode.StructuredData,
    MaxEvents = maximum,
    SavedEventReader = new EvtxSavedEventReader(),
    SavedEventDiagnosticHandler = diagnostics.Add
}).ToArray());
string? evtxDumpPath = args.Length > 2
    ? args[2]
    : Environment.GetEnvironmentVariable("EVENTVIEWERX_EVTX_DUMP");
var commandDiagnostics = new List<SavedEventReadDiagnostic>();
Measurement? command = string.IsNullOrWhiteSpace(evtxDumpPath)
    ? null
    : Measure(() => EventLogEngine.ReadFile(new EventLogFileQuery(path) {
        Oldest = true,
        ReadMode = EventReadMode.StructuredData,
        MaxEvents = maximum,
        SavedEventReader = new EvtxDumpSavedEventReader(evtxDumpPath),
        SavedEventDiagnosticHandler = commandDiagnostics.Add
    }).ToArray());

Measurement? windows = null;
Fidelity? fidelity = null;
Fidelity? commandFidelity = null;
string? windowsError = null;
if (OperatingSystem.IsWindows()) {
    try {
        windows = Measure(() => EventLogEngine.ReadFile(new EventLogFileQuery(path) {
            Oldest = true,
            ReadMode = EventReadMode.StructuredData,
            MaxEvents = maximum
        }).ToArray());
        fidelity = Compare(windows.Events, portable.Events);
        commandFidelity = command == null ? null : Compare(windows.Events, command.Events);
    } catch (Exception exception) {
        windowsError = exception.GetType().Name + ": " + exception.Message;
    }
}

var output = new {
    Path = path,
    FileBytes = new FileInfo(path).Length,
    Portable = portable.ToSummary(),
    EvtxDump = command?.ToSummary(),
    Windows = windows?.ToSummary(),
    WindowsError = windowsError,
    Fidelity = fidelity,
    EvtxDumpFidelity = commandFidelity,
    Diagnostics = diagnostics.Select(static item => new {
        item.Code,
        Severity = item.Severity.ToString(),
        item.Recovered,
        item.FileOffset,
        item.Message
    }),
    EvtxDumpDiagnostics = commandDiagnostics.Select(static item => new {
        item.Code,
        Severity = item.Severity.ToString(),
        item.Recovered,
        item.FileOffset,
        item.Message
    })
};
Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
if (portable.Events.Length == 0) {
    return 2;
}
if (fidelity != null && (fidelity.IdentityMatchRatio < 0.99 || fidelity.MissingPortableRecords > 0)) {
    return 3;
}
if (commandFidelity != null &&
    (commandFidelity.IdentityMatchRatio < 0.99 || commandFidelity.MissingPortableRecords > 0)) {
    return 4;
}
return 0;

static Measurement Measure(Func<EventObject[]> action) {
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    long before = GC.GetTotalAllocatedBytes(precise: true);
    var stopwatch = Stopwatch.StartNew();
    EventObject[] events = action();
    stopwatch.Stop();
    long allocated = GC.GetTotalAllocatedBytes(precise: true) - before;
    return new Measurement(events, stopwatch.Elapsed, allocated);
}

static Fidelity Compare(EventObject[] expected, EventObject[] actual) {
    Dictionary<long, EventObject> expectedById = expected
        .Where(static item => item.RecordId.HasValue)
        .GroupBy(static item => item.RecordId!.Value)
        .ToDictionary(static group => group.Key, static group => group.First());
    int compared = 0;
    int identityMatches = 0;
    int eventIdMatches = 0;
    int providerMatches = 0;
    int channelMatches = 0;
    int computerMatches = 0;
    int timestampMatches = 0;
    int timestampExactMatches = 0;
    foreach (EventObject item in actual.Where(static item => item.RecordId.HasValue)) {
        if (!expectedById.TryGetValue(item.RecordId!.Value, out EventObject? source)) {
            continue;
        }
        compared++;
        bool eventId = source.Id == item.Id;
        bool provider = string.Equals(source.ProviderName, item.ProviderName, StringComparison.Ordinal);
        bool channel = string.Equals(source.OriginalLogName, item.OriginalLogName, StringComparison.Ordinal);
        bool computer = string.Equals(source.SourceComputer, item.SourceComputer, StringComparison.OrdinalIgnoreCase);
        long timestampDifference = Math.Abs(
            source.TimeCreated.ToUniversalTime().Ticks - item.TimeCreated.ToUniversalTime().Ticks);
        bool timestampExact = timestampDifference == 0;
        bool timestamp = timestampDifference <= 10;
        eventIdMatches += eventId ? 1 : 0;
        providerMatches += provider ? 1 : 0;
        channelMatches += channel ? 1 : 0;
        computerMatches += computer ? 1 : 0;
        timestampMatches += timestamp ? 1 : 0;
        timestampExactMatches += timestampExact ? 1 : 0;
        if (eventId && provider && channel && computer && timestamp) {
            identityMatches++;
        }
    }
    return new Fidelity(
        expected.Length,
        actual.Length,
        compared,
        identityMatches,
        compared == 0 ? 0 : (double)identityMatches / compared,
        compared == 0 ? 0 : (double)eventIdMatches / compared,
        compared == 0 ? 0 : (double)providerMatches / compared,
        compared == 0 ? 0 : (double)channelMatches / compared,
        compared == 0 ? 0 : (double)computerMatches / compared,
        compared == 0 ? 0 : (double)timestampMatches / compared,
        compared == 0 ? 0 : (double)timestampExactMatches / compared,
        Math.Max(0, expected.Length - actual.Length),
        Math.Max(0, actual.Length - expected.Length));
}

internal sealed record Measurement(EventObject[] Events, TimeSpan Elapsed, long AllocatedBytes) {
    internal object ToSummary() => new {
        Count = Events.Length,
        ElapsedMilliseconds = Elapsed.TotalMilliseconds,
        AllocatedBytes,
        EventsPerSecond = Elapsed.TotalSeconds == 0 ? 0 : Events.Length / Elapsed.TotalSeconds,
        BytesPerEvent = Events.Length == 0 ? 0 : (double)AllocatedBytes / Events.Length
    };
}

internal sealed record Fidelity(
    int WindowsRecords,
    int PortableRecords,
    int ComparedRecords,
    int IdentityMatches,
    double IdentityMatchRatio,
    double EventIdMatchRatio,
    double ProviderMatchRatio,
    double ChannelMatchRatio,
    double ComputerMatchRatio,
    double TimestampMatchRatio,
    double TimestampExactMatchRatio,
    int MissingPortableRecords,
    int ExtraPortableRecords);
