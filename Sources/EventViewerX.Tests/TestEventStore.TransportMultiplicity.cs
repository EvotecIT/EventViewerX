using EventViewerX.Reporting;
using EventViewerX.Storage;
using Xunit;

namespace EventViewerX.Tests;

public sealed partial class TestEventStore {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PartialCollectorAndFullSourcePreserveMultiplicityInEitherIngestionOrder(bool sourceFirst) {
        string path = CreateStorePath();
        try {
            DateTime time = new(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc);
            EventReport collector = CreateReportFromTransport(time, 42, "alice", "WEC01", "ForwardedEvents");
            EventReport source = CreateReportFromTransport(new[] {
                (time, 9001L, "alice"), (time, 9002L, "alice")
            }, "source.ad.evotec.xyz", "Security");
            var store = new EventStore(path);
            await store.WriteAsync(sourceFirst ? source : collector);
            await store.WriteAsync(sourceFirst ? collector : source);
            EventStoreWriteResult repeated = await store.WriteAsync(source);
            EventReport stored = await store.ReadReportAsync(new EventStoreQuery());

            Assert.Equal(3, stored.Rows.Count);
            Assert.Equal(2, stored.Rows.Count(row => row.ContainerLog == "Security"));
            Assert.Equal(0, repeated.Inserted);
            Assert.Equal(2, repeated.Duplicates);
        } finally {
            DeleteStore(path);
        }
    }
}
