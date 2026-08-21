namespace EventLogParsing.BenchmarkHost;

internal sealed record ExportMeasurement(
    string Path,
    long EventCount,
    long Bytes,
    string? Sha256);
