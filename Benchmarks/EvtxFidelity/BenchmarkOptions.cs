using System.Globalization;
using EventViewerX;

internal sealed class BenchmarkOptions {
    internal const string Usage =
        "Usage: EventViewerX.EvtxFidelity <fixture.evtx> [maximum-events] [evtx-dump] " +
        "[--read-mode <mode>] [--warmup <count>] [--iterations <count>] " +
        "[--min-events-per-second <value>] [--max-bytes-per-event <value>] " +
        "[--minimum-identity-ratio <value>] [--minimum-exact-timestamp-ratio <value>] " +
        "[--output <path>]";

    internal required string Path { get; init; }
    internal long MaximumEvents { get; init; }
    internal string? EvtxDumpPath { get; init; }
    internal EventReadMode ReadMode { get; init; } = EventReadMode.StructuredData;
    internal int WarmupIterations { get; init; }
    internal int Iterations { get; init; } = 1;
    internal double? MinimumEventsPerSecond { get; init; }
    internal double? MaximumBytesPerEvent { get; init; }
    internal double MinimumIdentityMatchRatio { get; init; } = 0.99;
    internal double MinimumExactTimestampRatio { get; init; }
    internal string? OutputPath { get; init; }

    internal static BenchmarkOptions Parse(string[] args) {
        if (args.Length == 0 || !File.Exists(args[0])) {
            throw new ArgumentException("A readable EVTX fixture path is required.");
        }

        string path = System.IO.Path.GetFullPath(args[0]);
        long maximumEvents = 0;
        string? evtxDumpPath = null;
        EventReadMode readMode = EventReadMode.StructuredData;
        int warmupIterations = 0;
        int iterations = 1;
        double? minimumEventsPerSecond = null;
        double? maximumBytesPerEvent = null;
        double minimumIdentityMatchRatio = 0.99;
        double minimumExactTimestampRatio = 0;
        string? outputPath = null;
        int index = 1;

        if (index < args.Length && !IsOption(args[index])) {
            maximumEvents = ParseLong(args[index++], "maximum-events", minimum: 0);
        }
        if (index < args.Length && !IsOption(args[index])) {
            evtxDumpPath = args[index++];
        }

        while (index < args.Length) {
            string option = args[index++];
            string value = index < args.Length
                ? args[index++]
                : throw new ArgumentException($"Option '{option}' requires a value.");
            switch (option.ToLowerInvariant()) {
                case "--read-mode":
                    if (!Enum.TryParse(value, ignoreCase: true, out readMode) ||
                        !Enum.IsDefined(typeof(EventReadMode), readMode)) {
                        throw new ArgumentException($"Unsupported read mode '{value}'.");
                    }
                    break;
                case "--warmup":
                    warmupIterations = checked((int)ParseLong(value, "warmup", minimum: 0, maximum: 20));
                    break;
                case "--iterations":
                    iterations = checked((int)ParseLong(value, "iterations", minimum: 1, maximum: 20));
                    break;
                case "--min-events-per-second":
                    minimumEventsPerSecond = ParseDouble(value, "min-events-per-second", minimumExclusive: 0);
                    break;
                case "--max-bytes-per-event":
                    maximumBytesPerEvent = ParseDouble(value, "max-bytes-per-event", minimumExclusive: 0);
                    break;
                case "--minimum-identity-ratio":
                    minimumIdentityMatchRatio = ParseDouble(
                        value,
                        "minimum-identity-ratio",
                        minimumInclusive: 0,
                        maximumInclusive: 1);
                    break;
                case "--minimum-exact-timestamp-ratio":
                    minimumExactTimestampRatio = ParseDouble(
                        value,
                        "minimum-exact-timestamp-ratio",
                        minimumInclusive: 0,
                        maximumInclusive: 1);
                    break;
                case "--output":
                    outputPath = value;
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{option}'.");
            }
        }

        return new BenchmarkOptions {
            Path = path,
            MaximumEvents = maximumEvents,
            EvtxDumpPath = evtxDumpPath ?? Environment.GetEnvironmentVariable("EVENTVIEWERX_EVTX_DUMP"),
            ReadMode = readMode,
            WarmupIterations = warmupIterations,
            Iterations = iterations,
            MinimumEventsPerSecond = minimumEventsPerSecond,
            MaximumBytesPerEvent = maximumBytesPerEvent,
            MinimumIdentityMatchRatio = minimumIdentityMatchRatio,
            MinimumExactTimestampRatio = minimumExactTimestampRatio,
            OutputPath = outputPath
        };
    }

    private static bool IsOption(string value) => value.StartsWith("--", StringComparison.Ordinal);

    private static long ParseLong(
        string value,
        string name,
        long minimum,
        long maximum = long.MaxValue) {

        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result) ||
            result < minimum || result > maximum) {
            throw new ArgumentException(
                $"Option '{name}' must be between {minimum.ToString(CultureInfo.InvariantCulture)} and " +
                $"{maximum.ToString(CultureInfo.InvariantCulture)}.");
        }
        return result;
    }

    private static double ParseDouble(
        string value,
        string name,
        double? minimumExclusive = null,
        double? minimumInclusive = null,
        double? maximumInclusive = null) {

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) ||
            !double.IsFinite(result) ||
            (minimumExclusive.HasValue && result <= minimumExclusive.Value) ||
            (minimumInclusive.HasValue && result < minimumInclusive.Value) ||
            (maximumInclusive.HasValue && result > maximumInclusive.Value)) {
            throw new ArgumentException($"Option '{name}' has an invalid numeric value '{value}'.");
        }
        return result;
    }
}
