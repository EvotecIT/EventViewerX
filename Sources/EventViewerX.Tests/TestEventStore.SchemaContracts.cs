using DBAClientX;
using EventViewerX.Reporting;
using EventViewerX.Storage;
using Xunit;

namespace EventViewerX.Tests;

public sealed partial class TestEventStore {
    [Fact]
    public void FutureStoreVersionsAreRejectedBeforeCurrentSchemaDdlRuns() {
        string path = CreateStorePath();
        try {
            using (var sqlite = new SQLite { BusyTimeoutMs = 10000 }) {
                using SQLiteSession session = sqlite.OpenSession(path);
                session.ExecuteNonQuery(@"
CREATE TABLE evx_store_metadata (
    singleton_id INTEGER NOT NULL PRIMARY KEY,
    schema_version INTEGER NOT NULL,
    created_utc TEXT NOT NULL
);
INSERT INTO evx_store_metadata (singleton_id, schema_version, created_utc)
VALUES (1, 999, '2026-08-20T00:00:00Z');
CREATE TABLE evx_events (future_only TEXT NOT NULL);");
            }

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                new EventStore(path).Initialize());

            Assert.Contains("999", exception.Message, StringComparison.Ordinal);
            Assert.Contains("not supported", exception.Message, StringComparison.OrdinalIgnoreCase);
            using var verificationClient = new SQLite { BusyTimeoutMs = 10000 };
            using SQLiteSession verification = verificationClient.OpenSession(path);
            IReadOnlyList<string> columns = verification.QueryAsList(
                "PRAGMA table_info(evx_events);",
                static record => record.GetString(1));
            object? currentIndex = verification.ExecuteScalar(
                "SELECT name FROM sqlite_master WHERE type = 'index' AND name = 'ix_evx_events_time';");
            Assert.Equal(new[] { "future_only" }, columns);
            Assert.Null(currentIndex);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task GenericSchemaEvolutionPreservesStringValuesAndObservedColumns() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            await store.WriteAsync(CreateGenericReport(
                new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc),
                42,
                "IsoText",
                "2026-08-20T10:00:00Z"));
            await store.WriteAsync(CreateGenericReport(
                new DateTime(2026, 8, 1, 2, 0, 0, DateTimeKind.Utc),
                43,
                "SecondField",
                "two"));

            EventReport report = await store.ReadReportAsync(new EventStoreQuery { Oldest = true });
            string schemaJson;
            using (var sqlite = new SQLite { BusyTimeoutMs = 10000 }) {
                using SQLiteSession session = sqlite.OpenSession(path);
                schemaJson = Convert.ToString(session.ExecuteScalar(
                    "SELECT schema_json FROM evx_definitions WHERE definition_name = 'Generic';"),
                    System.Globalization.CultureInfo.InvariantCulture)!;
            }

            EventReportRow first = report.Rows[0];
            Assert.IsType<string>(first.Values["IsoText"]);
            Assert.Equal("2026-08-20T10:00:00Z", first.Values["IsoText"]);
            Assert.Contains("IsoText", schemaJson, StringComparison.Ordinal);
            Assert.Contains("SecondField", schemaJson, StringComparison.Ordinal);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task ExistingStoresAcquireIndexedTransportIdentityWithoutLosingIdempotence() {
        string path = CreateStorePath();
        try {
            EventReport report = CreateReport(
                (new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc), 42, "alice"));
            await new EventStore(path).WriteAsync(report);
            using (var sqlite = new SQLite { BusyTimeoutMs = 10000 }) {
                using SQLiteSession session = sqlite.OpenSession(path);
                session.ExecuteNonQuery("DROP INDEX ix_evx_events_original_transport;");
                session.ExecuteNonQuery("ALTER TABLE evx_events DROP COLUMN original_event_key;");
                session.ExecuteNonQuery("ALTER TABLE evx_events DROP COLUMN transport_kind;");
            }

            var migratedStore = new EventStore(path);
            EventStoreWriteResult duplicate = await migratedStore.WriteAsync(report);
            EventReport stored = await migratedStore.ReadReportAsync(new EventStoreQuery());

            Assert.Equal(0, duplicate.Inserted);
            Assert.Single(stored.Rows);
            using var verificationClient = new SQLite { BusyTimeoutMs = 10000 };
            using SQLiteSession verification = verificationClient.OpenSession(path);
            IReadOnlyList<string> columns = verification.QueryAsList(
                "PRAGMA table_info(evx_events);",
                static record => record.GetString(1));
            Assert.Contains("original_event_key", columns);
            Assert.Contains("transport_kind", columns);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task ExistingStoresAcquireNullableActivityMetadataColumns() {
        string path = CreateStorePath();
        try {
            await new EventStore(path).WriteAsync(CreateReport((
                new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc),
                42,
                "alice")));
            using (var sqlite = new SQLite { BusyTimeoutMs = 10000 }) {
                using SQLiteSession session = sqlite.OpenSession(path);
                session.ExecuteNonQuery("ALTER TABLE evx_events DROP COLUMN activity_id;");
                session.ExecuteNonQuery("ALTER TABLE evx_events DROP COLUMN related_activity_id;");
            }

            var migratedStore = new EventStore(path);
            EventReport report = await migratedStore.ReadReportAsync(new EventStoreQuery());

            Assert.Single(report.Rows);
            Assert.Null(report.Rows[0].ActivityId);
            Assert.Null(report.Rows[0].RelatedActivityId);
            using var verificationClient = new SQLite { BusyTimeoutMs = 10000 };
            using SQLiteSession verification = verificationClient.OpenSession(path);
            IReadOnlyList<string> columns = verification.QueryAsList(
                "PRAGMA table_info(evx_events);",
                static record => record.GetString(1));
            Assert.Contains("activity_id", columns);
            Assert.Contains("related_activity_id", columns);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task ConcurrentInitializersSerializeLegacyIdentityMigration() {
        string path = CreateStorePath();
        try {
            EventReport report = CreateReport(
                (new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc), 42, "alice"));
            await new EventStore(path).WriteAsync(report);
            using (var sqlite = new SQLite { BusyTimeoutMs = 10000 }) {
                using SQLiteSession session = sqlite.OpenSession(path);
                session.ExecuteNonQuery("DROP INDEX ix_evx_events_original_transport;");
                session.ExecuteNonQuery("ALTER TABLE evx_events DROP COLUMN original_event_key;");
                session.ExecuteNonQuery("ALTER TABLE evx_events DROP COLUMN transport_kind;");
            }
            var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task[] initializers = Enumerable.Range(0, 16).Select(_ => Task.Run(async () => {
                await start.Task;
                new EventStore(path).Initialize();
            })).ToArray();

            start.SetResult(true);
            await Task.WhenAll(initializers);

            using var verificationClient = new SQLite { BusyTimeoutMs = 10000 };
            using SQLiteSession verification = verificationClient.OpenSession(path);
            IReadOnlyList<string> columns = verification.QueryAsList(
                "PRAGMA table_info(evx_events);",
                static record => record.GetString(1));
            Assert.Contains("original_event_key", columns);
            Assert.Contains("transport_kind", columns);
            Assert.Equal(2, columns.Count(static name =>
                name is "original_event_key" or "transport_kind"));
        } finally {
            DeleteStore(path);
        }
    }

    [Theory]
    [InlineData("ADUserLogonFailed", EventReportSectionKind.Custom)]
    [InlineData("ADUserLogonFailed", EventReportSectionKind.Typed)]
    [InlineData("Generic", EventReportSectionKind.Custom)]
    [InlineData("EventStoreSummary", EventReportSectionKind.Custom)]
    [InlineData("NotABuiltInDefinition", EventReportSectionKind.Typed)]
    public async Task StoreRejectsAmbiguousDefinitionNameAndKindIdentities(
        string definitionName,
        EventReportSectionKind kind) {

        string path = CreateStorePath();
        try {
            EventReport report = EventReportEngine.CreateStored(
                new[] {
                    new EventReportRow {
                        Type = definitionName,
                        Values = new Dictionary<string, object?> { ["Value"] = "one" }
                    }
                },
                new[] {
                    new EventReportSectionSchema {
                        Name = definitionName,
                        Kind = kind,
                        Columns = new[] { CreateColumn("Value", typeof(string)) }
                    }
                });

            InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
                new EventStore(path).WriteAsync(report));

            Assert.Contains(definitionName, exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(path));
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task StoreAcceptsOnlyTheAuthoritativeBuiltInTypedSchema() {
        string path = CreateStorePath();
        try {
            EventReportSectionSchema schema = EventReportSectionSchema.FromType(EventType.ADUserLogonFailed);
            EventReport report = EventReportEngine.CreateStored(
                new[] {
                    new EventReportRow {
                        Type = EventType.ADUserLogonFailed.ToString(),
                        TimeCreated = new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc),
                        EventId = 4625,
                        RecordId = 42,
                        Provider = "Microsoft-Windows-Security-Auditing",
                        SourceLog = "Security",
                        ContainerLog = "Security",
                        SourceComputer = "AD0",
                        CollectorComputer = "AD0",
                        Values = new Dictionary<string, object?>()
                    }
                },
                new[] { schema });
            var store = new EventStore(path);

            EventStoreWriteResult result = await store.WriteAsync(report);
            IReadOnlyList<EventReportSectionSchema> discovered = await store.GetSchemasAsync(
                new EventStoreQuery { Types = new[] { EventType.ADUserLogonFailed } });

            Assert.Equal(1, result.Inserted);
            Assert.Equal(schema.Name, Assert.Single(discovered).Name);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task EmptyStoredQueriesRetainSelectedSchemasForCsvExport() {
        string path = CreateStorePath();
        string csv = Path.Combine(Path.GetTempPath(), $"evx-empty-store-{Guid.NewGuid():N}.csv");
        try {
            var store = new EventStore(path);
            EventReport report = EventReportEngine.CreateStored(
                Array.Empty<EventReportRow>(),
                new[] { CreateSchema("Audit", "Who") });
            await store.WriteAsync(report);

            EventReport empty = await store.ReadReportAsync(new EventStoreQuery {
                DefinitionNames = new[] { "Audit" },
                StartTime = new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
            EventReportCsvRenderer.Save(empty, csv);

            Assert.Empty(empty.Rows);
            Assert.Equal("Audit", Assert.Single(empty.Sections).Name);
            Assert.StartsWith("Who", File.ReadAllText(csv), StringComparison.Ordinal);
        } finally {
            File.Delete(csv);
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task CustomAliasesPersistAndDriveStoredPredicateDiscovery() {
        string path = CreateStorePath();
        try {
            EventReportColumnSchema column = CreateColumn("Who", typeof(string));
            column.Aliases = new[] { "Account" };
            EventReportSectionSchema schema = new() {
                Name = "AliasAudit",
                DisplayName = "Alias audit",
                Kind = EventReportSectionKind.Custom,
                Columns = new[] { column }
            };
            EventReport report = EventReportEngine.CreateStored(
                new[] {
                    new EventReportRow {
                        Type = "AliasAudit",
                        TimeCreated = new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc),
                        EventId = 4624,
                        RecordId = 42,
                        Values = new Dictionary<string, object?> { ["Who"] = "EVOTEC\\Alice" }
                    }
                },
                new[] { schema });
            var store = new EventStore(path);

            await store.WriteAsync(report);
            EventReportSectionSchema storedSchema = Assert.Single(await store.GetSchemasAsync(
                new EventStoreQuery { DefinitionNames = new[] { "AliasAudit" } }));
            EventReport matched = await store.ReadReportAsync(new EventStoreQuery {
                DefinitionNames = new[] { "AliasAudit" },
                Predicate = EventPredicate.Compare("Account", EventPredicateOperator.Equal, "EVOTEC\\Alice")
            });

            Assert.Equal("Account", Assert.Single(storedSchema.Columns).Aliases.Single());
            Assert.Single(matched.Rows);

            EventReportColumnSchema revisedColumn = CreateColumn("Who", typeof(string));
            revisedColumn.Aliases = new[] { "Principal" };
            EventReport revised = EventReportEngine.CreateStored(report.Rows, new[] {
                new EventReportSectionSchema {
                    Name = "AliasAudit",
                    Kind = EventReportSectionKind.Custom,
                    Columns = new[] { revisedColumn }
                }
            });
            await Assert.ThrowsAsync<InvalidDataException>(() => store.WriteAsync(revised));
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task FreshStoresSupplyAuthoritativeSchemasForEmptyTypedQueries() {
        string path = CreateStorePath();
        string csv = Path.Combine(Path.GetTempPath(), $"evx-empty-typed-store-{Guid.NewGuid():N}.csv");
        try {
            var store = new EventStore(path);
            EventReport report = await store.ReadReportAsync(new EventStoreQuery {
                Types = new[] { EventType.ADUserLogonFailed }
            });

            EventReportSection section = Assert.Single(report.Sections);
            Assert.Empty(report.Rows);
            Assert.Equal(nameof(EventType.ADUserLogonFailed), section.Name);
            Assert.NotEmpty(section.Columns);
            Assert.Equal(csv, EventReportCsvRenderer.Save(report, csv));
            Assert.NotEmpty(File.ReadAllText(csv));
        } finally {
            DeleteStore(path);
            if (File.Exists(csv)) {
                File.Delete(csv);
            }
        }
    }

    [Fact]
    public async Task FreshStoresUseSuppliedCustomSchemasForEmptyReports() {
        string path = CreateStorePath();
        string csv = Path.Combine(Path.GetTempPath(), $"evx-empty-custom-store-{Guid.NewGuid():N}.csv");
        var schema = new EventReportSectionSchema {
            Name = "FreshAudit",
            DisplayName = "Fresh audit",
            Kind = EventReportSectionKind.Custom,
            Columns = new[] {
                CreateColumn("Who", typeof(string)),
                CreateColumn("IPAddress", typeof(System.Net.IPAddress))
            }
        };
        try {
            var store = new EventStore(path);
            EventReport report = await store.ReadReportAsync(new EventStoreQuery {
                DefinitionNames = new[] { schema.Name },
                DefinitionSchemas = new[] { schema }
            });

            EventReportSection section = Assert.Single(report.Sections);
            Assert.Empty(report.Rows);
            Assert.Equal(schema.Name, section.Name);
            Assert.Equal(new[] { "Who", "IPAddress" }, section.Columns.Select(static column => column.Name));
            Assert.Equal(csv, EventReportCsvRenderer.Save(report, csv));
            string header = Assert.Single(File.ReadAllLines(csv));
            Assert.Contains("Who", header, StringComparison.Ordinal);
            Assert.Contains("IPAddress", header, StringComparison.Ordinal);
        } finally {
            DeleteStore(path);
            if (File.Exists(csv)) {
                File.Delete(csv);
            }
        }
    }

    [Fact]
    public async Task UnicodeDefinitionSchemaDiscoveryAppliesExactManagedSelection() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            await store.WriteAsync(CreateReportForDefinition(
                "MÜNCHEN-TYPE",
                (new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc), 41, "alice")));
            await store.WriteAsync(CreateReportForDefinition(
                "OtherType",
                (new DateTime(2026, 8, 1, 2, 0, 0, DateTimeKind.Utc), 42, "bob")));

            IReadOnlyList<EventReportSectionSchema> schemas = await store.GetSchemasAsync(
                new EventStoreQuery { DefinitionNames = new[] { "münchen-type" } });

            Assert.Equal("MÜNCHEN-TYPE", Assert.Single(schemas).Name);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task GenericEventDataCannotDuplicateCommonReportColumns() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            await store.WriteAsync(CreateGenericReport(
                new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc),
                42,
                nameof(EventReportRow.EventId),
                "provider-event-id"));

            EventReport report = await store.ReadReportAsync(new EventStoreQuery());
            EventReport metadataMatch = await store.ReadReportAsync(new EventStoreQuery {
                Predicate = EventPredicate.Compare(
                    nameof(EventReportRow.EventId),
                    EventPredicateOperator.Equal,
                    4624)
            });
            EventReportRow row = Assert.Single(report.Rows);
            EventReportSection section = Assert.Single(report.Sections);

            Assert.Equal(4624, row.EventId);
            Assert.Equal("provider-event-id", row.Values[nameof(EventReportRow.EventId)]);
            Assert.Equal((byte)0, row.ToDictionary()[nameof(EventReportRow.LevelValue)]);
            Assert.Single(metadataMatch.Rows);
            Assert.Single(section.Columns, static column =>
                string.Equals(column.Name, nameof(EventReportRow.EventId), StringComparison.OrdinalIgnoreCase));
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public void SchemaAwareRowSerializationPreservesDeclaredShadowingFields() {
        EventReport report = EventReportEngine.CreateStored(
            new[] {
                new EventReportRow {
                    Type = "CustomShadow",
                    EventId = 4624,
                    Provider = "Microsoft-Windows-Security-Auditing",
                    Values = new Dictionary<string, object?> {
                        [nameof(EventReportRow.EventId)] = "provider-event-id",
                        [nameof(EventReportRow.Provider)] = "provider-domain-value"
                    }
                }
            },
            new[] {
                new EventReportSectionSchema {
                    Name = "CustomShadow",
                    Kind = EventReportSectionKind.Custom,
                    Columns = new[] {
                        CreateColumn(nameof(EventReportRow.EventId), typeof(string)),
                        CreateColumn(nameof(EventReportRow.Provider), typeof(string))
                    }
                }
            });

        EventReportRow row = Assert.Single(report.Rows);
        IReadOnlyDictionary<string, object?> serialized = row.ToDictionary(Assert.Single(report.Sections));

        Assert.Equal("provider-event-id", serialized[nameof(EventReportRow.EventId)]);
        Assert.Equal("provider-domain-value", serialized[nameof(EventReportRow.Provider)]);
        Assert.Equal("CustomShadow", serialized[nameof(EventReportRow.Type)]);
    }

    [Fact]
    public void StoredReportsRejectAmbiguousDefinitionAndColumnSchemas() {
        var row = new EventReportRow {
            TimeCreated = new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc),
            Type = "Collision",
            Values = new Dictionary<string, object?> { ["First"] = "one" }
        };
        EventReportSectionSchema first = CreateSchema("Collision", "First");
        EventReportSectionSchema second = CreateSchema("collision", "Second");
        EventReportSectionSchema duplicateColumns = new() {
            Name = "DuplicateColumns",
            Kind = EventReportSectionKind.Custom,
            Columns = new[] {
                CreateColumn("Value", typeof(string)),
                CreateColumn("value", typeof(int))
            }
        };

        ArgumentException definition = Assert.Throws<ArgumentException>(() =>
            EventReportEngine.CreateStored(new[] { row }, new[] { first, second }));
        ArgumentException columns = Assert.Throws<ArgumentException>(() =>
            EventReportEngine.CreateStored(
                new[] { new EventReportRow { Type = "DuplicateColumns" } },
                new[] { duplicateColumns }));

        Assert.Contains("duplicate", definition.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("duplicate", columns.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StoredReportsRejectUndefinedSectionKinds() {
        var row = new EventReportRow {
            Type = "InvalidKind"
        };
        EventReportSectionSchema schema = new() {
            Name = "InvalidKind",
            Kind = (EventReportSectionKind)999,
            Columns = Array.Empty<EventReportColumnSchema>()
        };

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            EventReportEngine.CreateStored(new[] { row }, new[] { schema }));

        Assert.Contains("undefined", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StoredReportsNormalizeCompatibleValuesAndRejectIncompatibleSchemaValues() {
        EventReportSectionSchema schema = new() {
            Name = "TypedValues",
            Kind = EventReportSectionKind.Custom,
            Columns = new[] { CreateColumn("AttemptCount", typeof(int)) }
        };
        var compatible = new EventReportRow {
            Type = "TypedValues",
            Values = new Dictionary<string, object?> { ["AttemptCount"] = "7" }
        };
        var incompatible = new EventReportRow {
            Type = "TypedValues",
            Values = new Dictionary<string, object?> { ["AttemptCount"] = "not-an-integer" }
        };

        EventReport normalized = EventReportEngine.CreateStored(new[] { compatible }, new[] { schema });
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            EventReportEngine.CreateStored(new[] { incompatible }, new[] { schema }));

        Assert.Equal(7, Assert.IsType<int>(Assert.Single(normalized.Rows).Values["AttemptCount"]));
        Assert.Contains("AttemptCount", exception.Message, StringComparison.Ordinal);
        Assert.Contains("System.Int32", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StoreRevalidatesAndNormalizesMutableRowsAtThePersistenceBoundary() {
        string path = CreateStorePath();
        try {
            EventReportSectionSchema schema = new() {
                Name = "MutableValues",
                Kind = EventReportSectionKind.Custom,
                Columns = new[] { CreateColumn("AttemptCount", typeof(int)) }
            };
            EventReport report = EventReportEngine.CreateStored(
                new[] {
                    new EventReportRow {
                        Type = "MutableValues",
                        Values = new Dictionary<string, object?> { ["AttemptCount"] = "7" }
                    }
                },
                new[] { schema });
            report.Rows[0].Values = new Dictionary<string, object?> {
                ["AttemptCount"] = "not-an-integer"
            };
            var store = new EventStore(path);

            ArgumentException invalid = await Assert.ThrowsAsync<ArgumentException>(() =>
                store.WriteAsync(report));

            Assert.Contains("AttemptCount", invalid.Message, StringComparison.Ordinal);
            Assert.False(File.Exists(path));

            report.Rows[0].Values = new Dictionary<string, object?> { ["AttemptCount"] = "8" };
            EventStoreWriteResult written = await store.WriteAsync(report);
            EventReport stored = await store.ReadReportAsync(new EventStoreQuery());

            Assert.Equal(1, written.Inserted);
            Assert.Equal(8, Assert.IsType<int>(Assert.Single(stored.Rows).Values["AttemptCount"]));
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task LargeStoredSelectorsUseBoundedJsonValueParameters() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            await store.WriteAsync(CreateReport(
                (new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc), 41, "alice"),
                (new DateTime(2026, 8, 1, 2, 0, 0, DateTimeKind.Utc), 42, "bob")));
            long[] recordIds = Enumerable.Range(1, 40000)
                .Select(static value => (long)value)
                .ToArray();

            EventReport report = await store.ReadReportAsync(new EventStoreQuery {
                RecordIds = recordIds,
                Oldest = true
            });

            Assert.Equal(new long?[] { 41, 42 }, report.Rows.Select(static row => row.RecordId));
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task DerivedSummaryReportsCannotBeWrittenBackIntoEventHistory() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            await store.WriteAsync(CreateReportForDefinition(
                "FirstDefinition",
                (new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc), 42, "alice")));
            await store.WriteAsync(CreateReportForDefinition(
                "SecondDefinition",
                (new DateTime(2026, 8, 1, 2, 0, 0, DateTimeKind.Utc), 43, "bob")));
            EventReport summary = await store.CreateSummaryReportAsync(
                new EventStoreQuery(),
                EventStoreSummaryPeriod.Day);

            InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
                store.WriteAsync(summary));

            Assert.Contains("summary", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(2, (await store.ReadReportAsync(new EventStoreQuery())).Rows.Count);
        } finally {
            DeleteStore(path);
        }
    }

    private static EventReportSectionSchema CreateSchema(string name, string column) => new() {
        Name = name,
        DisplayName = name,
        Kind = EventReportSectionKind.Custom,
        Columns = new[] { CreateColumn(column, typeof(string)) }
    };

    private static EventReportColumnSchema CreateColumn(string name, Type type) => new() {
        Name = name,
        DisplayName = name,
        ValueTypeName = EventReportColumnSchema.GetStableTypeName(type)
    };
}
