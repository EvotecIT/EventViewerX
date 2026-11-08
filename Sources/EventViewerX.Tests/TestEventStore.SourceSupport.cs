using EventViewerX.Reporting;
using EventViewerX.Rules.SQL;
using EventViewerX.Storage;
using Xunit;

namespace EventViewerX.Tests;

public sealed partial class TestEventStore {
    [Theory]
    [InlineData(EventType.SqlDatabaseCreated, 17137)]
    [InlineData(EventType.BitLockerKeyChange, 4673)]
    [InlineData(EventType.BitLockerKeyChange, 4692)]
    public async Task HistoricalDetectionKeepsRetainedTypeIdentityWithoutRequiringItsNativeSource(EventType type, int eventId) {
        string path = CreateStorePath();
        try {
            EventObject source = new SavedEventRecord {
                EventId = eventId, ProviderName = type == EventType.SqlDatabaseCreated ? "MSSQLSERVER" : "Microsoft-Windows-Security-Auditing",
                Channel = type == EventType.SqlDatabaseCreated ? "Application" : "Security", RecordId = 1,
                Computer = "sql-fixture", TimeCreatedUtc = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc),
                Message = "Starting up database 'fixture'.", MessageRenderStatus = EventMessageRenderStatus.Rendered
            }.ToEventObject("historical-fixture", EventReadMode.StructuredDataAndMessage);
            // A legacy stored type is retained evidence, not proof that a new database was created.
            var store = new EventStore(path);
            EventTypeRecord record = type == EventType.SqlDatabaseCreated
                ? new SqlDatabaseCreated(source) : new EventViewerX.Rules.Windows.BitLockerKeyChange(source);
            await store.WriteAsync(EventReportEngine.Create(new object[] { record }));
            EventDetectionPlan plan = EventDetectionPlan.Compile(new[] {
                new EventDetectionRule(new EventDetectionRuleDefinition {
                    RuleId = "historical-type", Title = "Historical type identity", EventTypes = new[] { type }
                })
            });
            EventDetectionExecutionResult result = await store.EvaluateDetectionAsync(new EventStoreQuery(), plan);
            EventObservation retained = Assert.Single(result.Observations);
            Assert.Equal(type.ToString(), retained.TypeName);
            Assert.Equal(eventId, retained.SourceEvent.Id);
            Assert.Equal("historical-type", Assert.Single(result.Findings).RuleId);
            if (type == EventType.SqlDatabaseCreated) {
                Assert.Throws<NotSupportedException>(() => EventDefinitionCompiler.BuildQueryXml(new[] { type }));
            } else {
                Assert.DoesNotContain(eventId, EventTypeCatalog.GetSources(new[] { type }).SelectMany(item => item.EventIds));
            }
        } finally { DeleteStore(path); }
    }
}