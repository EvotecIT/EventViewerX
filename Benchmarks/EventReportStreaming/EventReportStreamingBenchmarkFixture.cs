using System.Diagnostics;
using EventViewerX.Reporting;

namespace EventViewerX.Benchmarks;

/// <summary>Measures equivalent snapshot and streaming queries over a lazy saved-event source.</summary>
public sealed class EventReportStreamingBenchmarkFixture : ISavedEventReader, IDisposable {
    private readonly string path;
    private readonly int count;
    private int readCount;

    /// <summary>Creates a task-owned source marker without preallocating event rows.</summary>
    public EventReportStreamingBenchmarkFixture(int count, string workRoot) {
        if (count <= 0) {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        this.count = count;
        Directory.CreateDirectory(workRoot);
        path = Path.Combine(workRoot, $"stream-{Guid.NewGuid():N}.evtx");
        File.WriteAllBytes(path, new byte[] { 1 });
    }

    /// <summary>Runs the existing materialized report API and consumes its rows.</summary>
    public StreamingBenchmarkResult RunSnapshot() => Run(stream: false);

    /// <summary>Runs the row streaming API and consumes rows without retaining them.</summary>
    public StreamingBenchmarkResult RunStream() => Run(stream: true);

    private StreamingBenchmarkResult Run(bool stream) {
        readCount = 0;
        var request = EventReportRequest.ForFiles(path);
        request.SavedEventReader = this;
        request.ReadMode = EventReadMode.StructuredData;
        request.Oldest = true;
        long memoryBefore = GC.GetTotalMemory(forceFullCollection: true);
        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        var timer = Stopwatch.StartNew();
        long checksum = 0;
        long rows = 0;
        double firstRowMs = 0;
        int readAtFirstRow = 0;
        void Consume(EventReportRow row) {
            if (rows == 0) {
                firstRowMs = timer.Elapsed.TotalMilliseconds;
                readAtFirstRow = readCount;
            }
            if (row.Values["TargetUserName"] is not string account || account != "alice" ||
                string.IsNullOrEmpty(row.ObservationIdentity)) {
                throw new InvalidOperationException("The query lost payload or source identity.");
            }
            checksum += row.RecordId!.Value;
            rows++;
        }
        EventReport? report = null;
        EventReportSummary summary;
        if (stream) {
            summary = EventReportEngine.StreamRowsAsync(request, (row, _, _) => {
                Consume(row);
                return Task.CompletedTask;
            }).GetAwaiter().GetResult();
        } else {
            report = EventReportEngine.QueryAsync(request).GetAwaiter().GetResult();
            foreach (EventReportRow row in report.Rows) {
                Consume(row);
            }
            summary = EventReportSummary.Create(report);
        }
        timer.Stop();
        long allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
        long retained = Math.Max(0, GC.GetTotalMemory(forceFullCollection: true) - memoryBefore);
        GC.KeepAlive(report); // Snapshot rows must remain rooted while measuring retained memory.
        return new StreamingBenchmarkResult(rows, checksum, summary.IsComplete,
            firstRowMs, timer.Elapsed.TotalMilliseconds, readAtFirstRow, allocated, retained);
    }

    /// <summary>Generates the ordered input lazily through the saved-reader boundary.</summary>
    public IEnumerable<SavedEventRecord> Read(EventLogFileQuery query,
        Action<SavedEventReadDiagnostic>? diagnosticHandler = null, CancellationToken cancellationToken = default) {
        for (int index = 0; index < count; index++) {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref readCount);
            yield return new SavedEventRecord {
                ProviderName = "BenchmarkAudit", EventId = 4624, RecordId = index + 1,
                Channel = "Security", Computer = "server01",
                TimeCreatedUtc = new DateTime(2026, 10, 4, 10, 0, 0, DateTimeKind.Utc).AddSeconds(index),
                Data = new Dictionary<string, string> { ["TargetUserName"] = "alice" }
            };
        }
    }

    /// <summary>Removes the fixture-owned source marker after validation.</summary>
    public void Dispose() => File.Delete(path);
}

/// <summary>Correctness and memory evidence from one normalized query.</summary>
public sealed record StreamingBenchmarkResult(long Rows, long Checksum, bool IsComplete,
    double FirstRowMs, double QueryMs, int ReadAtFirstRow, long AllocatedBytes, long RetainedManagedBytes);
