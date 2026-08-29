using EventViewerX.Storage;
using EventViewerX.Reporting;
using Xunit;

namespace EventViewerX.Tests;

public sealed partial class TestEventStore {
    [Fact]
    public async Task BackupIsIntegrityCheckedAndRestoreReplacesLaterState() {
        string path = CreateStorePath();
        string backup = CreateStorePath();
        try {
            var store = new EventStore(path);
            await store.WriteAsync(CreateReport((
                new DateTime(2026, 8, 28, 10, 0, 0, DateTimeKind.Utc),
                1,
                "alice")));

            EventStoreBackupResult artifact = await store.BackupAsync(backup);
            await store.WriteAsync(CreateReport((
                new DateTime(2026, 8, 28, 10, 5, 0, DateTimeKind.Utc),
                2,
                "bob")));
            EventStoreIntegrityResult beforeRestore = await store.CheckIntegrityAsync();
            EventStoreIntegrityResult restored = await store.RestoreAsync(backup);
            EventReport report = await store.ReadReportAsync(new EventStoreQuery { Oldest = true });

            Assert.True(beforeRestore.IsHealthy);
            Assert.Equal(2, beforeRestore.EventCount);
            Assert.Equal(64, artifact.Sha256.Length);
            Assert.True(artifact.Bytes > 0);
            Assert.True(restored.IsHealthy);
            Assert.Equal(1, restored.EventCount);
            Assert.Single(report.Rows);
            Assert.Equal("alice", report.Rows[0].Values["User"]);
        } finally {
            DeleteStore(path);
            DeleteStore(backup);
        }
    }

    [Fact]
    public async Task RetentionPrunesEventsAndFindingsIndependentlyAndReportsCompaction() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            await store.WriteAsync(CreateReport(
                (new DateTime(2026, 8, 28, 10, 0, 0, DateTimeKind.Utc), 1, "alice"),
                (new DateTime(2026, 8, 28, 10, 5, 0, DateTimeKind.Utc), 2, "bob")));
            await store.WriteFindingsAsync(new[] {
                CreateDetectionFinding("EVX-RETENTION-OLD", "alice", minute: 0),
                CreateDetectionFinding("EVX-RETENTION-NEW", "bob", minute: 5)
            });

            EventStoreRetentionResult result = await store.ApplyRetentionAsync(
                new EventStoreRetentionPolicy {
                    EventRetention = TimeSpan.FromMinutes(3),
                    FindingRetention = TimeSpan.FromMinutes(3),
                    VacuumAfterPrune = true
                },
                new DateTime(2026, 8, 28, 10, 6, 0, DateTimeKind.Utc));
            EventStoreIntegrityResult integrity = await store.CheckIntegrityAsync();

            Assert.Equal(1, result.DeletedEvents);
            Assert.Equal(1, result.DeletedFindings);
            Assert.True(result.Vacuumed);
            Assert.True(integrity.IsHealthy);
            Assert.Equal(1, integrity.EventCount);
            Assert.Equal(1, integrity.FindingCount);
        } finally {
            DeleteStore(path);
        }
    }
}
