using System.Globalization;
using DBAClientX;
using EventViewerX.Reporting;
using EventViewerX.Storage;
using Xunit;

namespace EventViewerX.Tests;

public sealed partial class TestEventStore {
    [Fact]
    public async Task ActivityAwareIdentityMigrationProcessesMultipleBoundedPages() {
        string path = CreateStorePath();
        const int rowCount = 513;
        try {
            DateTime start = new(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc);
            EventReport report = CreateReport(Enumerable.Range(1, rowCount)
                .Select(index => (start.AddTicks(index), (long)index, $"user-{index}"))
                .ToArray());
            var store = new EventStore(path);
            await store.WriteAsync(report);
            using (var sqlite = new SQLite()) {
                using SQLiteSession session = sqlite.OpenSession(path);
                session.ExecuteNonQuery(
                    "UPDATE evx_events SET event_key = 'legacy-' || rowid, original_event_key = 'legacy-original-' || rowid;");
                session.ExecuteNonQuery(
                    "UPDATE evx_store_metadata SET event_identity_version = 1 WHERE singleton_id = 1;");
            }

            new EventStore(path).Initialize();

            using var verificationSqlite = new SQLite();
            using SQLiteSession verification = verificationSqlite.OpenSession(path);
            Assert.Equal(2L, Convert.ToInt64(
                verification.ExecuteScalar(
                    "SELECT event_identity_version FROM evx_store_metadata WHERE singleton_id = 1;"),
                CultureInfo.InvariantCulture));
            Assert.Equal(rowCount, Convert.ToInt32(
                verification.ExecuteScalar("SELECT COUNT(*) FROM evx_events;"),
                CultureInfo.InvariantCulture));
            Assert.Equal(0, Convert.ToInt32(
                verification.ExecuteScalar(
                    "SELECT COUNT(*) FROM evx_events WHERE event_key LIKE 'legacy-%' OR original_event_key LIKE 'legacy-original-%';"),
                CultureInfo.InvariantCulture));
        } finally {
            DeleteStore(path);
        }
    }
}
