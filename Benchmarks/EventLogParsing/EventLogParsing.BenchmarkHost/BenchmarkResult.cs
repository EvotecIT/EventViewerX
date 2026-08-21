namespace EventLogParsing.BenchmarkHost;

internal sealed class BenchmarkResult {
    public required string Engine { get; init; }

    public required string ReadMode { get; init; }

    public required string FixturePath { get; init; }

    public required string RuntimeVersion { get; init; }

    public required string ProductVersion { get; init; }

    public long Count { get; init; }

    public long IdSum { get; init; }

    public long RecordIdSum { get; init; }

    public long TimeTicksXor { get; init; }

    public long OrderSignature { get; init; }

    public long? FirstRecordId { get; init; }

    public long? LastRecordId { get; init; }

    public long MetadataTouch { get; init; }

    public long MessageCharacters { get; init; }

    public long XmlCharacters { get; init; }

    public long PropertyCount { get; init; }

    public long StructuredFieldCount { get; init; }

    public long MessageFieldCount { get; init; }

    public long AttachmentBytes { get; init; }

    public long AllocatedBytes { get; init; }

    public long PeakWorkingSetBytes { get; init; }

    public int Gen0Collections { get; init; }

    public int Gen1Collections { get; init; }

    public int Gen2Collections { get; init; }

    public double ElapsedMilliseconds { get; init; }

    public string? OutputPath { get; init; }

    public long OutputBytes { get; init; }

    public string? OutputSha256 { get; init; }
}
