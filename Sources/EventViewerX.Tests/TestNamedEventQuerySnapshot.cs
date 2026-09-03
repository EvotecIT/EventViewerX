using System.Net;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventTypeQuerySnapshot {
    [Fact]
    public void QueryRejectsUndefinedNamedEventValues() {
        Assert.Throws<
            ArgumentOutOfRangeException>(() =>
                new EventTypeQuery(
                    new[] {
                        (EventType)int.MaxValue
                    }));
    }

    [Fact]
    public async Task ReadAsyncFreezesTheQueryBeforeEnumeration() {
        EventType namedEvent =
            Enum.GetValues(typeof(EventType))
                .Cast<EventType>()
                .First();
        var credential =
            new NetworkCredential(
                "original",
                "secret",
                "domain");
        var query = new EventTypeQuery(
            new[] { namedEvent }) {
            SourceEventIds = Array.Empty<int>(),
            MachineNames = new[] { "remote.contoso.test" },
            Credential = credential,
            MaxConcurrency = 1,
            Enrichment =
                new EventEnrichmentOptions {
                    ResolveDns = false,
                    DnsMaxConcurrency = 1
                }
        };

        IAsyncEnumerable<EventTypeRecord> stream =
            EventTypeEngine.ReadAsync(query);
        query.MaxConcurrency = 0;
        query.SourceEventIds = new[] { -1 };
        credential.UserName = "mutated";
        query.Enrichment.DnsMaxConcurrency = 0;

        int count = 0;
        await foreach (EventTypeRecord _ in stream) {
            count++;
        }

        Assert.Equal(0, count);
    }

    [Fact]
    public void SnapshotClonesMutableCredentialsAndEnrichment() {
        EventType namedEvent =
            Enum.GetValues(typeof(EventType))
                .Cast<EventType>()
                .First();
        var credential =
            new NetworkCredential(
                "original",
                "secret",
                "domain");
        var query = new EventTypeQuery(
            new[] { namedEvent }) {
            Credential = credential,
            SourceProviderNames = new[] { "Provider-A" },
            SourceRecordIds = new long[] { 10, 20 },
            Enrichment =
                new EventEnrichmentOptions {
                    ResolveDns = true,
                    DnsMaxConcurrency = 2
                }
        };

        EventTypeQuery snapshot =
            EventTypeQuerySnapshot.Copy(query);
        credential.UserName = "mutated";
        query.Enrichment.DnsMaxConcurrency = 1;
        query.SourceProviderNames = new[] { "Provider-B" };
        query.SourceRecordIds = new long[] { 30 };

        Assert.Equal(
            "original",
            snapshot.Credential!.UserName);
        Assert.Equal(
            2,
            snapshot.Enrichment!.DnsMaxConcurrency);
        Assert.Equal(new[] { "Provider-A" }, snapshot.SourceProviderNames);
        Assert.Equal(new long[] { 10, 20 }, snapshot.SourceRecordIds);
    }

    [Fact]
    public void SnapshotClonesOfflinePathsAndRejectsRemoteMixing() {
        string[] paths = { "one.evtx", "two.evtx" };
        var query = new EventTypeQuery(new[] { EventType.OSStartup }) {
            Paths = paths
        };

        EventTypeQuery snapshot = EventTypeQuerySnapshot.Copy(query);
        paths[0] = "changed.evtx";

        Assert.Equal(new[] { "one.evtx", "two.evtx" }, snapshot.Paths);
        query.MachineNames = new[] { "server" };
        Assert.Throws<ArgumentException>(() => EventTypeEngine.ReadAsync(query));
    }

    [Fact]
    public void ReadAsyncValidatesRemoteOptionsBeforeEnumeration() {
        EventType namedEvent =
            Enum.GetValues(typeof(EventType))
                .Cast<EventType>()
                .First();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EventTypeEngine.ReadAsync(
                new EventTypeQuery(
                    new[] { namedEvent }) {
                    Authentication =
                        (EventLogAuthentication)int.MaxValue
                }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EventTypeEngine.ReadAsync(
                new EventTypeQuery(
                    new[] { namedEvent }) {
                    RemoteConnectionTimeoutMilliseconds =
                        0
                }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EventTypeEngine.ReadAsync(
                new EventTypeQuery(
                    new[] { namedEvent }) {
                    RemoteReadTimeoutMilliseconds =
                        -1
                }));
    }
}
