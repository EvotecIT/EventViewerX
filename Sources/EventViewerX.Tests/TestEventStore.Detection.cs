using System.Globalization;
using DBAClientX;
using EventViewerX.Native;
using EventViewerX.Reporting;
using EventViewerX.Storage;
using Xunit;

namespace EventViewerX.Tests;

public sealed partial class TestEventStore {
    [Fact]
    public void RestoredObservationsKeepNativeMetadataAuthoritativeOverStoredFields() {
        DateTime time = new(2026, 8, 28, 10, 0, 0, DateTimeKind.Utc);
        EventObject source = CreateHistoricalEvent(42, 0, "alice");
        var stored = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) {
            ["EventId"] = 9999,
            ["ProviderName"] = "spoofed-provider",
            ["ProcessId"] = 999,
            ["ThreadId"] = 888,
            ["SourceLog"] = "spoofed-log",
            ["CustomField"] = "retained"
        };

        EventObservation observation = EventObservation.Restore(
            source,
            "stored-identity",
            "StoredType",
            stored,
            time,
            time);

        Assert.Equal(source.Id, observation.Fields["EventId"]);
        Assert.Equal(source.ProviderName, observation.Fields["ProviderName"]);
        Assert.Equal(source.ProcessId, observation.Fields["ProcessId"]);
        Assert.Equal(source.ThreadId, observation.Fields["ThreadId"]);
        Assert.Equal(source.OriginalLogName, observation.Fields["SourceLog"]);
        Assert.Equal("stored-identity", observation.Fields["Identity"]);
        Assert.Equal("StoredType", observation.Fields["TypeName"]);
        Assert.Equal("retained", observation.Fields["CustomField"]);
    }

    [Fact]
    public async Task FindingHistoryIsIdempotentQueryableAndRetainsEvidenceProvenance() {
        string path = CreateStorePath();
        try {
            EventDetectionFinding finding = CreateDetectionFinding("EVX-STORE-0001", "Łukasz");
            var store = new EventStore(path);

            EventFindingStoreWriteResult first = await store.WriteFindingsAsync(new[] { finding });
            EventFindingStoreWriteResult duplicate = await store.WriteFindingsAsync(new[] { finding });
            IReadOnlyList<StoredEventDetectionFinding> stored = await store.ReadFindingsAsync(
                new EventFindingStoreQuery {
                    RuleIds = new[] { "evx-store-0001" },
                    PackIds = new[] { "eventviewerx.tests" },
                    Severities = new[] { EventDetectionSeverity.High },
                    Statuses = new[] { EventDetectionFindingStatus.Matched },
                    EntityField = "Account",
                    EntityValue = "łUKASZ"
                });

            Assert.Equal(1, first.Attempted);
            Assert.Equal(1, first.Inserted);
            Assert.Equal(0, duplicate.Inserted);
            Assert.Equal(1, duplicate.Duplicates);
            StoredEventDetectionFinding restored = Assert.Single(stored);
            Assert.Equal(64, restored.FindingId.Length);
            Assert.Equal(finding.RuleId, restored.RuleId);
            Assert.Equal(finding.PackVersion, restored.PackVersion);
            Assert.Equal(finding.SourceHash, restored.SourceHash);
            Assert.Equal(finding.Tags, restored.Tags);
            Assert.True(restored.Coverage.IsComplete);
            Assert.Equal(finding.Coverage.ExpectedTargets, restored.Coverage.ExpectedTargets);
            Assert.Equal(finding.Coverage.ObservedChannels, restored.Coverage.ObservedChannels);
            Assert.Equal("Łukasz", restored.Entities["account"]);
            StoredEventDetectionEvidence evidence = Assert.Single(restored.Evidence);
            Assert.Equal(finding.Evidence[0].Identity, evidence.Identity);
            Assert.Equal(finding.Evidence[0].ProviderName, evidence.ProviderName);
            Assert.Equal(finding.Evidence[0].ReceivedTimeUtc, evidence.ReceivedTimeUtc);
            Assert.Equal(finding.Evidence[0].ProcessedTimeUtc, evidence.ProcessedTimeUtc);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task FindingHistoryRetainsCoverageChangesForTheSameMatchedEvidence() {
        string path = CreateStorePath();
        try {
            EventDetectionCoverage complete = EventDetectionCoverage.Create(
                expectedTargets: new[] { "server01" },
                observedTargets: new[] { "server01" },
                expectedChannels: new[] { "Security" },
                observedChannels: new[] { "Security" });
            EventDetectionFinding first = CreateDetectionFinding(
                "EVX-STORE-COVERAGE",
                "alice",
                coverage: complete);
            EventDetectionFinding second = CreateDetectionFinding(
                "EVX-STORE-COVERAGE",
                "alice",
                coverage: complete.WithFailures(new[] { "Collector disconnected." }));
            var store = new EventStore(path);

            EventFindingStoreWriteResult write = await store.WriteFindingsAsync(new[] { first, second });
            IReadOnlyList<StoredEventDetectionFinding> stored = await store.ReadFindingsAsync(
                new EventFindingStoreQuery { RuleIds = new[] { "EVX-STORE-COVERAGE" } });

            Assert.Equal(2, write.Inserted);
            Assert.Equal(2, stored.Count);
            Assert.Contains(stored, static finding => finding.Coverage.IsComplete);
            Assert.Contains(stored, static finding => !finding.Coverage.IsComplete);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task FindingHistoryTreatsReorderedCoverageSetsAsTheSameEvidence() {
        string path = CreateStorePath();
        try {
            EventDetectionCoverage firstCoverage = EventDetectionCoverage.Create(
                expectedTargets: new[] { "server01", "server02" },
                observedTargets: new[] { "server01", "server02" },
                expectedChannels: new[] { "Security", "System" },
                observedChannels: new[] { "Security", "System" });
            EventDetectionCoverage reorderedCoverage = EventDetectionCoverage.Create(
                expectedTargets: new[] { "SERVER02", "SERVER01" },
                observedTargets: new[] { "SERVER02", "SERVER01" },
                expectedChannels: new[] { "system", "security" },
                observedChannels: new[] { "system", "security" });
            EventDetectionFinding first = CreateDetectionFinding(
                "EVX-STORE-CANONICAL-COVERAGE",
                "alice",
                coverage: firstCoverage);
            EventDetectionFinding reordered = CreateDetectionFinding(
                "EVX-STORE-CANONICAL-COVERAGE",
                "alice",
                coverage: reorderedCoverage);
            var store = new EventStore(path);

            EventFindingStoreWriteResult write = await store.WriteFindingsAsync(new[] { first, reordered });
            IReadOnlyList<StoredEventDetectionFinding> stored = await store.ReadFindingsAsync(
                new EventFindingStoreQuery { RuleIds = new[] { "EVX-STORE-CANONICAL-COVERAGE" } });

            Assert.Equal(1, write.Inserted);
            Assert.Equal(1, write.Duplicates);
            Assert.Single(stored);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task FindingHistoryRetainsEffectiveSeverityChangesForTheSameEvidence() {
        string path = CreateStorePath();
        try {
            EventDetectionFinding high = CreateDetectionFinding(
                "EVX-STORE-SEVERITY",
                "alice",
                severity: EventDetectionSeverity.High);
            EventDetectionFinding critical = CreateDetectionFinding(
                "EVX-STORE-SEVERITY",
                "alice",
                severity: EventDetectionSeverity.Critical);
            var store = new EventStore(path);

            EventFindingStoreWriteResult write = await store.WriteFindingsAsync(new[] { high, critical });
            IReadOnlyList<StoredEventDetectionFinding> stored = await store.ReadFindingsAsync(
                new EventFindingStoreQuery { RuleIds = new[] { "EVX-STORE-SEVERITY" } });

            Assert.Equal(2, write.Inserted);
            Assert.Contains(stored, static finding => finding.Severity == EventDetectionSeverity.High);
            Assert.Contains(stored, static finding => finding.Severity == EventDetectionSeverity.Critical);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task FindingHistoryRetainsPackProvenanceChangesForTheSameEvidence() {
        string path = CreateStorePath();
        try {
            EventDetectionFinding first = CreateDetectionFinding(
                "EVX-STORE-PACK-PROVENANCE",
                "alice",
                packId: "eventviewerx.tests.first",
                packVersion: "1.0.0");
            EventDetectionFinding second = CreateDetectionFinding(
                "EVX-STORE-PACK-PROVENANCE",
                "alice",
                packId: "eventviewerx.tests.second",
                packVersion: "2.0.0");
            var store = new EventStore(path);

            EventFindingStoreWriteResult write = await store.WriteFindingsAsync(new[] { first, second });
            IReadOnlyList<StoredEventDetectionFinding> stored = await store.ReadFindingsAsync(
                new EventFindingStoreQuery { RuleIds = new[] { "EVX-STORE-PACK-PROVENANCE" } });

            Assert.Equal(2, write.Inserted);
            Assert.Equal(2, stored.Count);
            Assert.Contains(stored, static finding =>
                finding.PackId == "eventviewerx.tests.first" && finding.PackVersion == "1.0.0");
            Assert.Contains(stored, static finding =>
                finding.PackId == "eventviewerx.tests.second" && finding.PackVersion == "2.0.0");
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task UnicodeEntityQueriesRemainBoundedAcrossFindingPages() {
        string path = CreateStorePath();
        try {
            EventDetectionFinding[] findings = Enumerable.Range(0, 130)
                .Select(index => CreateDetectionFinding(
                    index == 129 ? "EVX-STORE-ŁUKASZ" : "EVX-STORE-UNICODE-PAGE",
                    index == 129 ? "Łukasz" : $"account-{index}",
                    index))
                .ToArray();
            var store = new EventStore(path);
            await store.WriteFindingsAsync(findings);

            IReadOnlyList<StoredEventDetectionFinding> byRule = await store.ReadFindingsAsync(
                new EventFindingStoreQuery {
                    RuleIds = new[] { "evx-store-łukasz" },
                    MaxFindings = 1,
                    Oldest = true
                });
            IReadOnlyList<StoredEventDetectionFinding> byEntity = await store.ReadFindingsAsync(
                new EventFindingStoreQuery {
                    EntityField = "Account",
                    EntityValue = "łUKASZ",
                    MaxFindings = 1,
                    Oldest = true
                });

            Assert.Equal("EVX-STORE-ŁUKASZ", Assert.Single(byRule).RuleId);
            StoredEventDetectionFinding finding = Assert.Single(byEntity);
            Assert.Equal("Łukasz", finding.Entities["Account"]);
            Assert.Single(finding.Evidence);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task FindingHistoryUsesOverlappingEvidenceWindowsAndBoundedOrdering() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            EventDetectionFinding first = CreateDetectionFinding("EVX-STORE-FIRST", "alice", minute: 0);
            EventDetectionFinding second = CreateDetectionFinding("EVX-STORE-SECOND", "bob", minute: 5);
            await store.WriteFindingsAsync(new[] { first, second });

            IReadOnlyList<StoredEventDetectionFinding> newest = await store.ReadFindingsAsync(
                new EventFindingStoreQuery {
                    StartTime = new DateTime(2026, 8, 28, 10, 4, 0, DateTimeKind.Utc),
                    MaxFindings = 1
                });
            IReadOnlyList<StoredEventDetectionFinding> oldest = await store.ReadFindingsAsync(
                new EventFindingStoreQuery { Oldest = true, MaxFindings = 1 });

            Assert.Equal("EVX-STORE-SECOND", Assert.Single(newest).RuleId);
            Assert.Equal("EVX-STORE-FIRST", Assert.Single(oldest).RuleId);
            await Assert.ThrowsAsync<ArgumentException>(() => store.ReadFindingsAsync(
                new EventFindingStoreQuery { EntityField = "Account" }));
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task FindingHistoryRetainsDistinctIncompleteDiagnosticsForTheSameEvidence() {
        string path = CreateStorePath();
        try {
            EventObservation observation = EventObservation.Create(CreateHistoricalEvent(1, minute: 0, "alice"));
            EventDetectionPlan plan = EventDetectionPlan.Compile(new[] {
                new EventDetectionRule(new EventDetectionRuleDefinition {
                    RuleId = "EVX-STORE-MISSING-FIRST",
                    Title = "Missing first group",
                    Kind = EventDetectionRuleKind.Threshold,
                    EventIds = new[] { 1001 },
                    Threshold = 2,
                    Window = TimeSpan.FromMinutes(5),
                    GroupBy = "MissingFirst"
                }),
                new EventDetectionRule(new EventDetectionRuleDefinition {
                    RuleId = "EVX-STORE-MISSING-SECOND",
                    Title = "Missing second group",
                    Kind = EventDetectionRuleKind.Threshold,
                    EventIds = new[] { 1001 },
                    Threshold = 2,
                    Window = TimeSpan.FromMinutes(5),
                    GroupBy = "MissingSecond"
                })
            });
            EventDetectionFinding[] incomplete = EventDetectionEngine.Stream(new[] { observation }, plan).ToArray();
            var store = new EventStore(path);

            EventFindingStoreWriteResult write = await store.WriteFindingsAsync(incomplete);
            IReadOnlyList<StoredEventDetectionFinding> stored = await store.ReadFindingsAsync(
                new EventFindingStoreQuery { Statuses = new[] { EventDetectionFindingStatus.Incomplete } });

            Assert.Equal(2, write.Inserted);
            Assert.Equal(2, stored.Count);
            Assert.Equal(2, stored.Select(static finding => finding.CompletenessDiagnostic).Distinct().Count());
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task StoredDetectionRehydratesPriorWindowAndPreservesEvidenceIdentityAcrossRestart() {
        string path = CreateStorePath();
        try {
            EventObject first = CreateHistoricalEvent(1, minute: 0, "alice");
            EventObject second = CreateHistoricalEvent(2, minute: 4, "alice");
            EventObservation firstLive = EventObservation.Create(first);
            EventObservation secondLive = EventObservation.Create(second);
            var store = new EventStore(path);
            await store.WriteAsync(EventReportEngine.Create(new object[] { first }));
            await store.WriteAsync(EventReportEngine.Create(new object[] { second }));
            EventDetectionPlan plan = EventDetectionPlan.Compile(new[] {
                new EventDetectionRule(new EventDetectionRuleDefinition {
                    RuleId = "EVX-STORE-RESTART",
                    Title = "Restart-safe threshold",
                    Kind = EventDetectionRuleKind.Threshold,
                    EventIds = new[] { 1001 },
                    Threshold = 2,
                    Window = TimeSpan.FromMinutes(5),
                    GroupBy = "Account"
                })
            });
            EventDetectionCoverage coverage = EventDetectionCoverage.Create(
                expectedTargets: new[] { path },
                observedTargets: new[] { path },
                expectedChannels: new[] { "Security" },
                observedChannels: new[] { "Security" },
                expectedEventIds: new[] { 1001 },
                observedEventIds: new[] { 1001 });

            EventDetectionExecutionResult result = await store.EvaluateDetectionAsync(
                new EventStoreQuery {
                    StartTime = new DateTime(2026, 8, 28, 10, 4, 0, DateTimeKind.Utc),
                    EndTime = new DateTime(2026, 8, 28, 10, 4, 0, DateTimeKind.Utc)
                },
                plan,
                new EventDetectionEngineOptions(coverage: coverage));

            EventDetectionFinding finding = Assert.Single(result.Findings);
            EventObservation requestedWindowObservation = Assert.Single(result.Observations);
            Assert.Equal(secondLive.Identity, requestedWindowObservation.Identity);
            Assert.All(result.Observations, static observation => {
                Assert.Equal(10, observation.SourceEvent.ProcessId);
                Assert.Equal(20, observation.SourceEvent.ThreadId);
                Assert.Equal(10, observation.Fields["ProcessId"]);
                Assert.Equal(20, observation.Fields["ThreadId"]);
            });
            Assert.True(
                result.IsComplete,
                $"Coverage={result.Coverage.IsComplete}; failures={string.Join(" | ", result.Coverage.Failures)}; " +
                $"status={finding.Status}; diagnostic={finding.CompletenessDiagnostic}");
            Assert.Equal(
                new[] { firstLive.Identity, secondLive.Identity },
                finding.EvidenceIdentities);
            Assert.Equal(new DateTime(2026, 8, 28, 10, 0, 0, DateTimeKind.Utc), finding.StartTimeUtc);
            Assert.Equal(new DateTime(2026, 8, 28, 10, 4, 0, DateTimeKind.Utc), finding.EndTimeUtc);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task StoredDetectionRetainsWarmupBoundFailuresForTheRequestedWindow() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            await store.WriteAsync(EventReportEngine.Create(new object[] {
                CreateHistoricalEvent(1, minute: 0, "alice"),
                CreateHistoricalEvent(2, minute: 1, "bob"),
                CreateHistoricalEvent(3, minute: 4, "charlie")
            }));
            EventDetectionPlan plan = EventDetectionPlan.Compile(new[] {
                new EventDetectionRule(new EventDetectionRuleDefinition {
                    RuleId = "EVX-STORE-WARMUP-BOUND",
                    Title = "Warm-up bounded threshold",
                    Kind = EventDetectionRuleKind.Threshold,
                    EventIds = new[] { 1001 },
                    Threshold = 2,
                    Window = TimeSpan.FromMinutes(5),
                    GroupBy = "Account"
                })
            });
            EventDetectionCoverage coverage = EventDetectionCoverage.Create(
                expectedTargets: new[] { path },
                observedTargets: new[] { path },
                expectedChannels: new[] { "Security" },
                observedChannels: new[] { "Security" },
                expectedEventIds: new[] { 1001 },
                observedEventIds: new[] { 1001 });
            DateTime resultStart = new(2026, 8, 28, 10, 4, 0, DateTimeKind.Utc);

            EventDetectionExecutionResult result = await store.EvaluateDetectionAsync(
                new EventStoreQuery { StartTime = resultStart, EndTime = resultStart },
                plan,
                new EventDetectionEngineOptions(
                    maximumObservations: 0,
                    maximumGroups: 1,
                    maximumStateObservations: 10,
                    coverage: coverage));

            EventDetectionFinding incomplete = Assert.Single(result.Findings);
            Assert.Equal(EventDetectionFindingStatus.Incomplete, incomplete.Status);
            Assert.True(incomplete.EndTimeUtc < resultStart);
            Assert.Contains("MaximumGroups", incomplete.CompletenessDiagnostic);
            Assert.False(result.IsComplete);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task StoredDetectionClampsStatefulWarmupAtDateTimeMinimum() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            store.Initialize();
            EventDetectionPlan plan = EventDetectionPlan.Compile(new[] {
                new EventDetectionRule(new EventDetectionRuleDefinition {
                    RuleId = "EVX-STORE-MINIMUM-TIME",
                    Title = "Minimum time threshold",
                    Kind = EventDetectionRuleKind.Threshold,
                    EventIds = new[] { 1001 },
                    Threshold = 2,
                    Window = TimeSpan.FromMinutes(5)
                })
            });

            EventDetectionExecutionResult result = await store.EvaluateDetectionAsync(
                new EventStoreQuery { StartTime = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc) },
                plan);

            Assert.Empty(result.Observations);
            Assert.Empty(result.Findings);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task StoredTemporalDetectionDoesNotPushDownPartiallyBoundedStepEventIds() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            await store.WriteAsync(EventReportEngine.Create(new object[] {
                CreateHistoricalEvent(1, minute: 0, "alice", eventId: 1001),
                CreateHistoricalEvent(2, minute: 1, "alice", eventId: 9001)
            }));
            EventDetectionPlan plan = EventDetectionPlan.Compile(new[] {
                new EventDetectionRule(new EventDetectionRuleDefinition {
                    RuleId = "EVX-STORE-PARTIAL-STEP-SELECTOR",
                    Title = "Partially bounded temporal selector",
                    Kind = EventDetectionRuleKind.OrderedTemporal,
                    Window = TimeSpan.FromMinutes(5),
                    GroupBy = "Account",
                    Steps = new[] {
                        new EventDetectionStepDefinition {
                            Name = "bounded",
                            EventIds = new[] { 1001 }
                        },
                        new EventDetectionStepDefinition {
                            Name = "predicate-only",
                            Predicate = EventPredicate.Compare(
                                "EventId",
                                EventPredicateOperator.Equal,
                                9001)
                        }
                    }
                })
            });

            EventDetectionExecutionResult result = await store.EvaluateDetectionAsync(
                new EventStoreQuery { Oldest = true },
                plan,
                new EventDetectionEngineOptions(coverage: EventDetectionCoverage.Create()));

            EventDetectionFinding finding = Assert.Single(result.Findings);
            Assert.Equal(new[] { 1001, 9001 }, finding.Evidence.Select(static item => item.EventId));
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public void FindingSchemaUpgradeIsAdditiveForExistingEventStores() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            store.Initialize();
            using (var sqlite = new SQLite()) {
                using SQLiteSession session = sqlite.OpenSession(path);
                session.ExecuteNonQuery("DROP TABLE evx_finding_entities;");
                session.ExecuteNonQuery("DROP TABLE evx_finding_evidence;");
                session.ExecuteNonQuery("DROP TABLE evx_findings;");
                session.ExecuteNonQuery(
                    "UPDATE evx_store_metadata SET finding_schema_version = 0 WHERE singleton_id = 1;");
            }

            new EventStore(path).Initialize();

            using var verificationSqlite = new SQLite();
            using SQLiteSession verification = verificationSqlite.OpenSession(path);
            Assert.Equal(2L, Convert.ToInt64(
                verification.ExecuteScalar(
                    "SELECT finding_schema_version FROM evx_store_metadata WHERE singleton_id = 1;"),
                CultureInfo.InvariantCulture));
            Assert.Equal(3L, Convert.ToInt64(
                verification.ExecuteScalar(
                    "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' " +
                    "AND name IN ('evx_findings', 'evx_finding_evidence', 'evx_finding_entities');"),
                CultureInfo.InvariantCulture));
            Assert.Equal(1L, Convert.ToInt64(
                verification.ExecuteScalar(
                    "SELECT COUNT(*) FROM pragma_table_info('evx_findings') WHERE name = 'coverage_json';"),
                CultureInfo.InvariantCulture));
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task WatcherCheckpointAdvanceUsesCompareAndSwap() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            var first = new EventStoreCheckpoint {
                Consumer = "watcher-a",
                Computer = "server01",
                Container = "Security|query-hash",
                RecordId = 41,
                BookmarkXml = "<Bookmark Channel='Security' RecordId='41' IsCurrent='true'/>",
                UpdatedAtUtc = DateTime.UtcNow
            };

            EventStoreCheckpoint persisted = await store.AdvanceCheckpointAsync(first, expectedCheckpoint: null);
            var next = new EventStoreCheckpoint {
                Consumer = persisted.Consumer,
                Computer = persisted.Computer,
                Container = persisted.Container,
                RecordId = 42,
                BookmarkXml = "<Bookmark Channel='Security' RecordId='42' IsCurrent='true'/>",
                UpdatedAtUtc = DateTime.UtcNow
            };
            EventStoreCheckpoint advanced = await store.AdvanceCheckpointAsync(next, persisted);

            Assert.Equal(42, advanced.RecordId);
            Assert.Equal(next.BookmarkXml, advanced.BookmarkXml);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.AdvanceCheckpointAsync(next, persisted));
        } finally {
            DeleteStore(path);
        }
    }

    private static EventDetectionFinding CreateDetectionFinding(
        string ruleId,
        string account,
        int minute = 0,
        EventDetectionCoverage? coverage = null,
        string packId = "eventviewerx.tests",
        string packVersion = "1.2.0",
        EventDetectionSeverity severity = EventDetectionSeverity.High) {

        DateTime time = new DateTime(2026, 8, 28, 10, 0, 0, DateTimeKind.Utc).AddMinutes(minute);
        var metadata = new NativeEventMetadata(
            "Provider-A",
            null,
            4624,
            qualifiers: null,
            level: 0,
            task: 0,
            opcode: 0,
            keywords: 0,
            time,
            minute + 1,
            null,
            null,
            10,
            20,
            "Security",
            "server01",
            null,
            1);
        var source = new EventObject(metadata, queriedMachine: "collector01", containerLog: "Security");
        source.Data["Account"] = account;
        EventObservation observation = EventObservation.Create(source, receivedTimeUtc: time, processedTimeUtc: time);
        return new EventDetectionFinding(
            ruleId,
            "1.0.0",
            packId,
            packVersion,
            "Native",
            ruleId,
            "stable",
            new string('A', 64),
            "MIT",
            "Durable finding",
            severity,
            90,
            EventDetectionFindingStatus.Matched,
            time,
            time,
            new[] { observation },
            new[] { "attack.t1110" },
            new[] { "Approved test activity" },
            new[] { "https://example.invalid/rule" },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Account"] = account },
            coverage ?? EventDetectionCoverage.Create(
                expectedTargets: new[] { "server01" },
                observedTargets: new[] { "server01" },
                expectedChannels: new[] { "Security" },
                observedChannels: new[] { "Security" }),
            "The test rule matched.",
            completenessDiagnostic: null);
    }

    private static EventObject CreateHistoricalEvent(
        long recordId,
        int minute,
        string account,
        int eventId = 1001) {
        DateTime time = new(2026, 8, 28, 10, minute, 0, DateTimeKind.Utc);
        var metadata = new NativeEventMetadata(
            "Provider-A",
            null,
            eventId,
            qualifiers: null,
            level: 0,
            task: 0,
            opcode: 0,
            keywords: 0,
            time,
            recordId,
            null,
            null,
            10,
            20,
            "Security",
            "server01",
            null,
            1);
        var source = new EventObject(metadata, queriedMachine: "collector01", containerLog: "Security");
        source.Data["Account"] = account;
        return source;
    }
}
