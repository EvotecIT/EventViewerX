using EventViewerX.Reporting;
using EventViewerX.Rules.SQL;
using EventViewerX.Storage;
using Xunit;

namespace EventViewerX.Tests;

public sealed partial class TestEventStore {
    [Fact]
    public async Task HistoricalDetectionKeepsRetainedTypeIdentityWithoutRequiringItsNativeSource() {
        string path = CreateStorePath();
        try {
            EventObject source = new SavedEventRecord {
                EventId = 17137, ProviderName = "MSSQLSERVER", Channel = "Application", RecordId = 1,
                Computer = "sql-fixture", TimeCreatedUtc = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc),
                Message = "Starting up database 'fixture'.", MessageRenderStatus = EventMessageRenderStatus.Rendered
            }.ToEventObject("historical-fixture", EventReadMode.StructuredDataAndMessage);
            // A legacy stored type is retained evidence, not proof that a new database was created.
            var store = new EventStore(path);
            await store.WriteAsync(EventReportEngine.Create(new object[] { new SqlDatabaseCreated(source) }));
            EventDetectionPlan plan = EventDetectionPlan.Compile(new[] {
                new EventDetectionRule(new EventDetectionRuleDefinition {
                    RuleId = "historical-sql", Title = "Historical SQL identity", EventTypes = new[] { EventType.SqlDatabaseCreated }
                })
            });
            EventDetectionExecutionResult result = await store.EvaluateDetectionAsync(new EventStoreQuery(), plan);
            EventObservation retained = Assert.Single(result.Observations);
            Assert.Equal(nameof(EventType.SqlDatabaseCreated), retained.TypeName);
            Assert.Equal(17137, retained.SourceEvent.Id);
            Assert.Equal("historical-sql", Assert.Single(result.Findings).RuleId);
            Assert.Throws<NotSupportedException>(() => EventDefinitionCompiler.BuildQueryXml(new[] { EventType.SqlDatabaseCreated }));
        } finally { DeleteStore(path); }
    }
}