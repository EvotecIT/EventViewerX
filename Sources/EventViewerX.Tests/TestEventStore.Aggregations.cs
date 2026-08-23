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
    public async Task StoredDistinctAggregationFallsBackToManagedBounds() {
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

            Assert.Equal(EventAggregationExecutionMode.Managed, plan.ExecutionMode);
            Assert.Contains("MaximumStateBytes", plan.Reason, StringComparison.Ordinal);
            Assert.Equal(EventAggregationExecutionMode.Managed, pushed.ExecutionMode);
            Assert.Equal(managed.Rows.Single().Measures["Sources"], pushed.Rows.Single().Measures["Sources"]);
        } finally {
            DeleteStore(path);
        }
    }


    [Fact]
    public async Task SqliteEmptyInputMatchesManagedZeroRowShape() {
        string path = CreateStorePath();
        try {
            var store = new EventStore(path);
            await store.WriteAsync(CreateReport(
                (new DateTime(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc), 1, "Alice")));
            var query = new EventStoreQuery { StartTime = new DateTime(2026, 8, 24, 0, 0, 0, DateTimeKind.Utc) };
            var definition = new EventAggregationDefinition();

            EventAggregationResult pushed = await store.AggregateAsync(query, definition);
            EventReport emptyReport = await store.ReadReportAsync(query);
            EventAggregationResult managed = EventAggregationEngine.Aggregate(emptyReport, definition);

            Assert.Equal(EventAggregationExecutionMode.SqlitePushdown, pushed.ExecutionMode);
            Assert.Empty(pushed.Rows);
            Assert.Equal(0, pushed.InputRows);
            Assert.Equal(managed.Rows.Count, pushed.Rows.Count);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task SqliteStateBudgetUsesTheManagedGroupIdentityCost() {
        string path = CreateStorePath();
        try {
            EventReport report = CreateReport(
                (new DateTime(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc), 1, "Alice"));
            var store = new EventStore(path);
            await store.WriteAsync(report);
            var definition = new EventAggregationDefinition {
                GroupBy = new[] { "Provider" },
                MaximumStateBytes = 100
            };

            EventAggregationResult pushed = await store.AggregateAsync(new EventStoreQuery(), definition);
            EventAggregationResult managed = EventAggregationEngine.Aggregate(report, definition);

            Assert.Equal(EventAggregationExecutionMode.SqlitePushdown, pushed.ExecutionMode);
            Assert.False(pushed.AggregationComplete);
            Assert.Empty(pushed.Rows);
            Assert.Equal(managed.InputRows, pushed.InputRows);
            Assert.Equal(managed.AggregationComplete, pushed.AggregationComplete);
            Assert.Contains("MaximumStateBytes", pushed.Diagnostic, StringComparison.Ordinal);
        } finally {
            DeleteStore(path);
        }
    }

    [Fact]
    public async Task SqliteGroupBoundFailureRetainsEvaluatedInputRows() {
        string path = CreateStorePath();
        try {
            EventReport report = CreateReport(
                (new DateTime(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc), 1, "Alice"),
                (new DateTime(2026, 8, 23, 10, 1, 0, DateTimeKind.Utc), 2, "Bob"));
            var store = new EventStore(path);
            await store.WriteAsync(report);
            var definition = new EventAggregationDefinition {
                GroupBy = new[] { "RecordId" },
                MaximumGroups = 1
            };

            EventAggregationResult pushed = await store.AggregateAsync(new EventStoreQuery(), definition);

            Assert.Equal(EventAggregationExecutionMode.SqlitePushdown, pushed.ExecutionMode);
            Assert.False(pushed.AggregationComplete);
            Assert.Equal(2, pushed.InputRows);
            Assert.Contains("MaximumGroups", pushed.Diagnostic, StringComparison.Ordinal);
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
