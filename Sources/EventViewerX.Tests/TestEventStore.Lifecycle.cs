using EventViewerX.Storage;
using EventViewerX.Reporting;
using DBAClientX;
using Xunit;

namespace EventViewerX.Tests;

public sealed partial class TestEventStore {
    [Fact]
    public async Task IntegrityValidationRejectsMissingEmptyAndUnrelatedFilesWithoutMutatingThem() {
        string missing = CreateStorePath();
        string empty = CreateStorePath();
        string unrelated = CreateStorePath();
        try {
            File.WriteAllBytes(empty, Array.Empty<byte>());
            new SQLite().ExecuteNonQuery(unrelated, "CREATE TABLE unrelated (value INTEGER NOT NULL);");
            long unrelatedBytes = new FileInfo(unrelated).Length;

            EventStoreIntegrityResult missingResult = await new EventStore(missing).CheckIntegrityAsync();
            EventStoreIntegrityResult emptyResult = await new EventStore(empty).CheckIntegrityAsync();
            EventStoreIntegrityResult unrelatedResult = await new EventStore(unrelated).CheckIntegrityAsync();

            Assert.False(missingResult.IsHealthy);
            Assert.False(File.Exists(missing));
            Assert.False(emptyResult.IsHealthy);
            Assert.Equal(0, new FileInfo(empty).Length);
            Assert.False(unrelatedResult.IsHealthy);
            Assert.Contains(unrelatedResult.Diagnostics, static diagnostic =>
                diagnostic.Contains("evx_store_metadata", StringComparison.Ordinal));
            Assert.Equal(unrelatedBytes, new FileInfo(unrelated).Length);
        } finally {
            DeleteStore(missing);
            DeleteStore(empty);
            DeleteStore(unrelated);
        }
    }

    [Fact]
    public async Task RestoreRejectsAnEmptyBackupWithoutReplacingLiveHistory() {
        string path = CreateStorePath();
        string backup = CreateStorePath();
        try {
            var store = new EventStore(path);
            await store.WriteAsync(CreateReport((
                new DateTime(2026, 8, 28, 10, 0, 0, DateTimeKind.Utc),
                1,
                "alice")));
            File.WriteAllBytes(backup, Array.Empty<byte>());

            await Assert.ThrowsAsync<InvalidDataException>(() => store.RestoreAsync(backup));
            EventReport report = await store.ReadReportAsync(new EventStoreQuery { Oldest = true });

            Assert.Single(report.Rows);
            Assert.Equal("alice", report.Rows[0].Values["User"]);
            Assert.Equal(0, new FileInfo(backup).Length);
        } finally {
            DeleteStore(path);
            DeleteStore(backup);
        }
    }

    [Fact]
    public async Task IntegrityValidationRejectsMissingColumnsAndPrimaryKeyConstraintsReadOnly() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            store.Initialize();
            var sqlite = new SQLite();
            sqlite.ExecuteNonQuery(path, @"
DROP TABLE evx_checkpoints;
CREATE TABLE evx_checkpoints (
    consumer TEXT NOT NULL,
    computer TEXT NOT NULL,
    container TEXT NOT NULL,
    record_id INTEGER NULL,
    bookmark_xml TEXT NULL
);");

            EventStoreIntegrityResult integrity = await store.CheckIntegrityAsync();
            using SQLiteSession verification = sqlite.OpenSession(path);
            IReadOnlyList<string> columns = verification.QueryAsList(
                "PRAGMA table_info(evx_checkpoints);",
                static record => record.GetString(1));

            Assert.False(integrity.IsHealthy);
            Assert.Contains(integrity.Diagnostics, static diagnostic =>
                diagnostic.Contains("updated_utc", StringComparison.Ordinal));
            Assert.Contains(integrity.Diagnostics, static diagnostic =>
                diagnostic.Contains("primary-key contract", StringComparison.Ordinal));
            Assert.DoesNotContain("updated_utc", columns);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task RestoreRejectsBackupMissingSecondaryTablesWithoutReplacingLiveHistory() {
        string path = CreateStorePath();
        string backup = CreateStorePath();
        try {
            var store = new EventStore(path);
            await store.WriteAsync(CreateReport((
                new DateTime(2026, 8, 28, 10, 0, 0, DateTimeKind.Utc),
                1,
                "alice")));
            var incompleteBackup = new EventStore(backup);
            incompleteBackup.Initialize();
            new SQLite().ExecuteNonQuery(backup, "DROP TABLE evx_finding_entities;");

            InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(
                () => store.RestoreAsync(backup));
            EventReport report = await store.ReadReportAsync(new EventStoreQuery { Oldest = true });

            Assert.Contains("evx_finding_entities", exception.Message, StringComparison.Ordinal);
            Assert.Single(report.Rows);
            Assert.Equal("alice", report.Rows[0].Values["User"]);
        } finally {
            DeleteStore(path);
            DeleteStore(backup);
        }
    }

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
