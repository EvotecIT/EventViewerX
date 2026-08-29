using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Security.Principal;
using DBAClientX;
using EventViewerX.Reporting;
using EventViewerX.Storage;
using Xunit;

namespace EventViewerX.Tests;

public sealed partial class TestEventStore {
    [Fact]
    public async Task RecordlessRowsWithDifferentSystemActivityMetadataRemainDistinct() {
        string path = CreateStorePath();
        try {
            EventReportRow first = CreateStoredActivityRow(
                1,
                Guid.Parse("d708180e-f236-4b18-bedd-53ac5ed45266"),
                relatedActivityId: null);
            EventReportRow second = CreateStoredActivityRow(
                1,
                Guid.Parse("458e53d3-748e-4892-b8f3-7cf56d226bd3"),
                relatedActivityId: null);
            first.RecordId = null;
            second.RecordId = null;
            var store = new EventStore(path);

            EventStoreWriteResult written = await store.WriteAsync(EventReportEngine.CreateStored(
                new[] { first, second },
                new[] { EventReportSectionSchema.CreateGeneric() }));
            EventReport stored = await store.ReadReportAsync(new EventStoreQuery { Oldest = true });

            Assert.Equal(2, written.Inserted);
            Assert.Equal(2, stored.Rows.Count);
            Assert.Equal(2, stored.Rows.Select(static row => row.ActivityId).Distinct().Count());
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task ActivityAwareIdentityMigrationRekeysPopulatedStores() {
        string path = CreateStorePath();
        try {
            EventReportRow row = CreateStoredActivityRow(
                1,
                Guid.Parse("82758911-d9f7-454c-a14d-0fab838a84d2"),
                relatedActivityId: null);
            row.RecordId = null;
            var store = new EventStore(path);
            await store.WriteAsync(EventReportEngine.CreateStored(
                new[] { row },
                new[] { EventReportSectionSchema.CreateGeneric() }));
            using (var sqlite = new SQLite()) {
                using SQLiteSession session = sqlite.OpenSession(path);
                session.ExecuteNonQuery(
                    "UPDATE evx_events SET event_key = 'legacy-key', original_event_key = 'legacy-original';");
                session.ExecuteNonQuery(
                    "UPDATE evx_store_metadata SET event_identity_version = 1 WHERE singleton_id = 1;");
            }

            new EventStore(path).Initialize();

            using var verificationSqlite = new SQLite();
            using SQLiteSession verification = verificationSqlite.OpenSession(path);
            Assert.Equal(3L, Convert.ToInt64(
                verification.ExecuteScalar(
                    "SELECT event_identity_version FROM evx_store_metadata WHERE singleton_id = 1;"),
                CultureInfo.InvariantCulture));
            Assert.NotEqual("legacy-key", Convert.ToString(
                verification.ExecuteScalar("SELECT event_key FROM evx_events LIMIT 1;"),
                CultureInfo.InvariantCulture));
            Assert.NotEqual("legacy-original", Convert.ToString(
                verification.ExecuteScalar("SELECT original_event_key FROM evx_events LIMIT 1;"),
                CultureInfo.InvariantCulture));
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task ActivityAwareIdentityMigrationDoesNotDuplicateRecordBasedRows() {
        string path = CreateStorePath();
        try {
            EventReportRow row = CreateStoredActivityRow(
                1,
                Guid.Parse("82758911-d9f7-454c-a14d-0fab838a84d2"),
                relatedActivityId: Guid.Parse("7e2abf19-c7d2-40a9-8ec9-a5c7168e3c06"));
            EventReport report = EventReportEngine.CreateStored(
                new[] { row },
                new[] { EventReportSectionSchema.CreateGeneric() });
            var store = new EventStore(path);
            await store.WriteAsync(report);
            using (var sqlite = new SQLite()) {
                using SQLiteSession session = sqlite.OpenSession(path);
                session.ExecuteNonQuery(
                    "UPDATE evx_events SET activity_id = NULL, related_activity_id = NULL, " +
                    "event_key = 'legacy-key', original_event_key = 'legacy-original';");
                session.ExecuteNonQuery(
                    "UPDATE evx_store_metadata SET event_identity_version = 1 WHERE singleton_id = 1;");
            }

            var migratedStore = new EventStore(path);
            migratedStore.Initialize();
            EventStoreWriteResult duplicate = await migratedStore.WriteAsync(report);
            EventReport stored = await migratedStore.ReadReportAsync(new EventStoreQuery { Oldest = true });

            Assert.Equal(0, duplicate.Inserted);
            Assert.Equal(1, duplicate.Duplicates);
            Assert.Single(stored.Rows);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task StoredRowsRetainSystemActivityMetadataForSemanticGrouping() {
        string path = CreateStorePath();
        Guid parentActivity = Guid.Parse("7e2abf19-c7d2-40a9-8ec9-a5c7168e3c06");
        try {
            EventReportRow parent = CreateStoredActivityRow(1, parentActivity, relatedActivityId: null);
            EventReportRow child = CreateStoredActivityRow(
                2,
                Guid.Parse("4ce61f98-73d6-4e2e-bf3c-3390e20e100d"),
                parentActivity);
            var store = new EventStore(path);
            await store.WriteAsync(EventReportEngine.CreateStored(
                new[] { parent, child },
                new[] { EventReportSectionSchema.CreateGeneric() }));

            EventReport stored = await store.ReadReportAsync(new EventStoreQuery { Oldest = true });
            EventOccurrenceGroup group = Assert.Single(EventOccurrenceEngine.Group(
                stored.Rows,
                new EventOccurrenceOptions { Mode = EventDuplicateMode.Semantic }).Groups);

            Assert.Equal(parentActivity, stored.Rows[0].ActivityId);
            Assert.Equal(parentActivity, stored.Rows[1].RelatedActivityId);
            Assert.Equal(2, group.ObservationCount);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task GroupPolicyStoreAliasUsesTheEnrichedPredicateSchema() {
        string path = CreateStorePath();
        try {
            var row = new EventReportRow {
                TimeCreated = new DateTime(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc),
                Type = "GroupPolicyAudit",
                EventId = 5136,
                RecordId = 42,
                Provider = "Microsoft-Windows-Security-Auditing",
                SourceLog = "Security",
                ContainerLog = "ForwardedEvents",
                SourceComputer = "dc1.ad.evotec.xyz",
                CollectorComputer = "wec1.ad.evotec.xyz",
                Values = new Dictionary<string, object?> {
                    ["Actor"] = "AD\\alice",
                    ["GroupPolicyNameAtEventTime"] = "Workstation Baseline"
                }
            };
            var rawRow = new EventReportRow {
                TimeCreated = row.TimeCreated.AddSeconds(1),
                Type = nameof(EventType.GroupPolicyDirectoryAudit),
                EventId = 5136,
                RecordId = 43,
                Provider = row.Provider,
                SourceLog = row.SourceLog,
                ContainerLog = row.ContainerLog,
                SourceComputer = row.SourceComputer,
                CollectorComputer = row.CollectorComputer,
                Values = new Dictionary<string, object?>()
            };
            var store = new EventStore(path);
            await store.WriteAsync(EventReportEngine.CreateStored(
                new[] { row, rawRow },
                new[] {
                    EventReportSectionSchema.FromGroupPolicyAudit(),
                    EventReportSectionSchema.FromType(EventType.GroupPolicyDirectoryAudit)
                }));

            EventReport matched = await store.ReadReportAsync(new EventStoreQuery {
                Types = new[] { EventType.GroupPolicyDirectoryAudit },
                Predicate = EventPredicate.Compare("Actor", EventPredicateOperator.Equal, "AD\\alice")
            });
            ArgumentException rejected = await Assert.ThrowsAsync<ArgumentException>(() => store.ReadReportAsync(
                new EventStoreQuery {
                    Types = new[] { EventType.GroupPolicyDirectoryAudit },
                    Predicate = EventPredicate.Compare("Who", EventPredicateOperator.Equal, "alice")
                }));

            Assert.Single(matched.Rows);
            Assert.Contains("not available", rejected.Message, StringComparison.OrdinalIgnoreCase);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public void BuiltInStoreQueriesNormalizePredicatesBeforePlanning() {
        var query = new EventStoreQuery {
            Types = new[] { EventType.ADUserLogonFailed },
            Predicate = EventPredicate.Compare(
                "Who",
                EventPredicateOperator.GreaterThan,
                "M")
        };

        ArgumentException exception = Assert.Throws<ArgumentException>(() => EventStore.Plan(query));

        Assert.Contains("Operator 'GreaterThan' is not supported by field 'Who'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StoreQueriesRejectMixedDefinitionSelectorFamilies() {
        var query = new EventStoreQuery {
            Types = new[] { EventType.ADUserLogonFailed },
            DefinitionNames = new[] { "CustomLogon" }
        };

        ArgumentException exception = Assert.Throws<ArgumentException>(() => EventStore.Plan(query));

        Assert.Contains("Types and DefinitionNames are mutually exclusive", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StoreQueriesNormalizeMixedDateTimeKindsBeforeValidatingBounds() {
        string path = CreateStorePath();
        DateTime local = DateTime.SpecifyKind(
            new DateTime(2026, 1, 15, 10, 0, 0),
            DateTimeKind.Local);
        DateTime endUtc = local.ToUniversalTime().AddMinutes(30);
        try {
            var store = new EventStore(path);
            await store.WriteAsync(CreateReport((
                local.ToUniversalTime().AddMinutes(15),
                42,
                "alice")));

            EventReport report = await store.ReadReportAsync(new EventStoreQuery {
                StartTime = local,
                EndTime = endUtc
            });

            Assert.Single(report.Rows);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task WriteIsIdempotentAndCommitsCheckpointWithRows() {
        string path = CreateStorePath();
        try {
            EventReport report = CreateReport(
                (new DateTime(2026, 7, 31, 23, 0, 0, DateTimeKind.Utc), 41, "alice"),
                (new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc), 42, "bob"));
            var store = new EventStore(path);
            var checkpoint = new EventStoreCheckpoint {
                Consumer = "weekly-report",
                Computer = "WEC01",
                Container = "ForwardedEvents",
                RecordId = 42,
                BookmarkXml = "<Bookmark/>"
            };

            EventStoreWriteResult first = await store.WriteAsync(report, checkpoint);
            EventStoreWriteResult second = await store.WriteAsync(report, checkpoint);
            EventStoreCheckpoint saved = Assert.IsType<EventStoreCheckpoint>(
                await store.GetCheckpointAsync("weekly-report", "WEC01", "ForwardedEvents"));

            Assert.Equal(2, first.Inserted);
            Assert.Equal(0, second.Inserted);
            Assert.Equal(2, second.Duplicates);
            Assert.True(first.CheckpointCommitted);
            Assert.Equal(42, saved.RecordId);
            Assert.Equal("<Bookmark/>", saved.BookmarkXml);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task CheckpointCompareAndSwapRejectsARegressingConcurrentWriter() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            EventReport initialReport = CreateReport((
                new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc),
                100,
                "initial"));
            await store.WriteAsync(initialReport, new EventStoreCheckpoint {
                Consumer = "collector",
                Computer = "WEC01",
                Container = "ForwardedEvents",
                RecordId = 100,
                BookmarkXml = "<Bookmark RecordId='100'/>"
            }, expectedCheckpoint: null);
            EventStoreCheckpoint observed = Assert.IsType<EventStoreCheckpoint>(
                await store.GetCheckpointAsync(
                    "collector",
                    "WEC01",
                    "ForwardedEvents"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.WriteAsync(
                CreateReport((
                    new DateTime(2026, 8, 1, 1, 15, 0, DateTimeKind.Utc),
                    125,
                    "late-first-writer")),
                new EventStoreCheckpoint {
                    Consumer = "collector",
                    Computer = "WEC01",
                    Container = "ForwardedEvents",
                    RecordId = 125,
                    BookmarkXml = "<Bookmark RecordId='125'/>"
                },
                expectedCheckpoint: null));
            await store.WriteAsync(
                CreateReport((
                    new DateTime(2026, 8, 1, 2, 0, 0, DateTimeKind.Utc),
                    200,
                    "newer")),
                new EventStoreCheckpoint {
                    Consumer = "collector",
                    Computer = "WEC01",
                    Container = "ForwardedEvents",
                    RecordId = 200,
                    BookmarkXml = "<Bookmark RecordId='200'/>"
                },
                observed);

            await Assert.ThrowsAsync<InvalidOperationException>(() => store.WriteAsync(
                CreateReport((
                    new DateTime(2026, 8, 1, 1, 30, 0, DateTimeKind.Utc),
                    150,
                    "stale")),
                new EventStoreCheckpoint {
                    Consumer = "collector",
                    Computer = "WEC01",
                    Container = "ForwardedEvents",
                    RecordId = 150,
                    BookmarkXml = "<Bookmark RecordId='150'/>"
                },
                observed));
            EventStoreCheckpoint current = Assert.IsType<EventStoreCheckpoint>(
                await store.GetCheckpointAsync(
                    "collector",
                    "WEC01",
                    "ForwardedEvents"));
            EventReport rows = await store.ReadReportAsync(new EventStoreQuery());

            Assert.Equal(200, current.RecordId);
            Assert.Equal("<Bookmark RecordId='200'/>", current.BookmarkXml);
            Assert.DoesNotContain(
                rows.Rows,
                row => Equals(row.Values["User"], "late-first-writer"));
            Assert.DoesNotContain(rows.Rows, row => Equals(row.Values["User"], "stale"));
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task CheckpointIdentityUsesOrdinalIgnoreCaseForUnicodeDimensions() {
        string path = CreateStorePath();
        try {
            EventReport report = CreateReport(
                (new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc), 42, "alice"));
            var store = new EventStore(path);
            await store.WriteAsync(report, new EventStoreCheckpoint {
                Consumer = "Überwachung",
                Computer = "München-DC",
                Container = "Sicherheit",
                RecordId = 42
            });
            await store.WriteAsync(report, new EventStoreCheckpoint {
                Consumer = "überwachung",
                Computer = "münchen-dc",
                Container = "sicherheit",
                RecordId = 84
            });

            EventStoreCheckpoint saved = Assert.IsType<EventStoreCheckpoint>(
                await store.GetCheckpointAsync("ÜBERWACHUNG", "MÜNCHEN-DC", "SICHERHEIT"));
            using var sqlite = new SQLite();
            using SQLiteSession session = sqlite.OpenSession(path);

            Assert.Equal(84, saved.RecordId);
            Assert.Equal(1L, Convert.ToInt64(
                session.ExecuteScalar("SELECT COUNT(*) FROM evx_checkpoints;"),
                CultureInfo.InvariantCulture));
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task DeleteCheckpointKeepsStoredEventsAndUsesCanonicalIdentity() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            await store.WriteAsync(
                CreateReport((
                    new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc),
                    42,
                    "alice")),
                new EventStoreCheckpoint {
                    Consumer = "Überwachung",
                    Computer = "München-DC",
                    Container = "ForwardedEvents",
                    RecordId = 42
                });

            bool deleted = await store.DeleteCheckpointAsync(
                "überwachung",
                "münchen-dc",
                "forwardedevents");
            EventReport remaining = await store.ReadReportAsync(
                new EventStoreQuery());

            Assert.True(deleted);
            Assert.Null(await store.GetCheckpointAsync(
                "Überwachung",
                "München-DC",
                "ForwardedEvents"));
            Assert.Single(remaining.Rows);
        } finally {
            DeleteStore(path);
        }
    }

    [Theory]
    [InlineData(100, 101, 200)]
    [InlineData(100, 100, 100)]
    [InlineData(100, 50, 200)]
    public void CheckpointAcceptsContiguousRetainedRanges(
        long checkpoint,
        long oldest,
        long newest) {

        new EventStoreCheckpoint { RecordId = checkpoint }
            .ValidateAvailableRange(oldest, newest);
    }

    [Theory]
    [InlineData(100L, 102L, 200L)]
    [InlineData(200L, 1L, 199L)]
    [InlineData(100L, 200L, 100L)]
    public void CheckpointRejectsClearedOrIncompleteRetainedRanges(
        long checkpoint,
        long oldest,
        long newest) {

        Assert.Throws<InvalidDataException>(() =>
            new EventStoreCheckpoint { RecordId = checkpoint }
                .ValidateAvailableRange(oldest, newest));
    }

    [Fact]
    public void CheckpointRejectsAnEmptyRetainedRange() {
        Assert.Throws<InvalidDataException>(() =>
            new EventStoreCheckpoint { RecordId = 100 }
                .ValidateAvailableRange(null, null));
    }

    [Fact]
    public async Task ExistingUnicodeCheckpointDuplicatesCoalesceDuringInitialization() {
        string path = CreateStorePath();
        try {
            new EventStore(path).Initialize();
            using (var sqlite = new SQLite()) {
                using SQLiteSession session = sqlite.OpenSession(path);
                session.ExecuteNonQuery(
                    @"INSERT INTO evx_checkpoints
                      (consumer, computer, container, record_id, bookmark_xml, updated_utc)
                      VALUES
                      ('Überwachung', 'München-DC', 'Sicherheit', 42, '<Old/>', '2026-08-01T01:00:00.0000000Z'),
                      ('überwachung', 'münchen-dc', 'sicherheit', 84, '<New/>', '2026-08-01T02:00:00.0000000Z');");
            }

            var migrated = new EventStore(path);
            EventStoreCheckpoint saved = Assert.IsType<EventStoreCheckpoint>(
                await migrated.GetCheckpointAsync("ÜBERWACHUNG", "MÜNCHEN-DC", "SICHERHEIT"));
            using var verificationSqlite = new SQLite();
            using SQLiteSession verification = verificationSqlite.OpenSession(path);

            Assert.Equal(84, saved.RecordId);
            Assert.Equal("<New/>", saved.BookmarkXml);
            Assert.Equal(1L, Convert.ToInt64(
                verification.ExecuteScalar("SELECT COUNT(*) FROM evx_checkpoints;"),
                CultureInfo.InvariantCulture));
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task OriginalEventIdentityDeduplicatesDirectAndCollectorTransport() {
        string path = CreateStorePath();
        try {
            DateTime time = new(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc);
            var store = new EventStore(path);
            EventStoreWriteResult forwarded = await store.WriteAsync(
                CreateReportFromTransport(time, 42, "alice", "WEC01", "ForwardedEvents"));
            EventStoreWriteResult direct = await store.WriteAsync(
                CreateReportFromTransport(time, 9001, "alice", "source.ad.evotec.xyz", "Security"));
            EventStoreWriteResult distinct = await store.WriteAsync(
                CreateReportFromTransport(time, 9002, "bob", "source.ad.evotec.xyz", "Security"));

            EventReport report = await store.ReadReportAsync(new EventStoreQuery());

            Assert.Equal(1, forwarded.Inserted);
            Assert.Equal(0, direct.Inserted);
            Assert.Equal(1, direct.Duplicates);
            Assert.Equal(1, distinct.Inserted);
            Assert.Equal(2, report.Rows.Count);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task SameTransportRecordsWithDistinctRecordIdsRemainDistinct() {
        string path = CreateStorePath();
        try {
            DateTime time = new(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc);
            var store = new EventStore(path);
            EventReport report = CreateReportFromTransport(
                new[] {
                    (time, 42L, "alice"),
                    (time, 43L, "alice")
                },
                "WEC01",
                "ForwardedEvents");

            EventStoreWriteResult result = await store.WriteAsync(report);
            EventReport stored = await store.ReadReportAsync(new EventStoreQuery { Oldest = true });

            Assert.Equal(2, result.Inserted);
            Assert.Equal(new long?[] { 42, 43 }, stored.Rows.Select(static row => row.RecordId));
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task OriginalEventIdentityDeduplicatesDifferentCollectors() {
        string path = CreateStorePath();
        try {
            DateTime time = new(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc);
            var store = new EventStore(path);
            EventStoreWriteResult first = await store.WriteAsync(
                CreateReportFromTransport(time, 42, "alice", "WEC01", "ForwardedEvents"));
            EventStoreWriteResult second = await store.WriteAsync(
                CreateReportFromTransport(time, 9001, "alice", "WEC02", "ForwardedEvents"));

            EventReport stored = await store.ReadReportAsync(new EventStoreQuery());

            Assert.Equal(1, first.Inserted);
            Assert.Equal(0, second.Inserted);
            Assert.Equal(1, second.Duplicates);
            Assert.Single(stored.Rows);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task OfflineTransportWithoutEvtxExtensionIsNotSuppressedByCollectorDeduplication() {
        string path = CreateStorePath();
        try {
            DateTime time = new(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc);
            var store = new EventStore(path);
            EventReport collector = CreateReportFromTransport(
                time,
                42,
                "alice",
                "WEC01",
                "ForwardedEvents");
            EventReport offline = CreateReportFromTransport(
                time,
                42,
                "alice",
                "renamed-event-archive",
                "renamed-event-archive");
            offline.Rows[0].SourceKind = EventLogQuerySourceKind.File;

            EventStoreWriteResult forwarded = await store.WriteAsync(collector);
            EventStoreWriteResult archived = await store.WriteAsync(offline);

            Assert.Equal(1, forwarded.Inserted);
            Assert.Equal(1, archived.Inserted);
            Assert.Equal(2, (await store.ReadReportAsync(new EventStoreQuery())).Rows.Count);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task OriginalEventIdentityDeduplicatesSelfForwardedCollectorCopies() {
        string path = CreateStorePath();
        try {
            DateTime time = new(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc);
            var store = new EventStore(path);
            EventStoreWriteResult forwarded = await store.WriteAsync(
                CreateReportFromTransport(
                    time,
                    42,
                    "alice",
                    "source.ad.evotec.xyz",
                    "ForwardedEvents"));
            EventStoreWriteResult direct = await store.WriteAsync(
                CreateReportFromTransport(
                    time,
                    9001,
                    "alice",
                    "source.ad.evotec.xyz",
                    "Security"));

            EventReport stored = await store.ReadReportAsync(new EventStoreQuery());

            Assert.Equal(1, forwarded.Inserted);
            Assert.Equal(0, direct.Inserted);
            Assert.Equal(1, direct.Duplicates);
            Assert.Single(stored.Rows);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task EventIdentityUsesTheSameCaseInsensitiveSemanticsAsStoredDimensions() {
        string path = CreateStorePath();
        try {
            DateTime time = new(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc);
            var store = new EventStore(path);
            EventReport original = CreateReportForDefinition(
                "StoredLogon",
                (time, 42, "alice"));
            EventReport alternateCase = CreateReportForDefinition(
                "storedlogon",
                (time, 42, "alice"));
            EventReportRow alternateRow = Assert.Single(alternateCase.Rows);
            alternateRow.SourceComputer = alternateRow.SourceComputer.ToUpperInvariant();
            alternateRow.SourceLog = alternateRow.SourceLog.ToLowerInvariant();
            alternateRow.Provider = alternateRow.Provider.ToLowerInvariant();

            EventStoreWriteResult first = await store.WriteAsync(original);
            EventStoreWriteResult second = await store.WriteAsync(alternateCase);

            Assert.Equal(1, first.Inserted);
            Assert.Equal(0, second.Inserted);
            Assert.Equal(1, second.Duplicates);
            Assert.Single((await store.ReadReportAsync(new EventStoreQuery())).Rows);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task EventIdentityDoesNotFoldValuesThatSqliteNoCaseKeepsDistinct() {
        string path = CreateStorePath();
        try {
            DateTime time = new(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc);
            var store = new EventStore(path);
            EventReport upperUnicode = CreateReportForDefinition(
                "StoredLogon",
                (time, 42, "alice"));
            EventReport lowerUnicode = CreateReportForDefinition(
                "StoredLogon",
                (time, 42, "alice"));
            EventReport trailingSpace = CreateReportForDefinition(
                "StoredLogon",
                (time, 42, "alice"));
            Assert.Single(upperUnicode.Rows).Provider = "MÜNCHEN";
            Assert.Single(lowerUnicode.Rows).Provider = "München";
            EventReportRow spacedRow = Assert.Single(trailingSpace.Rows);
            spacedRow.Provider = "MÜNCHEN";
            spacedRow.SourceComputer += " ";

            EventStoreWriteResult first = await store.WriteAsync(upperUnicode);
            EventStoreWriteResult second = await store.WriteAsync(lowerUnicode);
            EventStoreWriteResult third = await store.WriteAsync(trailingSpace);

            Assert.Equal(1, first.Inserted);
            Assert.Equal(1, second.Inserted);
            Assert.Equal(1, third.Inserted);
            Assert.Equal(3, (await store.ReadReportAsync(new EventStoreQuery())).Rows.Count);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task ReadUsesTypedPredicatesAndRetainsHomogeneousSchema() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            await store.WriteAsync(CreateReport(
                (new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc), 42, "alice"),
                (new DateTime(2026, 8, 1, 2, 0, 0, DateTimeKind.Utc), 43, "bob")));

            EventReport report = await store.ReadReportAsync(new EventStoreQuery {
                DefinitionNames = new[] { "StoredLogon" },
                Predicate = EventPredicate.Compare("User", EventPredicateOperator.Equal, "bob")
            });

            EventReportRow row = Assert.Single(report.Rows);
            Assert.Equal("bob", row.Values["User"]);
            EventReportSection section = Assert.Single(report.Sections);
            Assert.Equal(EventReportSectionKind.Custom, section.Kind);
            Assert.Equal(new[] { "User", "Computer" }, section.Columns.Select(static column => column.Name));
            Assert.DoesNotContain(section.Columns, static column => column.Name == nameof(EventReportRow.EventId));
            Assert.Equal(2, report.EventsScanned);
            Assert.False(report.ScanLimitReached);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task ReadRehydratesValuesFromDeclaredSchemaTypes() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            await store.WriteAsync(CreateTypedValueReport());

            EventReport report = await store.ReadReportAsync(new EventStoreQuery {
                Predicate = EventPredicate.Compare(
                    "IsoText",
                    EventPredicateOperator.Equal,
                    "2026-08-20T10:00:00Z")
            });

            EventReportRow row = Assert.Single(report.Rows);
            Assert.IsType<string>(row.Values["IsoText"]);
            Assert.Equal("2026-08-20T10:00:00Z", row.Values["IsoText"]);
            Assert.IsType<int>(row.Values["AttemptCount"]);
            Assert.Equal(7, row.Values["AttemptCount"]);
            Assert.IsType<DateTime>(row.Values["OccurredAt"]);
            Assert.Equal(
                new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc),
                ((DateTime)row.Values["OccurredAt"]!).ToUniversalTime());
            Assert.Equal(
                System.Net.IPAddress.Parse("2001:db8::1"),
                Assert.IsType<System.Net.IPAddress>(row.Values["ClientAddress"]));
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task StoredPredicatesUseLiveCommonFieldNamesAndNumericLevel() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            await store.WriteAsync(CreateReport(
                (new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc), 42, "alice")));
            EventPredicate predicate = EventPredicate.AllOf(
                EventPredicate.Compare("ProviderName", EventPredicateOperator.Equal,
                    "microsoft-windows-security-auditing"),
                EventPredicate.Compare("SourceLogName", EventPredicateOperator.Equal, "security"),
                EventPredicate.Compare("MachineName", EventPredicateOperator.Equal, "SOURCE.AD.EVOTEC.XYZ"),
                EventPredicate.Compare("TypeName", EventPredicateOperator.Equal, "StoredLogon"),
                EventPredicate.Compare("Level", EventPredicateOperator.Equal, 0));

            EventReport report = await store.ReadReportAsync(new EventStoreQuery {
                Predicate = predicate
            });
            EventReport direct = await store.ReadReportAsync(new EventStoreQuery {
                SourceComputers = new[] { "SOURCE.AD.EVOTEC.XYZ" },
                SourceLogs = new[] { "security" },
                Providers = new[] { "microsoft-windows-security-auditing" }
            });
            EventPredicate caseSensitiveProvider = EventPredicate.Compare(
                "ProviderName",
                EventPredicateOperator.Equal,
                "microsoft-windows-security-auditing");
            caseSensitiveProvider.IgnoreCase = false;
            EventReport caseSensitive = await store.ReadReportAsync(new EventStoreQuery {
                Predicate = caseSensitiveProvider
            });

            EventReportRow row = Assert.Single(report.Rows);
            Assert.Equal((byte)0, row.LevelValue);
            Assert.Equal("Information", row.Level);
            Assert.Single(direct.Rows);
            Assert.Empty(caseSensitive.Rows);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task StoredTypedFieldsCanShadowNativeColumnsWhileGenericAliasesStayAuthoritative() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            await store.WriteAsync(CreateShadowedProviderReport(
                new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc),
                42));

            EventReport customMatch = await store.ReadReportAsync(new EventStoreQuery {
                DefinitionNames = new[] { "StoredShadowedProvider" },
                Predicate = EventPredicate.Compare(
                    "ProviderName",
                    EventPredicateOperator.Equal,
                    "custom-provider")
            });
            EventReport nativeValueDoesNotMatch = await store.ReadReportAsync(new EventStoreQuery {
                DefinitionNames = new[] { "StoredShadowedProvider" },
                Predicate = EventPredicate.Compare(
                    "ProviderName",
                    EventPredicateOperator.Equal,
                    "Microsoft-Windows-Security-Auditing")
            });
            EventStoreSummaryResult summary = await store.SummarizeAsync(
                new EventStoreQuery {
                    DefinitionNames = new[] { "StoredShadowedProvider" },
                    Predicate = EventPredicate.Compare(
                        "ProviderName",
                        EventPredicateOperator.Equal,
                        "custom-provider")
                },
                EventStoreSummaryPeriod.Day);
            await store.WriteAsync(CreateGenericReport(
                new DateTime(2026, 8, 1, 2, 0, 0, DateTimeKind.Utc),
                43,
                "ProviderName",
                "generic-provider"));
            EventReport genericAliasDoesNotMatch = await store.ReadReportAsync(new EventStoreQuery {
                DefinitionNames = new[] { "Generic" },
                Predicate = EventPredicate.Compare(
                    "ProviderName",
                    EventPredicateOperator.Equal,
                    "generic-provider")
            });
            EventReport genericNativeMatch = await store.ReadReportAsync(new EventStoreQuery {
                DefinitionNames = new[] { "Generic" },
                Predicate = EventPredicate.Compare(
                    "ProviderName",
                    EventPredicateOperator.Equal,
                    "Microsoft-Windows-Security-Auditing")
            });

            EventReportRow row = Assert.Single(customMatch.Rows);
            Assert.Equal("custom-provider", row.Values["ProviderName"]);
            Assert.Empty(nativeValueDoesNotMatch.Rows);
            Assert.Equal(1, Assert.Single(summary.Rows).Count);
            Assert.Empty(genericAliasDoesNotMatch.Rows);
            Assert.Equal(
                "generic-provider",
                Assert.Single(genericNativeMatch.Rows).Values["ProviderName"]);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task UnlimitedCandidateScanAndContradictoryPredicateStayExact() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            await store.WriteAsync(CreateReport(
                (new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc), 42, "alice"),
                (new DateTime(2026, 8, 1, 2, 0, 0, DateTimeKind.Utc), 43, "bob")));

            EventReport unlimited = await store.ReadReportAsync(new EventStoreQuery {
                Predicate = EventPredicate.Compare("User", EventPredicateOperator.IsNotNull),
                MaxCandidates = 0,
                Oldest = true
            });
            EventReport contradictory = await store.ReadReportAsync(new EventStoreQuery {
                Predicate = EventPredicate.AllOf(
                    EventPredicate.Compare("EventId", EventPredicateOperator.Equal, 4624),
                    EventPredicate.Compare("EventId", EventPredicateOperator.Equal, 4625))
            });

            Assert.Equal(2, unlimited.Rows.Count);
            Assert.Equal(2, unlimited.EventsScanned);
            Assert.False(unlimited.ScanLimitReached);
            Assert.Empty(contradictory.Rows);
            Assert.Equal(2, contradictory.EventsScanned);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public void StoredSchemaTypeNamesAreRuntimeNeutralAndResolvable() {
        Type[] values = {
            typeof(string),
            typeof(int?),
            typeof(DateTime[]),
            typeof(Dictionary<string, List<int?[]>>)
        };

        foreach (Type value in values) {
            string stableName = EventReportColumnSchema.GetStableTypeName(value);
            Assert.DoesNotContain("mscorlib", stableName, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("System.Private.CoreLib", stableName, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Version=", stableName, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(value, EventReportColumnSchema.ResolveValueTypeName(stableName));
        }

        const string privateCoreString =
            "System.String, System.Private.CoreLib, Version=8.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e";
        const string mscorlibNullable =
            "System.Nullable`1[[System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]], mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";
        Assert.Equal(typeof(string), EventReportColumnSchema.ResolveValueTypeName(privateCoreString));
        Assert.Equal(typeof(int?), EventReportColumnSchema.ResolveValueTypeName(mscorlibNullable));
        Assert.Equal("System.String", EventReportColumnSchema.NormalizeValueTypeName(privateCoreString));
    }

    [Fact]
    public async Task LegacyRuntimeSpecificSchemaIsMigratedWithoutPruningRows() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            await store.WriteAsync(CreateReport(
                (new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc), 42, "alice")));
            const string privateCoreString =
                "System.String, System.Private.CoreLib, Version=8.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e";
            using (var sqlite = new SQLite { BusyTimeoutMs = 10000 }) {
                using SQLiteSession session = sqlite.OpenSession(path);
                string schemaJson = Convert.ToString(session.ExecuteScalar(
                    "SELECT schema_json FROM evx_definitions WHERE definition_name = 'StoredLogon';"),
                    System.Globalization.CultureInfo.InvariantCulture)!;
                string legacyJson = schemaJson.Replace("System.String", privateCoreString);
                session.ExecuteNonQuery(
                    "UPDATE evx_definitions SET schema_hash = $hash, schema_json = $schema WHERE definition_name = 'StoredLogon';",
                    new Dictionary<string, object?> {
                        ["$hash"] = "legacy-runtime-specific-hash",
                        ["$schema"] = legacyJson
                    });
            }

            EventStoreWriteResult migrated = await new EventStore(path).WriteAsync(CreateReport(
                (new DateTime(2026, 8, 1, 2, 0, 0, DateTimeKind.Utc), 43, "bob")));
            EventReport report = await new EventStore(path).ReadReportAsync(new EventStoreQuery { Oldest = true });
            string storedSchema;
            using (var sqlite = new SQLite { BusyTimeoutMs = 10000 }) {
                using SQLiteSession session = sqlite.OpenSession(path);
                storedSchema = Convert.ToString(session.ExecuteScalar(
                    "SELECT schema_json FROM evx_definitions WHERE definition_name = 'StoredLogon';"),
                    System.Globalization.CultureInfo.InvariantCulture)!;
            }

            Assert.Equal(1, migrated.Inserted);
            Assert.Equal(2, report.Rows.Count);
            Assert.DoesNotContain("System.Private.CoreLib", storedSchema, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Version=", storedSchema, StringComparison.OrdinalIgnoreCase);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task CompositeTypeQueriesExpandToStoredLeafDefinitions() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            await store.WriteAsync(CreateBuiltInReportForDefinition(
                "ADUserLogonFailed",
                (new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc), 42, "alice")));
            await store.WriteAsync(CreateBuiltInReportForDefinition(
                "ADUserLogon",
                (new DateTime(2026, 8, 1, 2, 0, 0, DateTimeKind.Utc), 43, "bob")));

            EventReport report = await store.ReadReportAsync(new EventStoreQuery {
                Types = new[] { EventType.ActiveDirectoryAuthentication },
                Oldest = true
            });

            Assert.Equal(2, report.Rows.Count);
            Assert.Equal(
                new[] { "ADUserLogonFailed", "ADUserLogon" },
                report.Rows.Select(static row => row.Type));
            Assert.Equal(2, report.Sections.Count);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task CandidateLimitIsExplicitAndNeverCheckpointedByRead() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            await store.WriteAsync(CreateReport(
                (new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc), 42, "alice"),
                (new DateTime(2026, 8, 1, 2, 0, 0, DateTimeKind.Utc), 43, "bob")));

            EventReport report = await store.ReadReportAsync(new EventStoreQuery {
                Predicate = EventPredicate.Compare("User", EventPredicateOperator.Equal, "nobody"),
                MaxCandidates = 1,
                Oldest = true
            });

            Assert.Empty(report.Rows);
            Assert.Equal(1, report.EventsScanned);
            Assert.True(report.ScanLimitReached);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task ManagedStoredReadsProbeOneAdditionalMatchWithoutMaterializingLaterRows() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            await store.WriteAsync(CreateReport(Enumerable.Range(1, 300)
                .Select(index => (
                    new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(index),
                    (long)index,
                    $"user-{index}"))
                .ToArray()));
            using (var sqlite = new SQLite { BusyTimeoutMs = 10000 }) {
                using SQLiteSession session = sqlite.OpenSession(path);
                session.ExecuteNonQuery(
                    "UPDATE evx_events SET values_json = '{' WHERE record_id = 257;");
            }

            EventReport report = await store.ReadReportAsync(new EventStoreQuery {
                DefinitionNames = new[] { "StoredLogon" },
                Predicate = EventPredicate.Compare("User", EventPredicateOperator.IsNotNull),
                MaxEvents = 1,
                Oldest = true
            });

            Assert.Equal("user-1", Assert.Single(report.Rows).Values["User"]);
            Assert.Equal(2, report.EventsScanned);
            Assert.True(report.ScanLimitReached);
            Assert.Contains("stored result limit", report.CompletenessDiagnostic, StringComparison.OrdinalIgnoreCase);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task StoredReadsAcceptLongMaxValueAsAnEffectivelyUnlimitedResultBound() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            await store.WriteAsync(CreateReport(
                (new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc), 42, "alice"),
                (new DateTime(2026, 8, 1, 2, 0, 0, DateTimeKind.Utc), 43, "bob")));

            EventReport report = await store.ReadReportAsync(new EventStoreQuery {
                MaxEvents = long.MaxValue,
                Oldest = true
            });

            Assert.Equal(2, report.Rows.Count);
            Assert.False(report.ScanLimitReached);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task CalendarSummariesUseSqlFastPathAndPruneSelectively() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            await store.WriteAsync(CreateReport(
                (new DateTime(2026, 7, 31, 23, 0, 0, DateTimeKind.Utc), 41, "alice"),
                (new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc), 42, "bob"),
                (new DateTime(2026, 8, 3, 1, 0, 0, DateTimeKind.Utc), 43, "carol")));

            EventStoreSummaryResult monthly = await store.SummarizeAsync(
                new EventStoreQuery(),
                EventStoreSummaryPeriod.Month);
            Assert.Equal(2, monthly.Rows.Count);
            Assert.Equal(3, monthly.EventsScanned);
            Assert.Equal(new[] { 1L, 2L }, monthly.Rows.Select(static row => row.Count));

            int deleted = await store.PruneBeforeAsync(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
            EventReport remaining = await store.ReadReportAsync(new EventStoreQuery());
            Assert.Equal(1, deleted);
            Assert.Equal(2, remaining.Rows.Count);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task PruneNormalizesAsciiDefinitionSelectorsAndRejectsEmptySelections() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            await store.WriteAsync(CreateReportForDefinition(
                "Audit",
                (new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc), 42, "alice")));
            await store.WriteAsync(CreateReportForDefinition(
                "OtherAudit",
                (new DateTime(2026, 8, 1, 2, 0, 0, DateTimeKind.Utc), 43, "bob")));

            int deleted = await store.PruneBeforeAsync(
                new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc),
                new[] { " Audit ", "AUDIT" });
            int emptyDeleted = await store.PruneBeforeAsync(
                new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc),
                new[] { " ", "\t" });
            EventReport remaining = await store.ReadReportAsync(new EventStoreQuery());

            Assert.Equal(1, deleted);
            Assert.Equal(0, emptyDeleted);
            Assert.Equal("OtherAudit", Assert.Single(remaining.Rows).Type);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task CalendarSummariesRejectPartialResultLimitsAndReturnUtcBuckets() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            await store.WriteAsync(CreateReport(
                (new DateTime(2026, 8, 3, 1, 0, 0, DateTimeKind.Utc), 42, "alice")));

            ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() => store.SummarizeAsync(
                new EventStoreQuery { MaxEvents = 1 },
                EventStoreSummaryPeriod.Day));
            EventStoreSummaryResult weekly = await store.SummarizeAsync(
                new EventStoreQuery {
                    Predicate = EventPredicate.Compare("User", EventPredicateOperator.Equal, "alice")
                },
                EventStoreSummaryPeriod.Week);

            Assert.Contains("exhaustive", exception.Message, StringComparison.OrdinalIgnoreCase);
            EventStoreSummaryRow row = Assert.Single(weekly.Rows);
            Assert.Equal(DateTimeKind.Utc, row.PeriodStartUtc.Kind);
            Assert.Equal(new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc), row.PeriodStartUtc);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task GenericSchemasCanEvolveWithObservedEventData() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            await store.WriteAsync(CreateGenericReport(
                new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc),
                42,
                "FirstField",
                "one"));
            await store.WriteAsync(CreateGenericReport(
                new DateTime(2026, 8, 1, 2, 0, 0, DateTimeKind.Utc),
                43,
                "SecondField",
                "two"));

            EventReport report = await store.ReadReportAsync(new EventStoreQuery { Oldest = true });

            Assert.Equal(2, report.Rows.Count);
            EventReportSection section = Assert.Single(report.Sections);
            Assert.Equal(EventReportSectionKind.Generic, section.Kind);
            Assert.Contains(section.Columns, static column => column.Name == "FirstField");
            Assert.Contains(section.Columns, static column => column.Name == "SecondField");
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task IncompatibleSchemaRollsBackRowsAndCheckpoint() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            await store.WriteAsync(CreateReport(
                (new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc), 42, "alice")));
            EventReport incompatible = CreateReportWithChangedSchema(
                new DateTime(2026, 8, 1, 2, 0, 0, DateTimeKind.Utc),
                43);
            var checkpoint = new EventStoreCheckpoint {
                Consumer = "schema-race",
                Computer = "WEC01",
                Container = "ForwardedEvents",
                RecordId = 43
            };

            await Assert.ThrowsAsync<InvalidDataException>(() => store.WriteAsync(incompatible, checkpoint));
            EventReport remaining = await store.ReadReportAsync(new EventStoreQuery());
            EventStoreCheckpoint? saved = await store.GetCheckpointAsync(
                "schema-race", "WEC01", "ForwardedEvents");

            Assert.Single(remaining.Rows);
            Assert.Null(saved);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task ConcurrentWritersRemainIdempotent() {
        string path = CreateStorePath();
        try {
            EventReport report = CreateReport(
                (new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc), 42, "alice"),
                (new DateTime(2026, 8, 1, 2, 0, 0, DateTimeKind.Utc), 43, "bob"));
            EventStoreWriteResult[] results = await Task.WhenAll(
                Enumerable.Range(0, 8).Select(_ => new EventStore(path).WriteAsync(report)));

            EventReport stored = await new EventStore(path).ReadReportAsync(new EventStoreQuery());

            Assert.Equal(2, results.Sum(static result => result.Inserted));
            Assert.Equal(2, stored.Rows.Count);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public void StoredPlanDistinguishesSqlPrefilterAndManagedVerification() {
        EventStoreQueryPlan plan = EventStore.Plan(new EventStoreQuery {
            EventIds = new[] { 4624 },
            Predicate = EventPredicate.AllOf(
                EventPredicate.Compare("Provider", EventPredicateOperator.Equal, "Security"),
                EventPredicate.Compare("User", EventPredicateOperator.Equal, "alice"))
        });

        Assert.True(plan.HasSqlPredicatePrefilter);
        Assert.True(plan.HasManagedVerification);
        Assert.Contains(plan.Steps, static step =>
            step.Stage == EventStoreQueryPlanStage.Sql && step.Expression.Contains("EventId", StringComparison.Ordinal));
        Assert.Contains(plan.Steps, static step =>
            step.Stage == EventStoreQueryPlanStage.Managed && step.Expression.Contains("Provider", StringComparison.Ordinal));
        Assert.Contains(plan.Steps, static step =>
            step.Stage == EventStoreQueryPlanStage.Managed && step.Expression.Contains("User", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StoredPlanUsesTheSelectedSchemaPushdownPolicy() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            await store.WriteAsync(CreateReportForDefinition(
                "StoredLogon",
                (new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc), 42, "alice")));
            await store.WriteAsync(CreateGenericReport(
                new DateTime(2026, 8, 1, 2, 0, 0, DateTimeKind.Utc),
                43,
                "CustomValue",
                "two"));
            EventPredicate predicate = EventPredicate.Compare(
                "EventId",
                EventPredicateOperator.Equal,
                4624);

            EventStoreQueryPlan typed = await store.PlanAsync(new EventStoreQuery {
                DefinitionNames = new[] { "StoredLogon" },
                Predicate = predicate
            });
            EventStoreQueryPlan generic = await store.PlanAsync(new EventStoreQuery {
                DefinitionNames = new[] { "Generic" },
                Predicate = predicate
            });
            EventStoreQueryPlan conservative = EventStore.Plan(new EventStoreQuery {
                DefinitionNames = new[] { "StoredLogon" },
                Predicate = predicate
            });

            Assert.Contains(typed.Steps, static step =>
                step.Stage == EventStoreQueryPlanStage.Sql &&
                step.Expression.StartsWith("EventId ", StringComparison.Ordinal));
            Assert.Contains(generic.Steps, static step =>
                step.Stage == EventStoreQueryPlanStage.Managed &&
                step.Expression.StartsWith("EventId ", StringComparison.Ordinal));
            Assert.Contains(conservative.Steps, static step =>
                step.Stage == EventStoreQueryPlanStage.Managed &&
                step.Reason.Contains("PlanAsync", StringComparison.Ordinal));
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task UnicodeCaseInsensitiveSelectionsFallBackToExactManagedMatching() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            await store.WriteAsync(CreateReportFromTransport(
                new[] { (new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc), 91L, "alice") },
                "WEC01",
                "ForwardedEvents",
                definitionName: "MÜNCHEN-TYPE",
                providerName: "MÜNCHEN-PROVIDER"));
            EventStoreWriteResult duplicate = await store.WriteAsync(CreateReportFromTransport(
                new[] { (new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc), 91L, "alice") },
                "WEC01",
                "ForwardedEvents",
                definitionName: "münchen-type",
                providerName: "MÜNCHEN-PROVIDER"));

            EventReport direct = await store.ReadReportAsync(new EventStoreQuery {
                DefinitionNames = new[] { "münchen-type" },
                Providers = new[] { "münchen-provider" }
            });
            EventPredicate providerPredicate = EventPredicate.Compare(
                "ProviderName",
                EventPredicateOperator.Equal,
                "münchen-provider");
            EventReport predicate = await store.ReadReportAsync(new EventStoreQuery {
                Predicate = providerPredicate
            });
            EventStoreSummaryResult summary = await store.SummarizeAsync(new EventStoreQuery {
                DefinitionNames = new[] { "münchen-type" }
            }, EventStoreSummaryPeriod.Day);
            EventStoreQueryPlan plan = EventStore.Plan(new EventStoreQuery {
                Predicate = providerPredicate
            });

            Assert.Single(direct.Rows);
            Assert.Single(predicate.Rows);
            Assert.Equal(0, duplicate.Inserted);
            Assert.Equal(1, Assert.Single(summary.Rows).Count);
            Assert.Contains(plan.Steps, static step =>
                step.Stage == EventStoreQueryPlanStage.Managed &&
                step.Expression.Contains("Provider", StringComparison.OrdinalIgnoreCase));

            int pruned = await store.PruneBeforeAsync(
                new DateTime(2026, 8, 21, 0, 0, 0, DateTimeKind.Utc),
                new[] { "münchen-type" });
            Assert.Equal(1, pruned);
            Assert.Empty((await store.ReadReportAsync(new EventStoreQuery())).Rows);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task UnicodePruneFallbackProcessesCandidatesInMultipleBoundedPages() {
        string path = CreateStorePath();
        DateTime time = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);
        try {
            var store = new EventStore(path);
            var selected = Enumerable.Range(1, 513)
                .Select(index => (time.AddTicks(index), (long)index, "selected" + index))
                .ToArray();
            var retained = Enumerable.Range(514, 513)
                .Select(index => (time.AddTicks(index), (long)index, "retained" + index))
                .ToArray();
            await store.WriteAsync(CreateReportForDefinition("MÜNCHEN-TYPE", selected));
            await store.WriteAsync(CreateReportForDefinition("OtherType", retained));

            int pruned = await store.PruneBeforeAsync(
                time.AddDays(1),
                new[] { "münchen-type" });
            EventReport remaining = await store.ReadReportAsync(new EventStoreQuery());

            Assert.Equal(513, pruned);
            Assert.Equal(513, remaining.Rows.Count);
            Assert.All(remaining.Rows, static row => Assert.Equal("OtherType", row.Type));
        } finally {
            DeleteStore(path);
        }
    }

    private static EventReport CreateReport(params (DateTime Time, long RecordId, string User)[] events) {
        return CreateReportFromTransport(events, "WEC01", "ForwardedEvents");
    }

    private static EventReportRow CreateStoredActivityRow(
        long recordId,
        Guid? activityId,
        Guid? relatedActivityId) => new() {
        TimeCreated = new DateTime(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc).AddSeconds(recordId),
        Type = "Generic",
        EventId = 1000 + (int)recordId,
        RecordId = recordId,
        Provider = "Contoso-Activity-Provider",
        SourceLog = "Application",
        ContainerLog = "ForwardedEvents",
        SourceComputer = "source1.ad.evotec.xyz",
        CollectorComputer = "wec1.ad.evotec.xyz",
        ActivityId = activityId,
        RelatedActivityId = relatedActivityId,
        Values = new Dictionary<string, object?>()
    };

    private static EventReport CreateReportForDefinition(
        string definitionName,
        params (DateTime Time, long RecordId, string User)[] events) {

        return CreateReportFromTransport(events, "WEC01", "ForwardedEvents", definitionName);
    }

    private static EventReport CreateBuiltInReportForDefinition(
        string definitionName,
        params (DateTime Time, long RecordId, string User)[] events) {

        EventReportRow[] rows = events.Select(item => new EventReportRow {
            TimeCreated = item.Time,
            Type = definitionName,
            EventId = 4624,
            RecordId = item.RecordId,
            Provider = "Microsoft-Windows-Security-Auditing",
            SourceLog = "Security",
            ContainerLog = "ForwardedEvents",
            SourceComputer = "DC01.ad.evotec.xyz",
            CollectorComputer = "WEC01",
            Level = "Information",
            LevelValue = 0,
            Message = "Synthetic stored typed event.",
            Values = new Dictionary<string, object?>()
        }).ToArray();
        EventType type = Enum.Parse<EventType>(definitionName, ignoreCase: true);
        return EventReportEngine.CreateStored(rows, new[] {
            EventReportSectionSchema.FromType(type)
        });
    }

    private static EventReport CreateReportFromTransport(
        DateTime time,
        long recordId,
        string user,
        string queriedMachine,
        string container) => CreateReportFromTransport(
            new[] { (time, recordId, user) },
            queriedMachine,
            container);

    private static EventReport CreateReportFromTransport(
        IReadOnlyList<(DateTime Time, long RecordId, string User)> events,
        string queriedMachine,
        string container,
        string definitionName = "StoredLogon",
        string providerName = "Microsoft-Windows-Security-Auditing") {

        EventDefinition definition = new() {
            Name = definitionName,
            DisplayName = "Stored logons",
            Description = "Stored logon test events.",
            Sources = new[] {
                new EventDefinitionSource { LogName = "Security", EventIds = new[] { 4624 } }
            },
            Fields = new[] {
                new EventDefinitionField { Name = "User", Source = EventFieldSource.Data, SourceName = "TargetUserName" },
                new EventDefinitionField { Name = "Computer", Source = EventFieldSource.Metadata, SourceName = "SourceComputer" }
            }
        };
        object[] records = events.Select(item => {
            var source = new EventObject(
                new SyntheticEventRecord(item.Time, item.RecordId, providerName),
                queriedMachine,
                EventReadMode.StructuredDataAndMessage) {
                ContainerLog = container,
                GatheredLogName = container
            };
            source.Data["TargetUserName"] = item.User;
            return (object)EventDefinitionEngine.CreateRecord(definition, source);
        }).ToArray();
        return EventReportEngine.Create(records, "Stored logons");
    }

    private static EventReport CreateReportWithChangedSchema(DateTime time, long recordId) {
        EventDefinition definition = new() {
            Name = "StoredLogon",
            Sources = new[] {
                new EventDefinitionSource { LogName = "Security", EventIds = new[] { 4624 } }
            },
            Fields = new[] {
                new EventDefinitionField { Name = "DifferentField", Source = EventFieldSource.Data, SourceName = "TargetUserName" }
            }
        };
        var source = new EventObject(
            new SyntheticEventRecord(time, recordId),
            "WEC01",
            EventReadMode.StructuredDataAndMessage) {
            ContainerLog = "ForwardedEvents",
            GatheredLogName = "ForwardedEvents"
        };
        source.Data["TargetUserName"] = "bob";
        return EventReportEngine.Create(new object[] {
            EventDefinitionEngine.CreateRecord(definition, source)
        });
    }

    private static EventReport CreateTypedValueReport() {
        EventDefinition definition = new() {
            Name = "StoredTypedValues",
            Sources = new[] {
                new EventDefinitionSource { LogName = "Security", EventIds = new[] { 4624 } }
            },
            Fields = new[] {
                new EventDefinitionField {
                    Name = "IsoText",
                    ValueKind = EventFieldValueKind.String,
                    Source = EventFieldSource.Data,
                    SourceName = "IsoText"
                },
                new EventDefinitionField {
                    Name = "AttemptCount",
                    ValueKind = EventFieldValueKind.Int32,
                    Source = EventFieldSource.Data,
                    SourceName = "AttemptCount"
                },
                new EventDefinitionField {
                    Name = "OccurredAt",
                    ValueKind = EventFieldValueKind.DateTime,
                    Source = EventFieldSource.Data,
                    SourceName = "OccurredAt"
                },
                new EventDefinitionField {
                    Name = "ClientAddress",
                    ValueKind = EventFieldValueKind.IpAddress,
                    Source = EventFieldSource.Data,
                    SourceName = "ClientAddress"
                }
            }
        };
        var source = new EventObject(
            new SyntheticEventRecord(
                new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc),
                44),
            "WEC01",
            EventReadMode.StructuredDataAndMessage) {
            ContainerLog = "ForwardedEvents",
            GatheredLogName = "ForwardedEvents"
        };
        source.Data["IsoText"] = "2026-08-20T10:00:00Z";
        source.Data["AttemptCount"] = "7";
        source.Data["OccurredAt"] = "2026-08-20T10:00:00Z";
        source.Data["ClientAddress"] = "2001:db8::1";
        return EventReportEngine.Create(new object[] {
            EventDefinitionEngine.CreateRecord(definition, source)
        });
    }

    private static EventReport CreateShadowedProviderReport(DateTime time, long recordId) {
        EventDefinition definition = new() {
            Name = "StoredShadowedProvider",
            DisplayName = "Stored provider shadow",
            Sources = new[] {
                new EventDefinitionSource { LogName = "Security", EventIds = new[] { 4624 } }
            },
            Fields = new[] {
                new EventDefinitionField {
                    Name = "ProviderName",
                    Source = EventFieldSource.Data,
                    SourceName = "CustomProvider"
                }
            }
        };
        var source = new EventObject(
            new SyntheticEventRecord(time, recordId),
            "WEC01",
            EventReadMode.StructuredDataAndMessage) {
            ContainerLog = "ForwardedEvents",
            GatheredLogName = "ForwardedEvents"
        };
        source.Data["CustomProvider"] = "custom-provider";
        return EventReportEngine.Create(new object[] {
            EventDefinitionEngine.CreateRecord(definition, source)
        });
    }

    private static EventReport CreateGenericReport(
        DateTime time,
        long recordId,
        string field,
        string value) {

        var source = new EventObject(
            new SyntheticEventRecord(time, recordId),
            "WEC01",
            EventReadMode.StructuredDataAndMessage) {
            ContainerLog = "ForwardedEvents",
            GatheredLogName = "ForwardedEvents"
        };
        source.Data[field] = value;
        return EventReportEngine.Create(new object[] { source }, "Generic events");
    }

    private static string CreateStorePath() => Path.Combine(
        Path.GetTempPath(),
        $"eventviewerx-store-{Guid.NewGuid():N}.db");

    private static void DeleteStore(string path) {
        foreach (string candidate in new[] { path, path + "-wal", path + "-shm", path + ".maintenance.lock" }) {
            if (File.Exists(candidate)) {
                File.Delete(candidate);
            }
        }
    }

    private sealed class SyntheticEventRecord : EventRecord {
        private readonly DateTime _time;
        private readonly long _recordId;
        private readonly string _providerName;

        internal SyntheticEventRecord(
            DateTime time,
            long recordId,
            string providerName = "Microsoft-Windows-Security-Auditing") {

            _time = time;
            _recordId = recordId;
            _providerName = providerName;
        }

        public override string ProviderName => _providerName;
        public override string LogName => "Security";
        public override string MachineName => "source.ad.evotec.xyz";
        public override int Id => 4624;
        public override byte? Level => 0;
        public override int? Task => 12544;
        public override long? Keywords => 0;
        public override IEnumerable<string> KeywordsDisplayNames => Array.Empty<string>();
        public override short? Opcode => 0;
        public override string OpcodeDisplayName => string.Empty;
        public override string TaskDisplayName => string.Empty;
        public override Guid? ProviderId => null;
        public override Guid? ActivityId => null;
        public override Guid? RelatedActivityId => null;
        public override int? ProcessId => 1;
        public override int? ThreadId => 1;
        public override string LevelDisplayName => "Information";
        public override IList<EventProperty> Properties => Array.Empty<EventProperty>();
        public override DateTime? TimeCreated => _time;
        public override int? Qualifiers => null;
        public override long? RecordId => _recordId;
        public override byte? Version => 0;
        public override SecurityIdentifier UserId => null!;
        public override EventBookmark Bookmark => null!;
        public override string FormatDescription() => "An account was logged on.";
        public override string FormatDescription(IEnumerable<object> values) => FormatDescription();
        public override string ToXml() => "<Event><EventData /></Event>";
    }
}
