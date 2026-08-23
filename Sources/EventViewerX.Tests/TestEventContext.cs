using System.Globalization;
using DBAClientX;
using EventViewerX.Storage;
using Xunit;

namespace EventViewerX.Tests;

public sealed class TestEventContext {
    private static readonly Guid GpoId = Guid.Parse("FB6A0E91-F93D-4428-B29D-2FDCC3A95425");
    private static readonly DateTime CreatedUtc = new(2026, 8, 20, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime RenamedUtc = CreatedUtc.AddHours(1);
    private static readonly DateTime DeletedUtc = CreatedUtc.AddHours(2);

    [Fact]
    public async Task TimelineIsIndependentOfFactArrivalOrder() {
        EventContextFact[] facts = CreateTimeline();
        var oldestFirst = new InMemoryEventContextStore();
        var newestFirst = new InMemoryEventContextStore();
        foreach (EventContextFact fact in facts) {
            await oldestFirst.StoreAsync(fact);
        }
        foreach (EventContextFact fact in facts.AsEnumerable().Reverse()) {
            await newestFirst.StoreAsync(fact);
        }

        EventContextResolution left = await oldestFirst.ResolveAsync(Query(DeletedUtc));
        EventContextResolution right = await newestFirst.ResolveAsync(Query(DeletedUtc));

        Assert.Equal(EventContextState.Deleted, left.State);
        Assert.Equal("Renamed policy", left.NameAtEventTime);
        Assert.Equal("Renamed policy", left.LastKnownName);
        Assert.Null(left.CurrentName);
        Assert.Equal(left.State, right.State);
        Assert.Equal(left.NameAtEventTime, right.NameAtEventTime);
        Assert.Equal(left.DistinguishedName, right.DistinguishedName);
    }

    [Fact]
    public async Task HistoricalResolutionDoesNotLeakAFutureName() {
        var store = new InMemoryEventContextStore();
        foreach (EventContextFact fact in CreateTimeline()) {
            await store.StoreAsync(fact);
        }

        EventContextResolution result = await store.ResolveAsync(Query(CreatedUtc.AddMinutes(5)));

        Assert.Equal(EventContextState.Historical, result.State);
        Assert.Equal("Original policy", result.NameAtEventTime);
        Assert.Null(result.CurrentName);
    }

    [Fact]
    public async Task SameTimeConflictsFailClosedAsAmbiguous() {
        var store = new InMemoryEventContextStore();
        await store.StoreAsync(Fact(CreatedUtc, "First name", "source-1"));
        await store.StoreAsync(Fact(CreatedUtc, "Second name", "source-2"));

        EventContextResolution result = await store.ResolveAsync(Query(CreatedUtc));

        Assert.Equal(EventContextState.Ambiguous, result.State);
        Assert.Null(result.NameAtEventTime);
        Assert.Null(result.CurrentName);
        Assert.Contains("disagree", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReusedAliasAcrossCanonicalObjectsFailsClosed() {
        var store = new InMemoryEventContextStore();
        const string reusedDn = "CN=Reused,OU=Policies,DC=ad,DC=evotec,DC=xyz";
        EventContextFact first = Fact(CreatedUtc, "First object", "source-1", reusedDn);
        EventContextFact second = Fact(CreatedUtc.AddHours(1), "Second object", "source-2", reusedDn);
        second.CanonicalId = "A3AB176B-A8F8-4D42-944C-C5258D1E4F65";
        await store.StoreAsync(first);
        await store.StoreAsync(second);

        EventContextResolution result = await store.ResolveAsync(new EventContextQuery {
            ObjectKind = EventContextObjectKind.GroupPolicy,
            Alias = reusedDn,
            AtUtc = CreatedUtc.AddHours(2)
        });

        Assert.Equal(EventContextState.Ambiguous, result.State);
        Assert.Null(result.CanonicalId);
        Assert.Contains("more than one", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NonShareableEvidenceIsPartitionedByAuthorizationContext() {
        var store = new InMemoryEventContextStore();
        EventContextFact fact = Fact(CreatedUtc, "Restricted policy", "lookup-1");
        fact.Provenance = EventContextProvenance.LiveLookup;
        fact.IsShareable = false;
        fact.AuthorizationContext = "EVOTEC\\reader-a";
        await store.StoreAsync(fact);

        EventContextResolution denied = await store.ResolveAsync(Query(CreatedUtc));
        EventContextQuery allowedQuery = Query(CreatedUtc);
        allowedQuery.AuthorizationContext = "evotec\\READER-A";
        EventContextResolution allowed = await store.ResolveAsync(allowedQuery);

        Assert.Equal(EventContextState.Unknown, denied.State);
        Assert.Equal(EventContextState.Current, allowed.State);
        Assert.Equal("Restricted policy", allowed.NameAtEventTime);
    }

    [Fact]
    public async Task NonShareableEvidenceRequiresAnAuthorizationPartition() {
        var store = new InMemoryEventContextStore();
        EventContextFact fact = Fact(CreatedUtc, "Restricted policy", "lookup-without-context");
        fact.IsShareable = false;

        await Assert.ThrowsAsync<ArgumentException>(() => store.StoreAsync(fact).AsTask());
    }

    [Fact]
    public async Task SqliteStorePersistsAliasesAndTimelineAcrossInstances() {
        string path = CreateStorePath();
        try {
            var writer = new SqliteEventContextStore(path);
            foreach (EventContextFact fact in CreateTimeline()) {
                await writer.StoreAsync(fact);
                await writer.StoreAsync(fact);
            }

            var reader = new SqliteEventContextStore(path);
            EventContextResolution byOldDistinguishedName = await reader.ResolveAsync(new EventContextQuery {
                ObjectKind = EventContextObjectKind.GroupPolicy,
                Alias = "CN={FB6A0E91-F93D-4428-B29D-2FDCC3A95425},OU=Staging,DC=ad,DC=evotec,DC=xyz",
                AtUtc = DeletedUtc
            });

            Assert.Equal(EventContextState.Deleted, byOldDistinguishedName.State);
            Assert.Equal(GpoId.ToString("D").ToUpperInvariant(), byOldDistinguishedName.CanonicalId);
            Assert.Equal("Renamed policy", byOldDistinguishedName.LastKnownName);
            using var sqlite = new SQLite { BusyTimeoutMs = 10000 };
            using SQLiteSession session = sqlite.OpenSession(path);
            Assert.Equal(3L, Convert.ToInt64(session.ExecuteScalar(
                "SELECT COUNT(*) FROM evx_context_facts;"),
                CultureInfo.InvariantCulture));
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task ReobservingTheSameSourceIsIdempotent() {
        string path = CreateStorePath();
        try {
            var store = new SqliteEventContextStore(path);
            EventContextFact first = Fact(CreatedUtc, "Original policy", "event-create");
            EventContextFact reobserved = Fact(CreatedUtc, "Original policy", "event-create");
            reobserved.ObservedAtUtc = first.ObservedAtUtc.AddDays(1);

            await store.StoreAsync(first);
            await store.StoreAsync(reobserved);

            using var sqlite = new SQLite { BusyTimeoutMs = 10000 };
            using SQLiteSession session = sqlite.OpenSession(path);
            Assert.Equal(1L, Convert.ToInt64(session.ExecuteScalar(
                "SELECT COUNT(*) FROM evx_context_facts;"),
                CultureInfo.InvariantCulture));
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task ContextAndEventHistoryCanShareOneDatabase() {
        string path = CreateStorePath();
        try {
            var context = new SqliteEventContextStore(path);
            await context.StoreAsync(Fact(CreatedUtc, "Shared database policy", "event-create"));

            new EventStore(path).Initialize();
            EventContextResolution resolution = await new SqliteEventContextStore(path)
                .ResolveAsync(Query(CreatedUtc));

            Assert.Equal(EventContextState.Current, resolution.State);
            Assert.Equal("Shared database policy", resolution.CurrentName);
            using var sqlite = new SQLite { BusyTimeoutMs = 10000 };
            using SQLiteSession session = sqlite.OpenSession(path);
            Assert.Equal(1L, Convert.ToInt64(session.ExecuteScalar(
                "SELECT COUNT(*) FROM evx_store_metadata;"),
                CultureInfo.InvariantCulture));
            Assert.Equal(1L, Convert.ToInt64(session.ExecuteScalar(
                "SELECT COUNT(*) FROM evx_context_metadata;"),
                CultureInfo.InvariantCulture));
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task ConcurrentWritersPreserveEveryIndependentFact() {
        string path = CreateStorePath();
        try {
            EventContextFact[] facts = Enumerable.Range(0, 8)
                .Select(index => Fact(
                    CreatedUtc.AddMinutes(index),
                    "Policy " + index.ToString(CultureInfo.InvariantCulture),
                    "event-" + index.ToString(CultureInfo.InvariantCulture)))
                .ToArray();

            await Task.WhenAll(facts.Select(fact => new SqliteEventContextStore(path)
                .StoreAsync(fact)
                .AsTask()));

            using var sqlite = new SQLite { BusyTimeoutMs = 10000 };
            using SQLiteSession session = sqlite.OpenSession(path);
            Assert.Equal(8L, Convert.ToInt64(session.ExecuteScalar(
                "SELECT COUNT(*) FROM evx_context_facts;"),
                CultureInfo.InvariantCulture));
            EventContextResolution resolution = await new SqliteEventContextStore(path)
                .ResolveAsync(Query(CreatedUtc.AddMinutes(7)));
            Assert.Equal("Policy 7", resolution.CurrentName);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public void UnsupportedContextSchemaFailsBeforeCreatingCurrentTables() {
        string path = CreateStorePath();
        try {
            using (var sqlite = new SQLite { BusyTimeoutMs = 10000 }) {
                using SQLiteSession session = sqlite.OpenSession(path);
                session.ExecuteNonQuery(@"
CREATE TABLE evx_context_metadata (
    singleton_id INTEGER NOT NULL PRIMARY KEY,
    schema_version INTEGER NOT NULL,
    created_utc TEXT NOT NULL
);
INSERT INTO evx_context_metadata (singleton_id, schema_version, created_utc)
VALUES (1, 99, '2026-08-23T00:00:00.0000000Z');");
            }

            Assert.Throws<InvalidDataException>(() => new SqliteEventContextStore(path).Initialize());

            using var verificationClient = new SQLite { BusyTimeoutMs = 10000 };
            using SQLiteSession verification = verificationClient.OpenSession(path);
            Assert.Null(verification.ExecuteScalar(
                "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'evx_context_facts';"));
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task FactsAfterRequestedTimeRemainUnknown() {
        var store = new InMemoryEventContextStore();
        await store.StoreAsync(Fact(CreatedUtc, "Future policy", "source-future"));

        EventContextResolution result = await store.ResolveAsync(Query(CreatedUtc.AddMinutes(-1)));

        Assert.Equal(EventContextState.Unknown, result.State);
        Assert.Contains("after", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    private static EventContextFact[] CreateTimeline() => new[] {
        Fact(
            CreatedUtc,
            "Original policy",
            "event-create",
            "CN={FB6A0E91-F93D-4428-B29D-2FDCC3A95425},OU=Staging,DC=ad,DC=evotec,DC=xyz"),
        Fact(
            RenamedUtc,
            "Renamed policy",
            "event-rename",
            "CN={FB6A0E91-F93D-4428-B29D-2FDCC3A95425},CN=Policies,CN=System,DC=ad,DC=evotec,DC=xyz",
            new[] { "CN={FB6A0E91-F93D-4428-B29D-2FDCC3A95425},OU=Staging,DC=ad,DC=evotec,DC=xyz" }),
        Fact(
            DeletedUtc,
            null,
            "event-delete",
            "CN={FB6A0E91-F93D-4428-B29D-2FDCC3A95425},CN=Policies,CN=System,DC=ad,DC=evotec,DC=xyz",
            isDeleted: true)
    };

    private static EventContextFact Fact(
        DateTime effectiveUtc,
        string? displayName,
        string sourceIdentity,
        string? distinguishedName = "CN={FB6A0E91-F93D-4428-B29D-2FDCC3A95425},CN=Policies,CN=System,DC=ad,DC=evotec,DC=xyz",
        IReadOnlyList<string>? extraAliases = null,
        bool isDeleted = false) => new() {
        ObjectKind = EventContextObjectKind.GroupPolicy,
        CanonicalId = GpoId.ToString("D"),
        Aliases = new[] { distinguishedName ?? string.Empty }
            .Concat(extraAliases ?? Array.Empty<string>())
            .Where(static value => value.Length > 0)
            .ToArray(),
        DisplayName = displayName,
        Domain = "ad.evotec.xyz",
        DistinguishedName = distinguishedName,
        EffectiveAtUtc = effectiveUtc,
        ObservedAtUtc = effectiveUtc.AddMinutes(1),
        IsDeleted = isDeleted,
        Provenance = EventContextProvenance.Event,
        SourceIdentity = sourceIdentity,
        ProviderName = "EventViewerX.Tests",
        ProviderSchemaVersion = 1,
        IsShareable = true
    };

    private static EventContextQuery Query(DateTime atUtc) => new() {
        ObjectKind = EventContextObjectKind.GroupPolicy,
        CanonicalId = GpoId.ToString("D"),
        AtUtc = atUtc
    };

    private static string CreateStorePath() => Path.Combine(
        Path.GetTempPath(),
        $"eventviewerx-context-{Guid.NewGuid():N}.db");

    private static void DeleteStore(string path) {
        foreach (string candidate in new[] { path, path + "-wal", path + "-shm" }) {
            if (File.Exists(candidate)) {
                File.Delete(candidate);
            }
        }
    }
}
