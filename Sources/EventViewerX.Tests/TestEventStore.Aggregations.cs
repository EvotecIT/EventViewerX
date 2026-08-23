using EventViewerX.Reporting;
using EventViewerX.Storage;
using Xunit;

namespace EventViewerX.Tests;

public sealed partial class TestEventStore {
    [Fact]
    public async Task SqliteTopRankingSupportsFirstAndLastSeenMeasures() {
        string path = CreateStorePath();
        try {
            EventReport report = CreateReport(
                (new DateTime(2026, 8, 22, 10, 0, 0, DateTimeKind.Utc), 1, "Alice"),
                (new DateTime(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc), 2, "Bob"));
            var store = new EventStore(path);
            await store.WriteAsync(report);
            var definition = new EventAggregationDefinition {
                GroupBy = new[] { "RecordId" },
                Measures = new[] {
                    new EventAggregationMeasure {
                        Operation = EventAggregationOperation.LastSeen,
                        Field = "TimeCreated",
                        OutputName = "Latest"
                    }
                },
                Top = 1,
                RankingMeasure = "Latest"
            };

            EventAggregationResult pushed = await store.AggregateAsync(new EventStoreQuery(), definition);

            Assert.Equal(EventAggregationExecutionMode.SqlitePushdown, pushed.ExecutionMode);
            Assert.Equal(2L, Assert.Single(pushed.Rows).Group["RecordId"]);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task SqliteAggregationMatchesManagedCaseInsensitiveDistinctContract() {
        string path = CreateStorePath();
        try {
            EventReport report = CreateReport(
                (new DateTime(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc), 1, "Alice"),
                (new DateTime(2026, 8, 23, 10, 1, 0, DateTimeKind.Utc), 2, "alice"),
                (new DateTime(2026, 8, 23, 10, 2, 0, DateTimeKind.Utc), 3, "Bob"));
            var store = new EventStore(path);
            await store.WriteAsync(report);
            var definition = new EventAggregationDefinition {
                GroupBy = new[] { "Provider" },
                Measures = new[] {
                    new EventAggregationMeasure {
                        Operation = EventAggregationOperation.DistinctCount,
                        Field = "SourceComputer",
                        OutputName = "Sources"
                    }
                }
            };
            EventStoreAggregationPlan plan = EventStore.PlanAggregation(new EventStoreQuery(), definition);
            EventAggregationResult pushed = await store.AggregateAsync(new EventStoreQuery(), definition);
            EventAggregationResult managed = EventAggregationEngine.Aggregate(report, definition);

            Assert.Equal(EventAggregationExecutionMode.SqlitePushdown, plan.ExecutionMode);
            Assert.Equal(EventAggregationExecutionMode.SqlitePushdown, pushed.ExecutionMode);
            Assert.Equal(managed.Rows.Single().Measures["Sources"], pushed.Rows.Single().Measures["Sources"]);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task SqliteAggregationFallsBackForNormalizedCustomFields() {
        string path = CreateStorePath();
        try {
            EventReport report = CreateReport(
                (new DateTime(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc), 1, "Alice"));
            var store = new EventStore(path);
            await store.WriteAsync(report);
            var definition = new EventAggregationDefinition { GroupBy = new[] { "User" } };

            EventStoreAggregationPlan plan = EventStore.PlanAggregation(new EventStoreQuery(), definition);
            EventAggregationResult result = await store.AggregateAsync(new EventStoreQuery(), definition);

            Assert.Equal(EventAggregationExecutionMode.Managed, plan.ExecutionMode);
            Assert.Equal(EventAggregationExecutionMode.Managed, result.ExecutionMode);
            Assert.Equal("Alice", Assert.Single(result.Rows).Group["User"]);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task SqliteAggregationExcludesNullGroupsLikeManagedAggregation() {
        string path = CreateStorePath();
        try {
            EventReport report = CreateReport(
                (new DateTime(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc), 1, "Alice"),
                (new DateTime(2026, 8, 23, 10, 1, 0, DateTimeKind.Utc), 2, "Bob"));
            report.Rows[1].RecordId = null;
            var store = new EventStore(path);
            await store.WriteAsync(report);
            var definition = new EventAggregationDefinition {
                GroupBy = new[] { "RecordId" },
                GroupNulls = EventAggregationNullPolicy.Exclude
            };

            EventAggregationResult pushed = await store.AggregateAsync(new EventStoreQuery(), definition);
            EventAggregationResult managed = EventAggregationEngine.Aggregate(report, definition);

            Assert.Equal(EventAggregationExecutionMode.SqlitePushdown, pushed.ExecutionMode);
            Assert.Equal(2, pushed.InputRows);
            Assert.Equal(managed.Rows.Count, pushed.Rows.Count);
            Assert.Equal(1L, Assert.Single(pushed.Rows).Group["RecordId"]);
        } finally {
            DeleteStore(path);
        }
    }
}
