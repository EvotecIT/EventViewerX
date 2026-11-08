using EventViewerX.Reporting;
using EventViewerX.Rules.SQL;
using EventViewerX.Storage;
using Xunit;

namespace EventViewerX.Tests;

public sealed partial class TestEventStore {
    [Theory]
    [InlineData(EventType.SqlDatabaseCreated, 17137, false)]
    [InlineData(EventType.BitLockerKeyChange, 4673, false)]
    [InlineData(EventType.BitLockerKeyChange, 4692, false)]
    [InlineData(EventType.SqlDatabaseCreated, 17137, true)]
    public async Task HistoricalDetectionKeepsRetainedTypeIdentityWithoutRequiringItsNativeSource(EventType type, int eventId, bool includeUntypedRule) {
        string path = CreateStorePath();
        try {
            EventObject source = new SavedEventRecord {
                EventId = eventId, ProviderName = type == EventType.SqlDatabaseCreated ? "MSSQLSERVER" : "Microsoft-Windows-Security-Auditing",
                Channel = type == EventType.SqlDatabaseCreated ? "Application" : "Security", RecordId = 1,
                Computer = "history-fixture", TimeCreatedUtc = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc),
                Message = "Retained legacy type identity.", MessageRenderStatus = EventMessageRenderStatus.Rendered
            }.ToEventObject("historical-fixture", EventReadMode.StructuredDataAndMessage);
            // A legacy stored type is retained evidence, not proof that a new database was created.
            var store = new EventStore(path);
            EventTypeRecord record = type == EventType.SqlDatabaseCreated
                ? new SqlDatabaseCreated(source) : new EventViewerX.Rules.Windows.BitLockerKeyChange(source);
            EventObject earlier = new SavedEventRecord {
                EventId = 9999, ProviderName = "Fixture", Channel = "Application", RecordId = 2,
                Computer = "history-fixture", TimeCreatedUtc = new DateTime(2026, 11, 1, 11, 0, 0, DateTimeKind.Utc)
            }.ToEventObject("historical-fixture", EventReadMode.StructuredData);
            await store.WriteAsync(EventReportEngine.Create(new object[] { earlier, record }));
            var rules = new List<IEventDetectionRule> {
                new EventDetectionRule(new EventDetectionRuleDefinition {
                    RuleId = "historical-type", Title = "Historical type identity", EventTypes = new[] { type }
                })
            };
            if (includeUntypedRule) {
                rules.Add(new EventDetectionRule(new EventDetectionRuleDefinition {
                    RuleId = "raw-event", Title = "Required raw event", EventIds = new[] { 9999 }
                }));
            }
            EventDetectionPlan plan = EventDetectionPlan.Compile(rules);
            EventDetectionExecutionResult result = await store.EvaluateDetectionAsync(new EventStoreQuery(), plan,
                new EventDetectionEngineOptions(maximumObservations: includeUntypedRule ? 10 : 1));
            Assert.Equal(includeUntypedRule ? 2 : 1, result.Observations.Count);
            Assert.True(result.IsEvaluationComplete);
            EventObservation retained = Assert.Single(result.Observations, item => item.TypeName == type.ToString());
            Assert.Equal(type.ToString(), retained.TypeName);
            Assert.Equal(eventId, retained.SourceEvent.Id);
            Assert.Single(result.Findings, item => item.RuleId == "historical-type");
            if (includeUntypedRule) { Assert.Single(result.Findings, item => item.RuleId == "raw-event"); }
            Assert.Empty((await store.EvaluateDetectionAsync(new EventStoreQuery { Types = new[] { EventType.OSStartup } }, plan)).Findings);
            Assert.Empty((await store.EvaluateDetectionAsync(new EventStoreQuery { DefinitionNames = new[] { "OSStartup" } }, plan)).Findings);
            Assert.Empty((await store.EvaluateDetectionAsync(new EventStoreQuery { EventIds = new[] { 1 } }, plan)).Findings);
            if (type == EventType.SqlDatabaseCreated) {
                Assert.Throws<NotSupportedException>(() => EventDefinitionCompiler.BuildQueryXml(new[] { type }));
            } else {
                Assert.DoesNotContain(eventId, EventTypeCatalog.GetSources(new[] { type }).SelectMany(item => item.EventIds));
            }
        } finally { DeleteStore(path); }
    }
}