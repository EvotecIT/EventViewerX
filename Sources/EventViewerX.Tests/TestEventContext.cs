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
    public async Task FutureNameConflictDoesNotMakeHistoricalStateAmbiguous() {
        var store = new InMemoryEventContextStore();
        await store.StoreAsync(Fact(CreatedUtc, "Original policy", "source-original"));
        await store.StoreAsync(Fact(RenamedUtc, "Future name one", "source-future-1"));
        await store.StoreAsync(Fact(RenamedUtc, "Future name two", "source-future-2"));

        EventContextResolution result = await store.ResolveAsync(Query(CreatedUtc.AddMinutes(5)));

        Assert.Equal(EventContextState.Historical, result.State);
        Assert.Equal("Original policy", result.NameAtEventTime);
        Assert.Equal("Original policy", result.LastKnownName);
        Assert.Null(result.CurrentName);
    }

    [Fact]
    public async Task FutureDomainConflictSuppressesOnlyCurrentContext() {
        var store = new InMemoryEventContextStore();
        await store.StoreAsync(Fact(CreatedUtc, "Original policy", "source-original"));
        EventContextFact first = Fact(RenamedUtc, "Renamed policy", "source-future-domain-1");
        EventContextFact second = Fact(RenamedUtc, "Renamed policy", "source-future-domain-2");
        second.Domain = "other.example.com";
        await store.StoreAsync(first);
        await store.StoreAsync(second);

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
    public async Task StableNameBeforeAConflictRemainsTheLastKnownName() {
        var store = new InMemoryEventContextStore();
        await store.StoreAsync(Fact(CreatedUtc, "Original policy", "source-original"));
        await store.StoreAsync(Fact(RenamedUtc, "Conflicting name one", "source-conflict-1"));
        await store.StoreAsync(Fact(RenamedUtc, "Conflicting name two", "source-conflict-2"));

        EventContextResolution result = await store.ResolveAsync(Query(RenamedUtc));

        Assert.Equal(EventContextState.Ambiguous, result.State);
        Assert.Null(result.NameAtEventTime);
        Assert.Equal("Original policy", result.LastKnownName);
        Assert.Null(result.CurrentName);
    }

    [Fact]
    public async Task HistoricalConflictDoesNotSuppressALaterStableCurrentName() {
        var store = new InMemoryEventContextStore();
        await store.StoreAsync(Fact(CreatedUtc, "Conflicting name one", "source-conflict-1"));
        await store.StoreAsync(Fact(CreatedUtc, "Conflicting name two", "source-conflict-2"));
        await store.StoreAsync(Fact(RenamedUtc, "Current stable name", "source-current"));

        EventContextResolution result = await store.ResolveAsync(Query(CreatedUtc));

        Assert.Equal(EventContextState.Ambiguous, result.State);
        Assert.Null(result.NameAtEventTime);
        Assert.Equal("Current stable name", result.CurrentName);
    }

    [Fact]
    public async Task RecreationAfterDeletionDoesNotCarryThePreviousNameForward() {
        var store = new InMemoryEventContextStore();
        await store.StoreAsync(Fact(CreatedUtc, "Original policy", "source-original"));
        await store.StoreAsync(Fact(RenamedUtc, null, "source-delete", isDeleted: true));
        await store.StoreAsync(Fact(DeletedUtc, null, "source-recreate"));

        EventContextResolution result = await store.ResolveAsync(Query(DeletedUtc));

        Assert.Equal(EventContextState.Current, result.State);
        Assert.Null(result.NameAtEventTime);
        Assert.Equal("Original policy", result.LastKnownName);
        Assert.Null(result.CurrentName);
    }

    [Fact]
    public async Task ExplicitDisplayNameRemovalStopsNameCarryForward() {
        var store = new InMemoryEventContextStore();
        await store.StoreAsync(Fact(CreatedUtc, "Original policy", "source-original"));
        EventContextFact removal = Fact(RenamedUtc, null, "source-name-removed");
        removal.DisplayNameObserved = true;
        await store.StoreAsync(removal);

        EventContextResolution result = await store.ResolveAsync(Query(RenamedUtc));

        Assert.Equal(EventContextState.Current, result.State);
        Assert.Null(result.NameAtEventTime);
        Assert.Equal("Original policy", result.LastKnownName);
        Assert.Null(result.CurrentName);
    }

    [Fact]
    public async Task BatchResolutionMatchesSingleQuerySemantics() {
        var store = new InMemoryEventContextStore();
        await store.StoreManyAsync(CreateTimeline());
        EventContextQuery[] queries = {
            Query(CreatedUtc),
            Query(RenamedUtc),
            Query(DeletedUtc)
        };

        IReadOnlyList<EventContextResolution> batch = await store.ResolveManyAsync(queries);

        Assert.Equal(queries.Length, batch.Count);
        for (int i = 0; i < queries.Length; i++) {
            EventContextResolution single = await store.ResolveAsync(queries[i]);
            Assert.Equal(single.State, batch[i].State);
            Assert.Equal(single.NameAtEventTime, batch[i].NameAtEventTime);
            Assert.Equal(single.LastKnownName, batch[i].LastKnownName);
            Assert.Equal(single.CurrentName, batch[i].CurrentName);
        }
    }

    [Fact]
    public async Task ConflictingLatestDeletionStateNeverExposesACurrentName() {
        var store = new InMemoryEventContextStore();
        await store.StoreAsync(Fact(CreatedUtc, "Original policy", "source-original"));
        await store.StoreAsync(Fact(RenamedUtc, null, "source-a-deleted", isDeleted: true));
        await store.StoreAsync(Fact(RenamedUtc, "Renamed policy", "source-z-live"));

        EventContextResolution result = await store.ResolveAsync(Query(CreatedUtc.AddMinutes(5)));

        Assert.Equal(EventContextState.Historical, result.State);
        Assert.Equal("Original policy", result.NameAtEventTime);
        Assert.Null(result.CurrentName);
    }

    [Fact]
    public async Task ConflictingDomainsFailClosedAsAmbiguous() {
        var store = new InMemoryEventContextStore();
        EventContextFact first = Fact(CreatedUtc, "Policy", "source-domain-1");
        EventContextFact second = Fact(CreatedUtc, "Policy", "source-domain-2");
        second.Domain = "other.example.com";
        await store.StoreAsync(first);
        await store.StoreAsync(second);

        EventContextResolution result = await store.ResolveAsync(Query(CreatedUtc));

        Assert.Equal(EventContextState.Ambiguous, result.State);
        Assert.Null(result.Domain);
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
    public async Task SuppliedCanonicalIdentityCannotFallBackToAnotherObjectsAlias() {
        var store = new InMemoryEventContextStore();
        const string firstDn = "CN=First,OU=Policies,DC=ad,DC=evotec,DC=xyz";
        const string secondDn = "CN=Second,OU=Policies,DC=ad,DC=evotec,DC=xyz";
        EventContextFact first = Fact(CreatedUtc, "First object", "source-first", firstDn);
        EventContextFact second = Fact(CreatedUtc, "Second object", "source-second", secondDn);
        second.CanonicalId = "A3AB176B-A8F8-4D42-944C-C5258D1E4F65";
        await store.StoreAsync(first);
        await store.StoreAsync(second);

        EventContextResolution result = await store.ResolveAsync(new EventContextQuery {
            ObjectKind = EventContextObjectKind.GroupPolicy,
            CanonicalId = GpoId.ToString("D"),
            Alias = secondDn,
            AtUtc = CreatedUtc
        });

        Assert.Equal(EventContextState.Ambiguous, result.State);
        Assert.Equal(GpoId.ToString("D").ToUpperInvariant(), result.CanonicalId);
        Assert.Contains("different objects", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SqliteIdentityLookupPreservesCanonicalAndAliasAgreement() {
        string path = CreateStorePath();
        try {
            var store = new SqliteEventContextStore(path);
            const string firstDn = "CN=First,OU=Policies,DC=ad,DC=evotec,DC=xyz";
            const string secondDn = "CN=Second,OU=Policies,DC=ad,DC=evotec,DC=xyz";
            EventContextFact first = Fact(CreatedUtc, "First object", "source-first", firstDn);
            EventContextFact second = Fact(CreatedUtc, "Second object", "source-second", secondDn);
            second.CanonicalId = "A3AB176B-A8F8-4D42-944C-C5258D1E4F65";
            await store.StoreAsync(first);
            await store.StoreAsync(second);

            EventContextResolution result = await store.ResolveAsync(new EventContextQuery {
                ObjectKind = EventContextObjectKind.GroupPolicy,
                CanonicalId = GpoId.ToString("D"),
                Alias = secondDn,
                AtUtc = CreatedUtc
            });

            Assert.Equal(EventContextState.Ambiguous, result.State);
            Assert.Equal(GpoId.ToString("D").ToUpperInvariant(), result.CanonicalId);
        } finally {
            DeleteStore(path);
        }
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
    public async Task IdenticalEvidenceCanBeStoredForSeparateAuthorizationPartitions() {
        string path = CreateStorePath();
        try {
            var store = new SqliteEventContextStore(path);
            EventContextFact first = Fact(CreatedUtc, "Restricted policy", "lookup-shared-source");
            first.Provenance = EventContextProvenance.LiveLookup;
            first.IsShareable = false;
            first.AuthorizationContext = "EVOTEC\\reader-a";
            EventContextFact second = Fact(CreatedUtc, "Restricted policy", "lookup-shared-source");
            second.Provenance = EventContextProvenance.LiveLookup;
            second.IsShareable = false;
            second.AuthorizationContext = "EVOTEC\\reader-b";

            Assert.NotEqual(
                EventContextIdentity.CreateFactKey(first),
                EventContextIdentity.CreateFactKey(second));
            await store.StoreAsync(first);
            await store.StoreAsync(second);

            EventContextQuery firstQuery = Query(CreatedUtc);
            firstQuery.AuthorizationContext = "EVOTEC\\reader-a";
            EventContextQuery secondQuery = Query(CreatedUtc);
            secondQuery.AuthorizationContext = "EVOTEC\\reader-b";
            Assert.Equal(EventContextState.Current, (await store.ResolveAsync(firstQuery)).State);
            Assert.Equal(EventContextState.Current, (await store.ResolveAsync(secondQuery)).State);
            using var sqlite = new SQLite { BusyTimeoutMs = 10000 };
            using SQLiteSession session = sqlite.OpenSession(path);
            Assert.Equal(2L, Convert.ToInt64(session.ExecuteScalar(
                "SELECT COUNT(*) FROM evx_context_facts;"),
                CultureInfo.InvariantCulture));
        } finally {
            DeleteStore(path);
        }
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
    public async Task SqliteBatchResolutionCrossesTheBoundedQueryChunk() {
        string path = CreateStorePath();
        try {
            var facts = new List<EventContextFact>();
            var queries = new List<EventContextQuery>();
            for (int index = 0; index < 101; index++) {
                Guid id = Guid.Parse($"00000000-0000-0000-0000-{index + 1:000000000000}");
                string alias = $"CN=Policy-{index},CN=Policies,DC=ad,DC=evotec,DC=xyz";
                EventContextFact fact = Fact(
                    CreatedUtc.AddMinutes(index),
                    "Policy " + index.ToString(CultureInfo.InvariantCulture),
                    "batch-" + index.ToString(CultureInfo.InvariantCulture),
                    alias);
                fact.CanonicalId = id.ToString("D");
                fact.Aliases = new[] { alias };
                facts.Add(fact);
                queries.Add(new EventContextQuery {
                    ObjectKind = EventContextObjectKind.GroupPolicy,
                    CanonicalId = id.ToString("D"),
                    Alias = alias,
                    AtUtc = fact.EffectiveAtUtc
                });
            }
            var store = new SqliteEventContextStore(path);
            await store.StoreManyAsync(facts);

            IReadOnlyList<EventContextResolution> results = await store.ResolveManyAsync(queries);

            Assert.Equal(101, results.Count);
            Assert.All(results, result => Assert.Equal(EventContextState.Current, result.State));
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
    public async Task MateriallyDifferentFactsFromTheSameSourceRemainVisible() {
        var store = new InMemoryEventContextStore();
        EventContextFact first = Fact(CreatedUtc, "First interpretation", "shared-source");
        EventContextFact second = Fact(CreatedUtc, "Second interpretation", "shared-source");

        Assert.NotEqual(
            EventContextIdentity.CreateFactKey(first),
            EventContextIdentity.CreateFactKey(second));
        await store.StoreAsync(first);
        await store.StoreAsync(second);

        EventContextResolution result = await store.ResolveAsync(Query(CreatedUtc));

        Assert.Equal(EventContextState.Ambiguous, result.State);
        Assert.Null(result.NameAtEventTime);
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
    public void VersionOneContextSchemaMigratesDisplayNameObservationState() {
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
VALUES (1, 1, '2026-08-23T00:00:00.0000000Z');
CREATE TABLE evx_context_facts (
    fact_key TEXT NOT NULL PRIMARY KEY,
    object_kind INTEGER NOT NULL,
    canonical_id TEXT NOT NULL,
    display_name TEXT NULL,
    domain TEXT NULL,
    distinguished_name TEXT NULL,
    effective_utc TEXT NOT NULL,
    observed_utc TEXT NOT NULL,
    is_deleted INTEGER NOT NULL,
    provenance INTEGER NOT NULL,
    source_identity TEXT NOT NULL,
    provider_name TEXT NOT NULL,
    provider_schema_version INTEGER NOT NULL,
    confidence_reason TEXT NULL,
    authorization_context TEXT NULL,
    is_shareable INTEGER NOT NULL
);
CREATE TABLE evx_context_aliases (
    fact_key TEXT NOT NULL,
    alias TEXT NOT NULL,
    PRIMARY KEY (fact_key, alias)
);");
            }

            new SqliteEventContextStore(path).Initialize();

            using var verificationClient = new SQLite { BusyTimeoutMs = 10000 };
            using SQLiteSession verification = verificationClient.OpenSession(path);
            Assert.Equal(2L, Convert.ToInt64(verification.ExecuteScalar(
                "SELECT schema_version FROM evx_context_metadata WHERE singleton_id = 1;"),
                CultureInfo.InvariantCulture));
            Assert.Equal(1L, Convert.ToInt64(verification.ExecuteScalar(
                "SELECT COUNT(*) FROM pragma_table_info('evx_context_facts') " +
                "WHERE name = 'display_name_observed';"),
                CultureInfo.InvariantCulture));
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
