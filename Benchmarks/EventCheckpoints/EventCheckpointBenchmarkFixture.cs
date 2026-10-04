using System.Diagnostics;
using DBAClientX;
using EventViewerX.Storage;

namespace EventViewerX.Benchmarks;

/// <summary>Measures checkpoint reads with retained and newly initialized store instances.</summary>
public sealed class EventCheckpointBenchmarkFixture : IDisposable {
    private readonly string root;
    private readonly EventStore store;
    private readonly int count;

    /// <summary>Creates a task-owned database; fixture insertion is outside the measured operation.</summary>
    public EventCheckpointBenchmarkFixture(int count, string workRoot) {
        if (count <= 0) {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        this.count = count;
        root = Path.Combine(workRoot, "checkpoint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        store = new EventStore(Path.Combine(root, "events.db"));
        store.Initialize();
        using var sqlite = new SQLite();
        using SQLiteSession session = sqlite.OpenSession(store.Path);
        session.RunInTransaction(transaction => {
            for (int index = 1; index <= count; index++) {
                transaction.ExecuteNonQuery(@"INSERT INTO evx_checkpoints
                    (consumer, computer, container, record_id, bookmark_xml, updated_utc)
                    VALUES ($consumer, $computer, $container, $record, NULL, $updated);",
                    new Dictionary<string, object?> {
                        ["$consumer"] = "Überwachung",
                        ["$computer"] = "München-" + index,
                        ["$container"] = "ForwardedEvents",
                        ["$record"] = index,
                        ["$updated"] = "2026-10-04T12:00:00.0000000Z"
                    });
            }
        });
    }

    /// <summary>Reads the last checkpoint ten times through an initialized store.</summary>
    public CheckpointBenchmarkResult ReadRetained() => Read(reopen: false);

    /// <summary>Reads the last checkpoint ten times through newly initialized store instances.</summary>
    public CheckpointBenchmarkResult ReadReopened() => Read(reopen: true);

    private CheckpointBenchmarkResult Read(bool reopen) {
        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        var timer = Stopwatch.StartNew();
        long checksum = 0;
        for (int index = 0; index < 10; index++) {
            EventStore current = reopen ? new EventStore(store.Path) : store;
            EventStoreCheckpoint checkpoint = current.GetCheckpointAsync(
                "ÜBERWACHUNG", "MÜNCHEN-" + count, "forwardedevents").GetAwaiter().GetResult()
                ?? throw new InvalidOperationException("Unicode checkpoint identity was lost.");
            checksum += checkpoint.RecordId!.Value;
        }
        timer.Stop();
        return new CheckpointBenchmarkResult(checksum, timer.Elapsed.TotalMilliseconds,
            GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore);
    }

    /// <summary>Removes only the fixture-owned database directory after all sessions close.</summary>
    public void Dispose() {
        if (Directory.Exists(root)) {
            Directory.Delete(root, recursive: true);
        }
    }
}

/// <summary>Identity and cost evidence for ten checkpoint reads.</summary>
public sealed record CheckpointBenchmarkResult(long Checksum, double LookupMs, long AllocatedBytes);
